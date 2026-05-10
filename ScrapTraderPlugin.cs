using System.IO;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;

namespace ScrapTrader
{
    public class ScrapTraderPlugin : TorchPluginBase
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static ScrapTraderPlugin Instance { get; private set; }

        public ScrapTraderConfig Config { get; private set; }

        private GridEvaluator _evaluator;
        private StationRegistrar _stationRegistrar;
        private bool _commandsRegistered = false;
        private string _storagePath;

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;

            _storagePath = Path.Combine(StoragePath, "ScrapTrader");
            Directory.CreateDirectory(_storagePath);

            var configPath = Path.Combine(_storagePath, "ScrapTrader.cfg");
            Config = ScrapTraderConfig.Load(configPath);

            _evaluator = new GridEvaluator(Config);
            _stationRegistrar = new StationRegistrar(Config);

            Log.Info("ScrapTrader v1.3 loaded.");

            var sessionManager = torch.Managers.GetManager(typeof(ITorchSessionManager)) as ITorchSessionManager;
            if (sessionManager != null)
                sessionManager.SessionStateChanged += OnSessionStateChanged;
        }

        private void OnSessionStateChanged(ITorchSession session, TorchSessionState state)
        {
            if (state == TorchSessionState.Loaded && !_commandsRegistered)
            {
                var commandManager = session.Managers.GetManager(typeof(Torch.Commands.CommandManager)) as Torch.Commands.CommandManager;
                if (commandManager != null)
                {
                    commandManager.RegisterCommandModule(typeof(ScrapAdminCommands));
                    _commandsRegistered = true;
                    Log.Info("ScrapTrader commands registered.");
                }

                _evaluator.BuildPriceCache();
                SalvageHook.Apply();
                _stationRegistrar.RegisterAllStations();
            }

            if (state == TorchSessionState.Unloading)
                _commandsRegistered = false;
        }

        private int _updateTick = 0;
        private const int STATION_REGISTER_INTERVAL = 60 * 60 * 5;

        public override void Update()
        {
            _updateTick++;
            if (_updateTick % STATION_REGISTER_INTERVAL == 0)
                _stationRegistrar.RegisterAllStations();
        }

        public override void Dispose()
        {
            Config?.Save(Path.Combine(_storagePath, "ScrapTrader.cfg"));
            Log.Info("ScrapTrader v1.3 unloaded.");
        }

        public void SaveConfig() => Config?.Save(Path.Combine(_storagePath, "ScrapTrader.cfg"));
        public StationRegistrar GetStationRegistrar() => _stationRegistrar;
        public GridEvaluator GetEvaluator() => _evaluator;
    }
}
