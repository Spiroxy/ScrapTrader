using System;
using System.Collections.Generic;
using NLog;
using Sandbox.Game.World;
using VRage.Game;
using VRageMath;

namespace ScrapTrader
{
    /// <summary>
    /// Manages registration of ScrapTrader grids as real MyFactionStation objects
    /// so that ServicesTerminal Salvage/Repair and Store all work correctly.
    /// </summary>
    public class StationRegistrar
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly ScrapTraderConfig _config;
        private readonly HashSet<long> _registeredGridIds = new HashSet<long>();

        public int RegisteredCount => _registeredGridIds.Count;

        public StationRegistrar(ScrapTraderConfig config)
        {
            _config = config;
        }

        public void RegisterAllStations()
        {
            try
            {
                int count = 0;
                foreach (var entity in Sandbox.Game.Entities.MyEntities.GetEntities())
                {
                    if (!(entity is Sandbox.Game.Entities.MyCubeGrid grid)) continue;
                    if (TryRegisterGrid(grid)) count++;
                }
                Log.Info($"ScrapTrader: Registered {count} station(s) with faction system.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ScrapTrader: Failed to register stations.");
            }
        }

        public bool TryRegisterGrid(Sandbox.Game.Entities.MyCubeGrid grid)
        {
            if (_registeredGridIds.Contains(grid.EntityId)) return false;

            // Check connector naming
            bool hasConnector = false;
            bool isDeepSpace = true;
            foreach (var block in grid.GetFatBlocks())
            {
                if (!(block is Sandbox.ModAPI.IMyShipConnector connector)) continue;
                if (connector.CustomName.Contains(_config.ConnectorNamePlanet))
                {
                    hasConnector = true; isDeepSpace = false; break;
                }
                if (connector.CustomName.Contains(_config.ConnectorNameSpace))
                {
                    hasConnector = true; isDeepSpace = true; break;
                }
            }
            if (!hasConnector) return false;

            // Check owner faction
            var gridOwner = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0L;
            var ownerIdentity = Sandbox.Game.World.MySession.Static?.Players?.TryGetIdentity(gridOwner);
            if (ownerIdentity == null) return false;
            if (!_config.AllowedStationFactions.Contains(ownerIdentity.DisplayName)) return false;

            // Find NPC faction to register under
            var faction = FindNpcFaction();
            if (faction == null)
            {
                Log.Warn("ScrapTrader: No NPC faction found.");
                return false;
            }

            var pos = grid.PositionComp.GetPosition();

            // Find SafeZone
            long safeZoneEntityId = 0;
            string expectedSzName = "SZ:" + grid.DisplayName;
            foreach (var entity in Sandbox.Game.Entities.MyEntities.GetEntities())
            {
                if (!(entity is Sandbox.Game.Entities.MySafeZone sz)) continue;
                if (sz.DisplayName == expectedSzName ||
                    (sz.DisplayName.StartsWith("SZ:(NPC-ECONOMY)") &&
                     VRageMath.Vector3D.Distance(sz.PositionComp.GetPosition(), pos) < 2000.0))
                {
                    safeZoneEntityId = sz.EntityId;
                    Log.Info($"ScrapTrader: Found SafeZone '{sz.DisplayName}' EntityId={safeZoneEntityId}");
                    break;
                }
            }

            if (safeZoneEntityId == 0)
                Log.Warn($"ScrapTrader: No SafeZone found for grid '{grid.DisplayName}'.");

            // Create real MyFactionStation
            var station = new MyFactionStation(
                grid.EntityId,              // id
                grid.DisplayName,           // name
                pos,                        // position
                MyStationTypeEnum.SpaceStation, // type
                faction,                    // faction
                "",                         // prefabName
                null,                       // generatedItemsContainerTypeId
                null, null,                 // up, forward
                isDeepSpace                 // isDeep
            );
            station.StationEntityId = grid.EntityId;
            station.SafeZoneEntityId = safeZoneEntityId;

            faction.AddStation(station);
            _registeredGridIds.Add(grid.EntityId);

            Log.Info($"ScrapTrader: Registered '{grid.DisplayName}' (EntityId={grid.EntityId}) under faction '{faction.Tag}'.");
            return true;
        }

        private MyFaction FindNpcFaction()
        {
            // Try allowed factions first
            foreach (var kvp in Sandbox.Game.World.MySession.Static.Factions)
            {
                var f = kvp.Value;
                if (_config.AllowedStationFactions.Contains(f.Name) ||
                    _config.AllowedStationFactions.Contains(f.Tag))
                    return f;
            }
            // Fallback: any NPC faction
            foreach (var kvp in Sandbox.Game.World.MySession.Static.Factions)
            {
                var f = kvp.Value;
                if (f.FactionTypeString != "PlayerMade" &&
                    f.FactionTypeString != "None" &&
                    !string.IsNullOrEmpty(f.FactionTypeString))
                    return f;
            }
            return null;
        }

        public void UnregisterAll() => _registeredGridIds.Clear();
    }
}
