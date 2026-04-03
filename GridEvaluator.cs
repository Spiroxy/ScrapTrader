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

            foreach (var kvp in _config.FallbackPrices)
            {
                if (!_priceCache.ContainsKey(kvp.Key))
                {
                    _priceCache[kvp.Key] = kvp.Value;
                    Log.Info($"ScrapTrader: Config fallback for '{kvp.Key}': {kvp.Value} cr");
                }
            }
        }

        public EvaluationResult Evaluate(MyCubeGrid grid)
        {
            var result = new EvaluationResult();
            result.GridName = grid.DisplayName;

            var blacklist = new HashSet<string>(_config.BlacklistedBlockTypes);

            foreach (var block in grid.GetFatBlocks())
            {
                var subtypeId = block.BlockDefinition.Id.SubtypeName;

                if (blacklist.Contains(subtypeId))
                {
                    result.SkippedBlocks++;
                    continue;
                }

                float condition = 1f;
                var slim = block.SlimBlock;
                if (slim != null && slim.MaxIntegrity > 0)
                    condition = slim.BuildIntegrity / slim.MaxIntegrity;

                var blockDef = block.BlockDefinition as MyCubeBlockDefinition;
                if (blockDef == null) continue;

                float blockValue = CalculateBlockValue(blockDef, condition);

                if (blockValue > 0)
                {
                    result.TotalRawValue += blockValue;
                    result.BlockCount++;

                    if (!result.BlockBreakdown.ContainsKey(subtypeId))
                        result.BlockBreakdown[subtypeId] = new BlockValueEntry();

                    result.BlockBreakdown[subtypeId].Count++;
                    result.BlockBreakdown[subtypeId].Value += blockValue;
                }
            }

            result.FinalPayout = (long)Math.Max(
                _config.MinimumPayout,
                result.TotalRawValue * _config.GlobalPriceMultiplier
            );

            return result;
        }

        // Maps SubtypeName → ObjectBuilder TypeId string for modded/reskin blocks
        private static readonly Dictionary<string, string> _subtypeToTypeId = new Dictionary<string, string>
        {
            { "LargeBlockDrillReskin",                   "MyObjectBuilder_Drill" },
            { "SmallBlockDrillReskin",                   "MyObjectBuilder_Drill" },
            { "LargeShipGrinderReskin",                  "MyObjectBuilder_ShipGrinder" },
            { "SmallShipGrinderReskin",                  "MyObjectBuilder_ShipGrinder" },
            { "LargeJumpDriveReskin",                    "MyObjectBuilder_JumpDrive" },
            { "LargeBlockOxygenTankLab",                 "MyObjectBuilder_OxygenTank" },
            { "LargeHydrogenTank",                       "MyObjectBuilder_OxygenTank" },
            { "SmallHydrogenTank",                       "MyObjectBuilder_OxygenTank" },
            { "OxygenTankSmall",                         "MyObjectBuilder_OxygenTank" },
            { "LargeBlockLargeGeneratorWarfare2",        "MyObjectBuilder_Reactor" },
            { "LargeBlockSmallGeneratorWarfare2",        "MyObjectBuilder_Reactor" },
            { "SurvivalKitLargeReskin",                  "MyObjectBuilder_SurvivalKit" },
            { "SurvivalKitSmallReskin",                  "MyObjectBuilder_SurvivalKit" },
            { "LargeShipWelderReskin",                   "MyObjectBuilder_ShipWelder" },
            { "SmallShipWelderReskin",                   "MyObjectBuilder_ShipWelder" },
            { "LgParachute",                             "MyObjectBuilder_Parachute" },
            { "SmParachute",                             "MyObjectBuilder_Parachute" },
            { "LargeProjector",                          "MyObjectBuilder_Projector" },
            { "SmallProjector",                          "MyObjectBuilder_Projector" },
            { "LargeBlockLargeAtmosphericThrust",        "MyObjectBuilder_Thrust" },
            { "SmallBlockLargeAtmosphericThrust",        "MyObjectBuilder_Thrust" },
            { "LargeBlockSmallAtmosphericThrust",        "MyObjectBuilder_Thrust" },
            { "SmallBlockSmallAtmosphericThrust",        "MyObjectBuilder_Thrust" },
            { "SmallBlockLargeHydrogenThrustIndustrial", "MyObjectBuilder_Thrust" },
            { "LargeBlockSmallHydrogenThrustIndustrial", "MyObjectBuilder_Thrust" },
            { "SmallBlockSmallHydrogenThrustIndustrial", "MyObjectBuilder_Thrust" },
            { "LargeBlockLargeIndustrialContainer",      "MyObjectBuilder_CargoContainer" },
            { "SmallBlockLargeContainer",                "MyObjectBuilder_CargoContainer" },
            { "LargeBlockSmallContainer",                "MyObjectBuilder_CargoContainer" },
        };

        /// <summary>Returns full component value by block subtype name.</summary>
        public float GetComponentValueBySubtype(string subtype)
        {
            try
            {
                // Try with known TypeId mapping first
                if (_subtypeToTypeId.TryGetValue(subtype, out string typeIdStr))
                {
                    var typeId = new MyDefinitionId(MyObjectBuilderType.Parse(typeIdStr), subtype);
                    var def = MyDefinitionManager.Static.GetCubeBlockDefinition(typeId);
                    if (def != null)
                    {
                        float val = GetBlockComponentValue(def);
                        if (val > 0) return val;
                    }
                }

                // Fallback: try MyObjectBuilder_CubeBlock
                var fallbackId = new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), subtype);
                var fallbackDef = MyDefinitionManager.Static.GetCubeBlockDefinition(fallbackId);
                if (fallbackDef != null)
                {
                    float val = GetBlockComponentValue(fallbackDef);
                    if (val > 0) return val;
                }

                // Last resort: scan all definitions
                foreach (var d in MyDefinitionManager.Static.GetAllDefinitions())
                {
                    if (!(d is MyCubeBlockDefinition cubeDef)) continue;
                    if (cubeDef.Id.SubtypeName != subtype) continue;
                    float v = GetBlockComponentValue(cubeDef);
                    if (v > 0) return v;
                }
            }
            catch { }
            return 0f;
        }

        /// <summary>Returns full component value of a block at 100% condition.</summary>
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

        private float CalculateBlockValue(MyCubeBlockDefinition blockDef, float condition)
        {
            float totalValue = 0f;
            if (blockDef.Components == null) return 0f;

            foreach (var component in blockDef.Components)
            {
                var compSubtype = component.Definition.Id.SubtypeName;
                if (_priceCache.TryGetValue(compSubtype, out float pricePerUnit))
                {
                    float conditionFactor = Math.Min(1f, Math.Max(0f, condition * _config.ConditionMultiplier));
                    totalValue += pricePerUnit * component.Count * conditionFactor;
                }
            }

            return totalValue;
        }
    }

    public class EvaluationResult
    {
        public string GridName { get; set; } = "";
        public int BlockCount { get; set; } = 0;
        public int SkippedBlocks { get; set; } = 0;
        public float TotalRawValue { get; set; } = 0f;
        public long FinalPayout { get; set; } = 0;
        public Dictionary<string, BlockValueEntry> BlockBreakdown { get; set; }
            = new Dictionary<string, BlockValueEntry>();
    }

    public class BlockValueEntry
    {
        public int Count { get; set; } = 0;
        public float Value { get; set; } = 0f;
    }
}
