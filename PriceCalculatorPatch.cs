using System;
using System.Collections.Generic;
using System.Reflection;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Torch.Managers.PatchManager;

namespace ScrapTrader
{
    [Torch.Managers.PatchManager.PatchShim]
    internal static class PriceCalculatorPatch
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        internal static void Patch(PatchContext target)
        {
            var method = typeof(Sandbox.Game.SessionComponents.MySessionComponentEconomy)
                .GetMethod("GetGridMinimalPrice",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (method == null)
            {
                Log.Error("ScrapTrader: Could not find GetGridMinimalPrice – price patch not applied.");
                return;
            }

            var prefix = typeof(PriceCalculatorPatch).GetMethod(
                nameof(PrefixGetGridMinimalPrice),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            target.GetPattern(method).Prefixes.Add(prefix);
            Log.Info("ScrapTrader: Price calculator patch applied via PatchShim.");
        }

        private static bool PrefixGetGridMinimalPrice(
            MyCubeGrid grid,
            ref bool hasItems,
            ref int numberOfBlocks,
            ref long __result)
        {
            try
            {
                var config = ScrapTraderPlugin.Instance?.Config;
                var evaluator = ScrapTraderPlugin.Instance?.GetEvaluator();

                if (config == null || evaluator == null)
                    return true;

                hasItems = false;
                numberOfBlocks = 0;
                long totalValue = 0;

                var grids = new List<MyCubeGrid>();
                MyCubeGridGroups.Static.Mechanical.GetGroupNodes(grid, grids);

                foreach (var g in grids)
                {
                    numberOfBlocks += g.CubeBlocks.Count;

                    foreach (var slim in g.CubeBlocks)
                    {
                        if (!hasItems && slim.FatBlock != null && slim.FatBlock.HasInventory)
                        {
                            for (int i = 0; i < slim.FatBlock.InventoryCount; i++)
                            {
                                if (slim.FatBlock.GetInventory(i)?.ItemCount > 0)
                                {
                                    hasItems = true;
                                    break;
                                }
                            }
                        }

                        float condition = slim.BuildLevelRatio;
                        long blockValue = GetBlockValue(slim, evaluator);
                        totalValue += (long)(blockValue * condition);
                    }
                }

                long fee = (long)(totalValue * config.SalvageFeePercent);
                __result = Math.Max(config.MinimumPayout, totalValue - fee);

                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ScrapTrader: PriceCalculatorPatch failed, falling back to vanilla.");
                return true;
            }
        }

        private static long GetBlockValue(MySlimBlock slim, GridEvaluator evaluator)
        {
            long total = 0;
            try
            {
                var components = slim.BlockDefinition?.Components;
                if (components == null) return 0;
                foreach (var comp in components)
                    total += (long)(evaluator.GetComponentPrice(comp.Definition.Id.SubtypeName) * comp.Count);
            }
            catch { }
            return total;
        }
    }
}
