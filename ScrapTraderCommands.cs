using NLog;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace ScrapTrader
{
    [Category("scraptrader")]
    public class ScrapAdminCommands : CommandModule
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        [Command("registerstations", "Manually scan and register all ScrapTrader NPC stations.")]
        [Permission(MyPromoteLevel.Admin)]
        public void RegisterStations()
        {
            var registrar = ScrapTraderPlugin.Instance?.GetStationRegistrar();
            if (registrar == null)
            {
                Context.Respond("[ScrapTrader] Plugin not ready.");
                return;
            }

            int before = registrar.RegisteredCount;
            registrar.RegisterAllStations();
            int after = registrar.RegisteredCount;
            Context.Respond($"[ScrapTrader] Done. {after - before} new station(s) registered ({after} total).");
        }

        [Command("status", "Show ScrapTrader plugin status.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Status()
        {
            var plugin = ScrapTraderPlugin.Instance;
            if (plugin == null) return;

            Context.Respond(
                $"\n= SCRAPTRADER v1.3 STATUS =\n" +
                $"  Salvage fee:  {plugin.Config.SalvageFeePercent * 100:0}%\n" +
                $"  Repair fee:   {plugin.Config.RepairFeePercent * 100:0}%\n" +
                $"  Registered stations: {plugin.GetStationRegistrar()?.RegisteredCount ?? 0}"
            );
        }
    }
}
