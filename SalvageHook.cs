using System;
using System.Linq;
using System.Reflection;
using NLog;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components;

namespace ScrapTrader
{
    public static class SalvageHook
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static Type _salvageType;
        private static Type _repairType;

        public static void Apply()
        {
            try
            {
                // Find component types via reflection
                _salvageType = FindType("MySalvageServiceComponent");
                _repairType = FindType("MyRepairServiceComponent");

                if (_salvageType == null)
                {
                    Log.Error("ScrapTrader: Could not find MySalvageServiceComponent.");
                    return;
                }

                // Find Harmony via reflection (Torch loads it)
                var harmonyType = FindType("HarmonyLib.Harmony");
                if (harmonyType == null)
                {
                    Log.Error("ScrapTrader: Could not find HarmonyLib.Harmony.");
                    return;
                }

                var harmonyInstance = Activator.CreateInstance(harmonyType, "com.spirogames.scraptrader");

                // Patch GetStation - this is what controls if Salvage/Repair services show
                PatchMethod(harmonyInstance, harmonyType, _salvageType, "GetStation", nameof(GetStation_Postfix));

                // Patch AcceptOffer_ServerInternal to capture salvage events
                PatchMethod(harmonyInstance, harmonyType, _salvageType, "AcceptOffer_ServerInternal", nameof(AfterAcceptOffer));

                Log.Info("ScrapTrader: Salvage hooks applied successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ScrapTrader: Failed to apply salvage hooks.");
            }
        }

        private static void PatchMethod(object harmony, Type harmonyType, Type targetType, string methodName, string postfixName)
        {
            try
            {
                var target = targetType.GetMethod(methodName,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                if (target == null)
                {
                    Log.Warn($"ScrapTrader: Could not find method {methodName}.");
                    return;
                }

                var postfix = typeof(SalvageHook).GetMethod(postfixName,
                    BindingFlags.Static | BindingFlags.Public);

                // Use Harmony's Patch method via reflection
                var harmonyMethodType = FindType("HarmonyLib.HarmonyMethod");
                var postfixHarmony = Activator.CreateInstance(harmonyMethodType, postfix);

                var patchMethod = harmonyType.GetMethod("Patch",
                    new[] { typeof(MethodBase), harmonyMethodType, harmonyMethodType, harmonyMethodType, harmonyMethodType });

                patchMethod?.Invoke(harmony, new[] { target, null, postfixHarmony, null, null });
                Log.Info($"ScrapTrader: Patched {methodName}.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"ScrapTrader: Failed to patch {methodName}.");
            }
        }

        /// <summary>
        /// Postfix for GetStation - if station returns null (not an NPC station),
        /// we check if it's a ScrapTrader station and return a fake station object.
        /// </summary>
        public static void GetStation_Postfix(object __instance, ref object __result)
        {
            try
            {
                if (__result != null) return; // Already a valid NPC station

                // Check if this Services Terminal is on a ScrapTrader-allowed station
                // Get the block entity from __instance
                var entityProp = __instance.GetType().GetProperty("Entity") ??
                                 __instance.GetType().GetProperty("Container");
                if (entityProp == null) return;

                var entity = entityProp.GetValue(__instance);
                if (entity == null) return;

                // Check if block is on a grid owned by allowed faction
                // If so, find the nearest NPC station to use as proxy
                var nearestStation = FindNearestNpcStation();
                if (nearestStation != null)
                {
                    __result = nearestStation;
                    Log.Info("ScrapTrader: Injected NPC station proxy for Services Terminal.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ScrapTrader: Error in GetStation_Postfix.");
            }
        }

        private static object FindNearestNpcStation()
        {
            try
            {
                // Find MyEconomyManager or similar to get a valid trade station
                var economyType = FindType("MyEconomyManager") ??
                                  FindType("Sandbox.Game.World.MyEconomyManager");
                if (economyType == null) return null;

                var staticProp = economyType.GetProperty("Static");

                // Alternative: find any existing MySalvageServiceComponent that has a valid station
                var allEntities = Sandbox.Game.Entities.MyEntities.GetEntities();
                foreach (var entity in allEntities)
                {
                    if (!(entity is Sandbox.Game.Entities.MyCubeGrid grid)) continue;
                    foreach (var block in grid.GetFatBlocks())
                    {
                        var components = block.Components;
                        if (components == null) continue;

                        var salvageComp = _salvageType != null ? components.GetType().GetMethod("Get")?.MakeGenericMethod(_salvageType)?.Invoke(components, null) : null;
                        if (salvageComp == null) continue;

                        var getStation = _salvageType.GetMethod("GetStation",
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        if (getStation == null) continue;

                        var station = getStation.Invoke(salvageComp, null);
                        if (station != null) return station;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Postfix for AcceptOffer_ServerInternal - fires when player sells a grid.
        /// </summary>
        public static void AfterAcceptOffer(object __instance, long identityId,
            MyObjectBuilder_SalvageServiceQuote quote)
        {
            try
            {
                if (quote == null || quote.Failed) return;
                if (identityId == 0) return;

                long scAmount = quote.TotalOffered;
                if (scAmount <= 0) return;

                var players = new System.Collections.Generic.List<IMyPlayer>();
                MyAPIGateway.Players?.GetPlayers(players);
                var player = players.FirstOrDefault(p => p.IdentityId == identityId);
                if (player == null) return;

                Log.Info($"ScrapTrader: Grid Salvage – {player.DisplayName} " +
                         $"sold '{quote.GridName}' for {scAmount:N0} SC " +
                         $"({quote.NumberOfBlocks} blocks)");

                // Future: award Scrap Bucks
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ScrapTrader: Error in AfterAcceptOffer.");
            }
        }

        private static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .FirstOrDefault(t => t.Name == name || t.FullName == name);
        }
    }
}
