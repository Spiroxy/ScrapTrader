using System;
using System.Collections.Generic;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.ObjectBuilders;

namespace ScrapTrader
{
    public class GridEvaluator
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly ScrapTraderConfig _config;
        private readonly Dictionary<string, float> _priceCache = new Dictionary<string, float>();

        public GridEvaluator(ScrapTraderConfig config)
        {
            _config = config;
        }

        public void BuildPriceCache()
        {
            _priceCache.Clear();
            int found = 0;

            foreach (var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                if (!(def is MyPhysicalItemDefinition physDef)) continue;
                if (physDef.MinimalPricePerUnit <= 0) continue;

                var key = physDef.Id.SubtypeName;
                if (!_priceCache.ContainsKey(key))
                {
                    _priceCache[key] = physDef.MinimalPricePerUnit;
                    found++;
                }
            }

            Log.Info($"ScrapTrader: Loaded {found} prices from SE definitions.");
        }

        public EvaluationResult EvaluateSalvage(MyCubeGrid grid)
        {
            var result = new EvaluationResult { GridName = grid.DisplayName };
            var blacklist = new HashSet<string>(_config.BlacklistedBlockTypes);

            foreach (var block in grid.GetFatBlocks())
            {
                var subtypeId = block.BlockDefinition.Id.SubtypeName;
                if (blacklist.Contains(subtypeId)) { result.SkippedBlocks++; continue; }

                var slim = block.SlimBlock;
                float condition = (slim != null && slim.MaxIntegrity > 0)
                    ? slim.BuildIntegrity / slim.MaxIntegrity
                    : 1f;

                var blockDef = block.BlockDefinition as MyCubeBlockDefinition;
                if (blockDef?.Components == null) continue;

                float blockValue = CalculateBlockValue(blockDef, condition);
                if (blockValue <= 0) continue;

                result.TotalRawValue += blockValue;
                result.BlockCount++;
            }

            float payout = result.TotalRawValue * (1f - _config.SalvageFeePercent);
            result.FinalPayout = Math.Max(_config.MinimumPayout, (long)payout);
            return result;
        }

        public EvaluationResult EvaluateRepair(MyCubeGrid grid)
        {
            var result = new EvaluationResult { GridName = grid.DisplayName };
            var blacklist = new HashSet<string>(_config.BlacklistedBlockTypes);

            foreach (var block in grid.GetFatBlocks())
            {
                var subtypeId = block.BlockDefinition.Id.SubtypeName;
                if (blacklist.Contains(subtypeId)) { result.SkippedBlocks++; continue; }

                var slim = block.SlimBlock;
                float condition = (slim != null && slim.MaxIntegrity > 0)
                    ? slim.BuildIntegrity / slim.MaxIntegrity
                    : 1f;

                float missing = 1f - condition;
                if (missing <= 0) continue;

                var blockDef = block.BlockDefinition as MyCubeBlockDefinition;
                if (blockDef?.Components == null) continue;

                float blockValue = CalculateBlockValue(blockDef, missing);
                if (blockValue <= 0) continue;

                result.TotalRawValue += blockValue;
                result.BlockCount++;
            }

            float cost = result.TotalRawValue * (1f + _config.RepairFeePercent);
            result.FinalPayout = (long)cost;
            return result;
        }

        private float CalculateBlockValue(MyCubeBlockDefinition blockDef, float factor)
        {
            float total = 0f;
            foreach (var component in blockDef.Components)
            {
                var compSubtype = component.Definition.Id.SubtypeName;
                if (_priceCache.TryGetValue(compSubtype, out float price))
                    total += price * component.Count * Math.Max(0f, Math.Min(1f, factor));
            }
            return total;
        }

        public float GetBlockComponentValue(MyCubeBlockDefinition blockDef)
        {
            if (blockDef?.Components == null) return 0f;
            float total = 0f;
            foreach (var component in blockDef.Components)
            {
                var compSubtype = component.Definition.Id.SubtypeName;
                if (_priceCache.TryGetValue(compSubtype, out float price))
                    total += price * component.Count;
            }
            return total;
        }
    }

    public class EvaluationResult
    {
        public string GridName    { get; set; } = "";
        public int BlockCount     { get; set; } = 0;
        public int SkippedBlocks  { get; set; } = 0;
        public float TotalRawValue { get; set; } = 0f;
        public long FinalPayout   { get; set; } = 0;
    }
}
