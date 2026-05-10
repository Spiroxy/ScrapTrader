using System;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace ScrapTrader
{
    [Category("scrap")]
    public class ScrapCommands : CommandModule
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        [Command("list", "Show available items (must be within 100m of station).")]
        [Permission(MyPromoteLevel.None)]
        public void List()
        {
            var player = Context.Player;
            if (player == null) { Context.Respond("Players only."); return; }

            if (!IsNearStation(player, 100f, out string stationType))
            {
                Context.Respond("[ScrapTrader] You must be within 100m of a ScrapTrader station.");
                return;
            }

            var allItems = ScrapTraderPlugin.Instance?.Inventory?.GetDisplayItems();
            if (allItems == null || allItems.Count == 0)
            {
                Context.Respond("[ScrapTrader] No items currently in stock.");
                return;
            }

            var sb = new System.Text.StringBuilder("\n= SCRAPTRADER INVENTORY =\n");
            sb.AppendLine($"  {"ID",-4} {"Block",-28} {"Price",12}");
            sb.AppendLine("  ----------------------------------------");

            bool anyShown = false;
            foreach (var item in allItems)
            {
                if (item.StationType != "Any" && item.StationType != stationType) continue;

                string name = item.BlockDisplayName.Length > 26
                    ? item.BlockDisplayName.Substring(0, 24) + ".."
                    : item.BlockDisplayName;

                string tag = item.StationType == "Space" ? " [Space only]" : "";
                sb.AppendLine($"  {item.Id,-4} {name,-28} {item.SellPrice,10:N0} SC{tag}");
                anyShown = true;
            }

            if (!anyShown)
            {
                Context.Respond("[ScrapTrader] No items available at this station type.");
                return;
            }

            sb.AppendLine("  ----------------------------------------");
            sb.AppendLine($"  Showing {allItems.Count} of {ScrapTraderPlugin.Instance?.Inventory?.Count ?? 0} items");
            sb.AppendLine("  Buy: !scrap buy <ID>");
            Context.Respond(sb.ToString());
        }

        [Command("buy", "Buy an item. Usage: !scrap buy <ID>")]
        [Permission(MyPromoteLevel.None)]
        public void Buy(int itemId)
        {
            var player = Context.Player;
            if (player == null) { Context.Respond("Players only."); return; }
            ScrapTraderPlugin.Instance?.GetBuyManager()?.ProcessBuy(player, itemId);
        }

        [Command("help", "Show ScrapTrader help.")]
        [Permission(MyPromoteLevel.None)]
        public void Help()
        {
            Context.Respond(
                "\n= SCRAPTRADER v1.3 =\n" +
                "\n SELLING:\n" +
                "  Use the Services Terminal on the station\n" +
                "  Select Grid Salvage to sell your ship\n" +
                "\n BUYING:\n" +
                "  Stand within 100m of station\n" +
                "  !scrap list     - Show available items\n" +
                "  !scrap buy <ID> - Purchase item\n" +
                "\n STATION REQUIREMENTS:\n" +
                "  Faction leader: Military / The Ferryman / Prime Broker\n" +
                "  Services Terminal block on station\n" +
                "  Connector: 'ScrapTrader Connector Planet'\n" +
                "  Connector: 'ScrapTrader Connector Space'\n" +
                "\n OPTIONAL:\n" +
                "  LCD Panel: 'ScrapTrader Inventory'"
            );
        }

        private bool IsNearStation(IMyPlayer player, float range, out string stationType)
        {
            stationType = "Any";
            if (player.Character == null) return false;
            var playerPos = player.Character.GetPosition();
            var config = ScrapTraderPlugin.Instance?.Config;
            if (config == null) return false;

            foreach (var entity in MyEntities.GetEntities())
            {
                if (!(entity is MyCubeGrid grid)) continue;
                var gridOwner = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0L;
                var ownerIdentity = MySession.Static?.Players?.TryGetIdentity(gridOwner);
                if (ownerIdentity == null) continue;
                if (!config.AllowedStationFactions.Contains(ownerIdentity.DisplayName)) continue;

                foreach (var block in grid.GetFatBlocks())
                {
                    if (!(block is IMyShipConnector connector)) continue;
                    string connType = null;
                    if (connector.CustomName.Contains(config.ConnectorNameSpace)) connType = "Space";
                    else if (connector.CustomName.Contains(config.ConnectorNamePlanet)) connType = "Planet";
                    if (connType == null) continue;

                    double dist = VRageMath.Vector3D.Distance(playerPos, block.PositionComp.GetPosition());
                    if (dist <= range)
                    {
                        stationType = connType;
                        return true;
                    }
                }
            }
            return false;
        }
    }

    [Category("scraptrader")]
    public class ScrapAdminCommands : CommandModule
    {
        [Command("status", "Show plugin status.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Status()
        {
            var config = ScrapTraderPlugin.Instance?.Config;
            if (config == null) return;
            var sb = new System.Text.StringBuilder("\n= SCRAPTRADER v1.3 STATUS =\n");
            sb.AppendLine($"  Interaction range:  {config.InteractionRange}m");
            sb.AppendLine($"  Spawn distance:     {config.SpawnDistanceFromConnector}m");
            sb.AppendLine($"  Display items:      {config.DisplayItemCount}");
            sb.AppendLine($"  LCD update:         {config.LcdUpdateIntervalSeconds}s");
            sb.AppendLine($"  Total in inventory: {ScrapTraderPlugin.Instance?.Inventory?.Count ?? 0}");
            sb.AppendLine($"  Tradeable blocks:   {ScrapTraderPlugin.Instance?.TradeableBlocks?.Blocks?.Count ?? 0}");
            Context.Respond(sb.ToString());
        }

        [Command("restock", "Manually trigger inventory restock.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Restock()
        {
            var plugin = ScrapTraderPlugin.Instance;
            if (plugin == null) return;
            plugin.Inventory.RestockFromConfig(plugin.TradeableBlocks, plugin.GetEvaluator());
            plugin.GetLcdManager()?.ForceUpdate();
            Context.Respond("[ScrapTrader] Inventory restocked and LCD updated.");
        }

        [Command("clearinventory", "Clear all items from inventory.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ClearInventory()
        {
            var items = ScrapTraderPlugin.Instance?.Inventory?.GetAllItems();
            if (items == null) return;
            int count = items.Count;
            foreach (var item in items)
                ScrapTraderPlugin.Instance.Inventory.TryRemoveItem(item.Id, out _);
            ScrapTraderPlugin.Instance?.GetLcdManager()?.ForceUpdate();
            Context.Respond($"[ScrapTrader] Cleared {count} item(s).");
        }

        [Command("updatelcd", "Force update all LCD panels.")]
        [Permission(MyPromoteLevel.Admin)]
        public void UpdateLcd()
        {
            ScrapTraderPlugin.Instance?.GetLcdManager()?.ForceUpdate();
            Context.Respond("[ScrapTrader] LCD panels updated.");
        }

        [Command("registerstations", "Manually register ScrapTrader grids as faction stations.")]
        [Permission(MyPromoteLevel.Admin)]
        public void RegisterStations()
        {
            var registrar = ScrapTraderPlugin.Instance?.GetStationRegistrar();
            if (registrar == null) return;
            int before = registrar.RegisteredCount;
            registrar.RegisterAllStations();
            int after = registrar.RegisteredCount;
            Context.Respond($"[ScrapTrader] Station registration complete. {after - before} new station(s) registered ({after} total).");
        }

        [Command("multiplier", "Set global price multiplier.")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetMultiplier(float multiplier)
        {
            var config = ScrapTraderPlugin.Instance?.Config;
            if (config == null) return;
            multiplier = Math.Max(0.01f, Math.Min(1f, multiplier));
            config.GlobalPriceMultiplier = multiplier;
            ScrapTraderPlugin.Instance.SaveConfig();
            Context.Respond($"[ScrapTrader] Multiplier set to {multiplier:P0}.");
        }
    }
}
