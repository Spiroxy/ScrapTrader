using System;
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
        public InventoryManager Inventory { get; private set; }
        public TradeableBlocksConfig TradeableBlocks { get; private set; }

        private LcdManager _lcdManager;
        private BuyManager _buyManager;
        private GridEvaluator _evaluator;
        private StationRegistrar _stationRegistrar;
        private bool _commandsRegistered = false;
        private string _worldStoragePath;

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;

            _worldStoragePath = Path.Combine(StoragePath, "Saves", "SpiroGames-ScrapBound", "Storage", "ScrapTrader");
            Directory.CreateDirectory(_worldStoragePath);

            Log.Info($"ScrapTrader v1.2 storage path: {_worldStoragePath}");

            var configPath = Path.Combine(_worldStoragePath, "ScrapTrader.cfg");
            Config = ScrapTraderConfig.Load(configPath);

            var tradeablePath = Path.Combine(_worldStoragePath, "TradeableBlocks.xml");
            TradeableBlocks = TradeableBlocksConfig.Load(tradeablePath);

            Inventory = new InventoryManager(_worldStoragePath);

            _evaluator = new GridEvaluator(Config);
            _lcdManager = new LcdManager(Config, Inventory);
            _buyManager = new BuyManager(Config, Inventory, TradeableBlocks);
            _stationRegistrar = new StationRegistrar(Config);

            Log.Info("ScrapTrader v1.3 plugin loaded.");

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
                    commandManager.RegisterCommandModule(typeof(ScrapCommands));
                    commandManager.RegisterCommandModule(typeof(ScrapAdminCommands));
                    _commandsRegistered = true;
                    Log.Info("ScrapTrader commands registered.");
                }

                _evaluator.BuildPriceCache();
                Inventory.RestockFromConfig(TradeableBlocks, _evaluator);
                _lcdManager.ForceUpdate();
                SalvageHook.Apply();
                _updateTick = 0;
            }

            if (state == TorchSessionState.Unloading)
                _commandsRegistered = false;
        }

        public BuyManager GetBuyManager() => _buyManager;
        public LcdManager GetLcdManager() => _lcdManager;
        public GridEvaluator GetEvaluator() => _evaluator;
        public StationRegistrar GetStationRegistrar() => _stationRegistrar;

        private int _updateTick = 0;
        private const int STATION_REGISTER_INTERVAL = 60 * 60 * 5; // ~5 minutes at 60 ticks/sec

        public override void Update()
        {
            _lcdManager?.Update();
            _updateTick++;

            // Register stations periodically
            if (_updateTick % STATION_REGISTER_INTERVAL == 0)
            {
                _stationRegistrar.RegisterAllStations();
            }
        }

        public override void Dispose()
        {
            Config?.Save(Path.Combine(_worldStoragePath, "ScrapTrader.cfg"));
            Log.Info("ScrapTrader v1.3 plugin unloaded.");
        }

        public void SaveConfig() => Config?.Save(Path.Combine(_worldStoragePath, "ScrapTrader.cfg"));
    }
}
