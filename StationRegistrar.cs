using System.Collections.Generic;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRageMath;

namespace ScrapTrader
{
    public class StationRegistrar
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly ScrapTraderConfig _config;
        private readonly HashSet<long> _registered = new HashSet<long>();

        public int RegisteredCount => _registered.Count;

        public StationRegistrar(ScrapTraderConfig config)
        {
            _config = config;
        }

        public void RegisterAllStations()
        {
            int newCount = 0;

            foreach (var entity in MyEntities.GetEntities())
            {
                if (!(entity is MyCubeGrid grid)) continue;
                if (!grid.DisplayName.Contains(_config.GridPrefix)) continue;
                if (_registered.Contains(grid.EntityId)) continue;

                if (!HasNearbySafeZone(grid.PositionComp.GetPosition())) continue;

                _registered.Add(grid.EntityId);
                newCount++;
                Log.Info($"ScrapTrader: Registered station '{grid.DisplayName}'");
            }

            if (newCount > 0)
                Log.Info($"ScrapTrader: {newCount} new station(s) registered. Total: {_registered.Count}");
        }

        public bool IsRegistered(long entityId) => _registered.Contains(entityId);

        private bool HasNearbySafeZone(Vector3D pos)
        {
            foreach (var entity in MyEntities.GetEntities())
            {
                if (entity == null) continue;
                var name = entity.DisplayName;
                if (string.IsNullOrEmpty(name)) continue;
                if (!name.StartsWith(_config.SafeZonePrefix)) continue;
                if (Vector3D.Distance(entity.PositionComp.GetPosition(), pos) <= _config.SafeZoneMaxDistance)
                    return true;
            }
            return false;
        }
    }
}
