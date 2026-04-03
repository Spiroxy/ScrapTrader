using System;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace ScrapTrader
{
    public class BuyManager
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly ScrapTraderConfig _config;
        private readonly InventoryManager _inventory;
        private readonly TradeableBlocksConfig _tradeableBlocks;

        public BuyManager(ScrapTraderConfig config, InventoryManager inventory, TradeableBlocksConfig tradeableBlocks)
        {
            _config = config;
            _inventory = inventory;
            _tradeableBlocks = tradeableBlocks;
        }

        public void ProcessBuy(IMyPlayer player, int itemId)
        {
            if (player == null) return;

            // 1. Must be near a valid station – get station type and connector orientation
            if (!IsNearValidStation(player, out Vector3D connectorPos, out Vector3D connectorForward, out Vector3D connectorUp, out string stationType))
            {
                SendMessage(player, $"[ScrapTrader] You must be within {_config.InteractionRange}m of a ScrapTrader station to purchase items.");
                return;
            }

            // 2. Item must exist
            var item = _inventory.GetItem(itemId);
            if (item == null)
            {
                SendMessage(player, $"[ScrapTrader] Item #{itemId} not found. Use !scrap list to see available items.");
                return;
            }

            // 3. Check station type matches item
            if (item.StationType != "Any" && item.StationType != stationType)
            {
                string where = item.StationType == "Space" ? "a space station" : "a planet station";
                SendMessage(player, $"[ScrapTrader] '{item.BlockDisplayName}' is only available at {where}.");
                return;
            }

            // 4. Check prefab exists
            if (!_tradeableBlocks.TryGet(item.BlockSubtype, out var tradeableDef) || !tradeableDef.HasPrefab)
            {
                SendMessage(player, $"[ScrapTrader] No prefab configured for '{item.BlockSubtype}'.");
                return;
            }

            // 5. Check SC balance
            long balance = MyBankingSystem.GetBalance(player.IdentityId);
            if (balance < item.SellPrice)
            {
                SendMessage(player,
                    $"[ScrapTrader] Insufficient funds.\n" +
                    $"  Price:   {item.SellPrice:N0} SC\n" +
                    $"  Balance: {balance:N0} SC\n" +
                    $"  Missing: {item.SellPrice - balance:N0} SC");
                return;
            }

            // 6. Reserve item
            if (!_inventory.TryRemoveItem(itemId, out var purchasedItem))
            {
                SendMessage(player, "[ScrapTrader] Item was just purchased by another player. Please try again.");
                return;
            }

            // 7. Charge SC
            try
            {
                MyBankingSystem.ChangeBalance(player.IdentityId, -purchasedItem.SellPrice);
            }
            catch (Exception ex)
            {
                _inventory.AddItem(purchasedItem.BlockSubtype, purchasedItem.BlockDisplayName,
                    purchasedItem.Condition, purchasedItem.BuyPrice, purchasedItem.SellPrice, purchasedItem.StationType);
                Log.Error(ex, $"ScrapTrader: Payment failed for {player.DisplayName}");
                SendMessage(player, "[ScrapTrader] Payment failed. Please contact an admin.");
                return;
            }

            // 8. Spawn prefab 15m in front of connector
            var spawnPosition = connectorPos + connectorForward * _config.SpawnDistanceFromConnector;

            try
            {
                ScrapTraderPlugin.Instance.Torch.Invoke(() =>
                {
                    var resultList = new System.Collections.Generic.List<MyCubeGrid>();
                    MyPrefabManager.Static.SpawnPrefab(
                        resultList,
                        tradeableDef.Prefab,
                        spawnPosition,
                        (Vector3)connectorForward,
                        (Vector3)connectorUp
                    );
                    Log.Info($"ScrapTrader: Spawned prefab '{tradeableDef.Prefab}' at {spawnPosition} for {player.DisplayName}");
                });
            }
            catch (Exception ex)
            {
                MyBankingSystem.ChangeBalance(player.IdentityId, purchasedItem.SellPrice);
                _inventory.AddItem(purchasedItem.BlockSubtype, purchasedItem.BlockDisplayName,
                    purchasedItem.Condition, purchasedItem.BuyPrice, purchasedItem.SellPrice, purchasedItem.StationType);
                Log.Error(ex, $"ScrapTrader: Spawn failed for '{tradeableDef.Prefab}'");
                SendMessage(player, "[ScrapTrader] Failed to spawn item. SC refunded.");
                return;
            }

            Log.Info($"PURCHASE: {player.DisplayName} bought '{purchasedItem.BlockSubtype}' (ID={itemId}) for {purchasedItem.SellPrice:N0} SC");

            SendMessage(player,
                $"[ScrapTrader] Purchase complete!\n" +
                $"  {purchasedItem.BlockDisplayName}\n" +
                $"  -{purchasedItem.SellPrice:N0} SC\n" +
                $"  Item spawned {_config.SpawnDistanceFromConnector}m in front of the connector!\n" +
                $"  A pleasure to do business {player.DisplayName} – You and your Space Credits are always welcome back!");
        }

        private bool IsNearValidStation(IMyPlayer player, out Vector3D connectorPos,
            out Vector3D connectorForward, out Vector3D connectorUp, out string stationType)
        {
            connectorPos = Vector3D.Zero;
            connectorForward = Vector3D.Forward;
            connectorUp = Vector3D.Up;
            stationType = "Any";

            if (player.Character == null) return false;
            var playerPos = player.Character.GetPosition();

            foreach (var entity in MyEntities.GetEntities())
            {
                if (!(entity is MyCubeGrid grid)) continue;

                var gridOwner = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0L;
                var ownerIdentity = MySession.Static?.Players?.TryGetIdentity(gridOwner);
                if (ownerIdentity == null) continue;
                if (!_config.AllowedStationFactions.Contains(ownerIdentity.DisplayName)) continue;

                foreach (var block in grid.GetFatBlocks())
                {
                    if (!(block is IMyShipConnector connector)) continue;

                    string connType = null;
                    if (connector.CustomName.Contains(_config.ConnectorNameSpace)) connType = "Space";
                    else if (connector.CustomName.Contains(_config.ConnectorNamePlanet)) connType = "Planet";
                    if (connType == null) continue;

                    double dist = Vector3D.Distance(playerPos, block.PositionComp.GetPosition());
                    if (dist <= _config.InteractionRange)
                    {
                        connectorPos = block.PositionComp.GetPosition();
                        connectorForward = block.WorldMatrix.Forward;
                        connectorUp = block.WorldMatrix.Up;
                        stationType = connType;
                        return true;
                    }
                }
            }
            return false;
        }

        private void SendMessage(IMyPlayer player, string message)
        {
            try
            {
                var torch = ScrapTraderPlugin.Instance.Torch;
                var chatManager = torch.CurrentSession?.Managers
                    .GetManager(typeof(Torch.API.Managers.IChatManagerServer))
                    as Torch.API.Managers.IChatManagerServer;
                chatManager?.SendMessageAsOther("ScrapTrader", message, VRageMath.Color.LightGreen, player.SteamUserId);
            }
            catch (Exception ex) { Log.Error(ex, "SendMessage failed"); }
            Log.Info($"[MSG to {player.DisplayName}]: {message}");
        }
    }
}
