using System;
using System.Collections.Generic;
using System.Text;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ScrapTrader
{
    public class LcdManager
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly ScrapTraderConfig _config;
        private readonly InventoryManager _inventory;

        private int _tickCounter = 0;
        private int UpdateIntervalTicks => _config.LcdUpdateIntervalSeconds * 60;

        public LcdManager(ScrapTraderConfig config, InventoryManager inventory)
        {
            _config = config;
            _inventory = inventory;
        }

        public void Update()
        {
            _tickCounter++;
            if (_tickCounter % UpdateIntervalTicks == 0)
            {
                _inventory.RefreshDisplay();
                UpdateAllLcds();
            }
        }

        public void ForceUpdate()
        {
            _inventory.RefreshDisplay();
            UpdateAllLcds();
        }

        private void UpdateAllLcds()
        {
            var items = _inventory.GetDisplayItems();
            int updated = 0;

            foreach (var entity in MyEntities.GetEntities())
            {
                if (!(entity is MyCubeGrid grid)) continue;

                var gridOwner = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0L;
                var ownerIdentity = MySession.Static?.Players?.TryGetIdentity(gridOwner);
                if (ownerIdentity == null) continue;
                if (!_config.AllowedStationFactions.Contains(ownerIdentity.DisplayName)) continue;

                // Determine station type from connector name
                string stationType = GetStationType(grid);
                if (stationType == null) continue;

                foreach (var block in grid.GetFatBlocks())
                {
                    if (!(block is IMyTextPanel lcd)) continue;
                    if (!lcd.CustomName.Contains(_config.LcdInventoryName)) continue;

                    string content = BuildLcdContent(items, stationType);
                    lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                    lcd.FontSize = 0.6f;
                    lcd.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
                    lcd.Font = "Monospace";
                    lcd.FontColor = new VRageMath.Color(0, 255, 0);
                    lcd.BackgroundColor = new VRageMath.Color(0, 10, 0);
                    lcd.WriteText(content);
                    updated++;
                }
            }

            if (updated > 0)
                Log.Info($"ScrapTrader: Updated {updated} LCD(s).");
        }

        private string GetStationType(MyCubeGrid grid)
        {
            foreach (var block in grid.GetFatBlocks())
            {
                if (!(block is IMyShipConnector connector)) continue;
                if (connector.CustomName.Contains(_config.ConnectorNameSpace)) return "Space";
                if (connector.CustomName.Contains(_config.ConnectorNamePlanet)) return "Planet";
            }
            return null;
        }

        private string BuildLcdContent(List<InventoryItem> items, string stationType)
        {
            // Filter by station type
            var filtered = new List<InventoryItem>();
            foreach (var item in items)
                if (item.StationType == "Any" || item.StationType == stationType)
                    filtered.Add(item);

            var sb = new StringBuilder();
            sb.AppendLine("= SCRAPTRADER INVENTORY =");
            sb.AppendLine($"  {DateTime.Now:HH:mm}  |  {filtered.Count} item(s)");
            sb.AppendLine("=========================");

            if (filtered.Count == 0)
            {
                sb.AppendLine("");
                sb.AppendLine("  No items in stock.");
                sb.AppendLine("");
            }
            else
            {
                sb.AppendLine("  ID   Block              Price");
                sb.AppendLine("=========================");

                foreach (var item in filtered)
                {
                    string name = item.BlockDisplayName.Length > 18
                        ? item.BlockDisplayName.Substring(0, 16) + ".."
                        : item.BlockDisplayName;

                    sb.AppendLine($"  {item.Id,-4} {name,-18} {item.SellPrice / 1000:N0}k SC");
                }
            }

            sb.AppendLine("=========================");
            sb.AppendLine("  !scrap buy <ID>");
            return sb.ToString();
        }
    }
}
