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
    public static class ScrapTraderCommands
    {
        public static event Action<IMyPlayer> OnConfirm;
        public static event Action<IMyPlayer> OnCancel;
        public static void InvokeConfirm(IMyPlayer player) => OnConfirm?.Invoke(player);
        public static void InvokeCancel(IMyPlayer player) => OnCancel?.Invoke(player);
    }

    [Category("scrap")]
    public class ScrapCommands : CommandModule
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        [Command("sell", "Evaluate and sell the docked grid.")]
        [Permission(MyPromoteLevel.None)]
        public void Sell()
        {
            var player = Context.Player;
            if (player == null) { Context.Respond("Players only."); return; }
            ScrapTraderPlugin.Instance?.GetManager()?.InitiateSale(player);
        }

        [Command("confirm", "Confirm the pending deal.")]
        [Permission(MyPromoteLevel.None)]
        public void Confirm()
        {
            var player = Context.Player;
            if (player == null) return;
            ScrapTraderCommands.InvokeConfirm(player);
        }

        [Command("cancel", "Cancel the pending offer.")]
        [Permission(MyPromoteLevel.None)]
        public void Cancel()
        {
            var player = Context.Player;
            if (player == null) return;
            ScrapTraderCommands.InvokeCancel(player);
        }

        [Command("list", "Show current items available for purchase.")]
        [Permission(MyPromoteLevel.None)]
        public void List()
        {
            var player = Context.Player;
            if (player == null) { Context.Respond("Players only."); return; }

            // Must be within 100m of a station
            if (!IsNearStation(player, 100f, out string stationType))
            {
                Context.Respond("[ScrapTrader] You must be within 100m of a ScrapTrader station to view inventory.");
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
            foreach (var item in allItems)
            {
                // Only show items available at this station type
                if (item.StationType != "Any" && item.StationType != stationType) continue;

                string name = item.BlockDisplayName.Length > 26
                    ? item.BlockDisplayName.Substring(0, 24) + ".."
                    : item.BlockDisplayName;

                string tag = item.StationType == "Space" ? " [Space only]" : "";
                sb.AppendLine($"  {item.Id,-4} {name,-28} {item.SellPrice,10:N0} SC{tag}");
            }
            sb.AppendLine("  ----------------------------------------");
            sb.AppendLine($"  Showing {allItems.Count} of {ScrapTraderPlugin.Instance?.Inventory?.Count ?? 0} items");
            sb.AppendLine("  Buy: !scrap buy <ID>");
            Context.Respond(sb.ToString());
        }

        private bool IsNearStation(IMyPlayer player, float range, out string stationType)
        {
            stationType = "Any";
            if (player.Character == null) return false;
            var playerPos = player.Character.GetPosition();
            var config = ScrapTraderPlugin.Instance?.Config;
            if (config == null) return false;

            foreach (var entity in Sandbox.Game.Entities.MyEntities.GetEntities())
            {
                if (!(entity is Sandbox.Game.Entities.MyCubeGrid grid)) continue;
                var gridOwner = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0L;
                var ownerIdentity = Sandbox.Game.World.MySession.Static?.Players?.TryGetIdentity(gridOwner);
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
                "\n╔══ SCRAPTRADER v1.2 ══╗\n" +
                "║ SELLING:\n" +
                "║  Dock ship to ScrapTrader connector\n" +
                "║  !scrap sell     - Evaluate grid\n" +
                "║  !scrap confirm  - Accept deal\n" +
                "║  !scrap cancel   - Decline deal\n" +
                "║\n" +
                "║ BUYING:\n" +
                "║  !scrap list     - Show available items\n" +
                "║  !scrap buy <ID> - Purchase item\n" +
                "╚═══════════════════════╝"
            );
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
            var sb = new System.Text.StringBuilder("\n╔══ SCRAPTRADER v1.2 STATUS ══╗\n");
            sb.AppendLine($"║ Global multiplier:  {config.GlobalPriceMultiplier:P0}");
            sb.AppendLine($"║ Interaction range:  {config.InteractionRange}m");
            sb.AppendLine($"║ Spawn distance:     {config.SpawnDistanceFromConnector}m");
            sb.AppendLine($"║ Display items:      {config.DisplayItemCount}");
            sb.AppendLine($"║ LCD update:         {config.LcdUpdateIntervalSeconds}s");
            sb.AppendLine($"║ Total in inventory: {ScrapTraderPlugin.Instance?.Inventory?.Count ?? 0}");
            sb.AppendLine($"║ Tradeable blocks:   {ScrapTraderPlugin.Instance?.TradeableBlocks?.Blocks?.Count ?? 0}");
            sb.AppendLine("╚══════════════════════════════╝");
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
