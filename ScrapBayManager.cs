using System;
using System.Collections.Generic;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.ModAPI;
using Sandbox.Game.World;
using VRage.Game.ModAPI;
using VRageMath;
using Torch.API.Managers;
using Torch.Managers;

namespace ScrapTrader
{
    public class ScrapBayManager : IDisposable
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly GridEvaluator _evaluator;
        private readonly ScrapTraderConfig _config;
        private readonly InventoryManager _inventoryManager;
        private readonly TradeableBlocksConfig _tradeableBlocks;

        private readonly Dictionary<long, PendingDeal> _pendingDeals
            = new Dictionary<long, PendingDeal>();

        private int _tickCounter = 0;
        private const int CLEANUP_INTERVAL_TICKS = 1800;
        private const int DEAL_TIMEOUT_TICKS = 1800;

        public ScrapBayManager(InventoryManager inventoryManager, TradeableBlocksConfig tradeableBlocks, GridEvaluator evaluator)
        {
            _config = ScrapTraderPlugin.Instance.Config;
            _evaluator = evaluator;
            _inventoryManager = inventoryManager;
            _tradeableBlocks = tradeableBlocks;
        }

        public void Init()
        {
            ScrapTraderCommands.OnConfirm += HandleConfirm;
            ScrapTraderCommands.OnCancel += HandleCancel;
            Log.Info("ScrapBayManager ready. Waiting for interactions.");
        }

        public void RebuildPriceCache()
        {
            _evaluator.BuildPriceCache();
        }

        public void Update()
        {
            _tickCounter++;
            if (_tickCounter % CLEANUP_INTERVAL_TICKS == 0)
                CleanupExpiredDeals();
        }

        public void InitiateSale(IMyPlayer player)
        {
            if (player == null) return;

            long identityId = player.IdentityId;

            if (_pendingDeals.ContainsKey(identityId))
            {
                SendMessage(player, "[ScrapTrader] You already have an active offer. Type /scrap confirm or /scrap cancel.");
                return;
            }

            var dockedGrid = FindDockedGrid(player);
            if (dockedGrid == null)
            {
                return;
            }

            if (!PlayerOwnsGrid(player, dockedGrid))
            {
                SendMessage(player, "[ScrapTrader] You do not own this grid. The station only buys grids you have ownership of.");
                return;
            }

            var result = _evaluator.Evaluate(dockedGrid);

            // Take snapshot of block IDs and integrity for tamper detection
            var snapshot = new HashSet<long>();
            var integritySnapshot = new Dictionary<long, float>();
            foreach (var block in dockedGrid.GetFatBlocks())
            {
                snapshot.Add(block.EntityId);
                var slim = block.SlimBlock;
                if (slim != null)
                    integritySnapshot[block.EntityId] = slim.BuildIntegrity;
            }

            _pendingDeals[identityId] = new PendingDeal
            {
                Grid = dockedGrid,
                GridEntityId = dockedGrid.EntityId,
                EvaluationResult = result,
                PlayerId = identityId,
                CreatedAtTick = _tickCounter,
                SnapshotBlockCount = snapshot.Count,
                SnapshotBlockIds = snapshot,
                SnapshotIntegrity = integritySnapshot
            };

            string offer =
                $"\n╔══ SCRAPTRADER OFFER ══╗\n" +
                $"║ Grid:        {result.GridName}\n" +
                $"║ Blocks:      {result.BlockCount} pcs\n" +
                $"║ Raw value:   {result.TotalRawValue:N0} cr\n" +
                $"║ Station cut: {(1f - _config.GlobalPriceMultiplier) * 100:N0}%\n" +
                $"╠═══════════════════════╣\n" +
                $"║ PAYOUT: {result.FinalPayout:N0} credits\n" +
                $"╚═══════════════════════╝\n" +
                $"Type !scrap confirm to accept\n" +
                $"Type !scrap cancel to decline\n" +
                $"(Offer valid for 30 seconds)";

            SendMessage(player, offer);
            Log.Info($"Offer sent to {player.DisplayName}: {result.FinalPayout} cr for '{result.GridName}'");
        }

        private void HandleConfirm(IMyPlayer player)
        {
            long identityId = player.IdentityId;

            if (!_pendingDeals.TryGetValue(identityId, out var deal))
            {
                SendMessage(player, "[ScrapTrader] No active offer found. Interact with the terminal again.");
                return;
            }

            _pendingDeals.Remove(identityId);

            if (deal.Grid == null || deal.Grid.Closed)
            {
                SendMessage(player, "[ScrapTrader] Grid could no longer be found. Deal cancelled.");
                return;
            }

            // Tamper check – verify grid hasn't been modified since the offer
            var currentBlocks = new HashSet<long>();
            foreach (var block in deal.Grid.GetFatBlocks())
                currentBlocks.Add(block.EntityId);

            if (currentBlocks.Count < deal.SnapshotBlockCount)
            {
                int removed = deal.SnapshotBlockCount - currentBlocks.Count;
                SendMessage(player,
                    $"[ScrapTrader] Deal cancelled – {removed} block(s) were removed after the offer was made.\n" +
                    $"Re-dock your ship intact and run !scrap sell again.");
                Log.Warn($"TAMPER DETECTED: {player.DisplayName} removed {removed} blocks from '{deal.EvaluationResult.GridName}' during pending deal.");
                return;
            }

            // Also check that no NEW blocks were added (to prevent swapping cheap blocks for expensive ones)
            foreach (var id in currentBlocks)
            {
                if (!deal.SnapshotBlockIds.Contains(id))
                {
                    SendMessage(player,
                        "[ScrapTrader] Deal cancelled – blocks were added to the grid after the offer was made.\n" +
                        "Re-dock your ship and run !scrap sell again.");
                    Log.Warn($"TAMPER DETECTED: {player.DisplayName} added blocks to '{deal.EvaluationResult.GridName}' during pending deal.");
                    return;
                }
            }

            // Check that no block's integrity has changed (detects both repairs and grinding)
            foreach (var block in deal.Grid.GetFatBlocks())
            {
                var slim = block.SlimBlock;
                if (slim == null) continue;
                if (!deal.SnapshotIntegrity.TryGetValue(block.EntityId, out float originalIntegrity)) continue;

                float diff = slim.BuildIntegrity - originalIntegrity;

                if (diff > 0.01f)
                {
                    SendMessage(player,
                        "[ScrapTrader] Deal cancelled – blocks were repaired after the offer was made.\n" +
                        "Re-dock your ship without repairing it and run !scrap sell again.");
                    Log.Warn($"TAMPER DETECTED: {player.DisplayName} repaired blocks on '{deal.EvaluationResult.GridName}' during pending deal.");
                    return;
                }

                if (diff < -0.01f)
                {
                    SendMessage(player,
                        "[ScrapTrader] Deal cancelled – blocks were damaged or ground down after the offer was made.\n" +
                        "Re-dock your ship and run !scrap sell again.");
                    Log.Warn($"TAMPER DETECTED: {player.DisplayName} damaged/ground blocks on '{deal.EvaluationResult.GridName}' during pending deal.");
                    return;
                }
            }

            bool paid = PayPlayer(identityId, deal.EvaluationResult.FinalPayout);
            if (!paid)
            {
                SendMessage(player, "[ScrapTrader] Payment failed. Please contact an admin.");
                Log.Error($"Payment failed for {player.DisplayName}, {deal.EvaluationResult.FinalPayout} cr");
                return;
            }

            // Add whitelisted blocks to inventory before closing grid
            int addedToInventory = ProcessGridIntoInventory(deal.Grid, player.DisplayName);

            Log.Info($"DEAL: {player.DisplayName} sold '{deal.EvaluationResult.GridName}' " +
                     $"for {deal.EvaluationResult.FinalPayout:N0} SC " +
                     $"({deal.EvaluationResult.BlockCount} blocks, {addedToInventory} added to inventory)");

            deal.Grid.Close();

            SendMessage(player,
                $"[ScrapTrader] Deal complete!\n" +
                $"  +{deal.EvaluationResult.FinalPayout:N0} SC added to your account.\n" +
                $"  {addedToInventory} block(s) added to station inventory.\n" +
                $"  Grid '{deal.EvaluationResult.GridName}' has been scrapped. Thanks!");
        }

        private void HandleCancel(IMyPlayer player)
        {
            if (_pendingDeals.Remove(player.IdentityId))
                SendMessage(player, "[ScrapTrader] Offer cancelled. Grid was not touched.");
            else
                SendMessage(player, "[ScrapTrader] No active offer to cancel.");
        }

        /// <summary>
        /// Goes through each block on the grid, checks if it's whitelisted,
        /// calculates sell price and adds it to inventory.
        /// Returns number of items added.
        /// </summary>
        private int ProcessGridIntoInventory(MyCubeGrid grid, string sellerName)
        {
            int added = 0;

            foreach (var block in grid.GetFatBlocks())
            {
                var subtype = block.BlockDefinition.Id.SubtypeName;

                // Only process whitelisted blocks
                if (!_tradeableBlocks.TryGet(subtype, out var tradeableDef))
                    continue;

                // Skip blacklisted
                if (_config.BlacklistedBlockTypes.Contains(subtype))
                    continue;

                // Get condition
                float condition = 1f;
                var slim = block.SlimBlock;
                if (slim != null && slim.MaxIntegrity > 0)
                    condition = slim.BuildIntegrity / slim.MaxIntegrity;

                // Calculate component value
                float componentValue = _evaluator.GetBlockComponentValue(block.BlockDefinition as MyCubeBlockDefinition);

                // Buy price = what seller was paid (already done via FinalPayout)
                // Stored for reference only
                long buyPrice = (long)(componentValue * condition * _config.GlobalPriceMultiplier);

                // Sell price = full component value × sellMultiplier
                long sellPrice = (long)(componentValue * tradeableDef.SellMultiplier);

                // Get display name
                string displayName = block.BlockDefinition.DisplayNameText ?? subtype;

                _inventoryManager.AddItem(subtype, displayName, condition, buyPrice, sellPrice);
                added++;

                Log.Info($"ScrapTrader: Added to inventory – {displayName} " +
                         $"(condition={condition:P0}, sellPrice={sellPrice:N0} SC) from {sellerName}");
            }

            return added;
        }

        private MyCubeGrid FindDockedGrid(IMyPlayer player)
        {
            if (player.Character == null)
            {
                SendMessage(player, "[ScrapTrader] Could not find your character position.");
                return null;
            }

            var playerPos = player.Character.GetPosition();
            MyCubeGrid bestGrid = null;
            double bestDist = _config.InteractionRange;

            foreach (var entity in MyEntities.GetEntities())
            {
                if (!(entity is MyCubeGrid stationGrid)) continue;

                foreach (var block in stationGrid.GetFatBlocks())
                {
                    if (!(block is IMyShipConnector connector)) continue;
                    if (!connector.CustomName.Contains(_config.ConnectorNameSpace) &&
                        !connector.CustomName.Contains(_config.ConnectorNamePlanet)) continue;

                    if (connector.Status != Sandbox.ModAPI.Ingame.MyShipConnectorStatus.Connected) continue;

                    // Check that the station grid is owned by an allowed NPC faction leader
                    var gridOwner = stationGrid.BigOwners.Count > 0 ? stationGrid.BigOwners[0] : 0L;
                    var ownerIdentity = MySession.Static.Players.TryGetIdentity(gridOwner);
                    if (ownerIdentity == null || !_config.AllowedStationFactions.Contains(ownerIdentity.DisplayName))
                        continue;

                    // Check that the player is close to THIS station
                    double distToStation = VRageMath.Vector3D.Distance(playerPos, block.PositionComp.GetPosition());
                    if (distToStation > bestDist) continue;

                    var other = connector.OtherConnector;
                    if (other == null) continue;

                    var docked = (other as VRage.Game.ModAPI.IMyCubeBlock)?.CubeGrid as MyCubeGrid;
                    if (docked != null && docked != stationGrid)
                    {
                        bestGrid = docked;
                        bestDist = distToStation;
                    }
                }
            }

            if (bestGrid == null)
                SendMessage(player, $"[ScrapTrader] No official ScrapTrader station found nearby.\nMake sure you are within {_config.InteractionRange}m of a station and your ship is docked.");

            return bestGrid;
        }

        private bool PlayerOwnsGrid(IMyPlayer player, MyCubeGrid grid)
        {
            return grid.BigOwners.Contains(player.IdentityId)
                || grid.SmallOwners.Contains(player.IdentityId);
        }

        private bool PayPlayer(long identityId, long amount)
        {
            try
            {
                MyBankingSystem.ChangeBalance(identityId, amount);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error paying identityId={identityId}");
                return false;
            }
        }

        private void SendMessage(IMyPlayer player, string message)
        {
            try
            {
                var torch = ScrapTraderPlugin.Instance.Torch;
                var chatManager = torch.CurrentSession?.Managers
                    .GetManager<Torch.API.Managers.IChatManagerServer>();

                chatManager?.SendMessageAsOther("ScrapTrader", message, VRageMath.Color.LightGreen, player.SteamUserId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SendMessage failed");
            }

            Log.Info($"[MSG to {player.DisplayName}]: {message}");
        }

        private void CleanupExpiredDeals()
        {
            var expired = new List<long>();
            foreach (var kvp in _pendingDeals)
                if (_tickCounter - kvp.Value.CreatedAtTick > DEAL_TIMEOUT_TICKS)
                    expired.Add(kvp.Key);

            foreach (var id in expired)
            {
                _pendingDeals.Remove(id);
                Log.Info($"Pending deal for identityId={id} expired.");
            }
        }

        public void Dispose()
        {
            ScrapTraderCommands.OnConfirm -= HandleConfirm;
            ScrapTraderCommands.OnCancel -= HandleCancel;
        }
    }

    public class PendingDeal
    {
        public MyCubeGrid Grid { get; set; }
        public long GridEntityId { get; set; }
        public EvaluationResult EvaluationResult { get; set; }
        public long PlayerId { get; set; }
        public int CreatedAtTick { get; set; }

        // Snapshot of blocks at time of offer – used to detect tampering
        public int SnapshotBlockCount { get; set; }
        public HashSet<long> SnapshotBlockIds { get; set; }

        // Snapshot of each block's integrity – detects repairs during pending deal
        public Dictionary<long, float> SnapshotIntegrity { get; set; }
    }
}
