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

        private ScrapBayManager _scrapBayManager;
        private LcdManager _lcdManager;
        private BuyManager _buyManager;
        private GridEvaluator _evaluator;
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
            _scrapBayManager = new ScrapBayManager(Inventory, TradeableBlocks, _evaluator);
            _scrapBayManager.Init();

            _lcdManager = new LcdManager(Config, Inventory);
            _buyManager = new BuyManager(Config, Inventory, TradeableBlocks);

            Log.Info("ScrapTrader v1.2 plugin loaded.");

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

                // Build price cache from SE definitions
                _evaluator.BuildPriceCache();

                // Restock inventory on session load
                Inventory.RestockFromConfig(TradeableBlocks, _evaluator);

                // Force LCD update
                _lcdManager.ForceUpdate();
            }

            if (state == TorchSessionState.Unloading)
                _commandsRegistered = false;
        }

        public ScrapBayManager GetManager() => _scrapBayManager;
        public BuyManager GetBuyManager() => _buyManager;
        public LcdManager GetLcdManager() => _lcdManager;
        public GridEvaluator GetEvaluator() => _evaluator;

        public override void Update()
        {
            _scrapBayManager?.Update();
            _lcdManager?.Update();
        }

        public override void Dispose()
        {
            _scrapBayManager?.Dispose();
            Config?.Save(Path.Combine(_worldStoragePath, "ScrapTrader.cfg"));
            Log.Info("ScrapTrader v1.2 plugin unloaded.");
        }

        public void SaveConfig() => Config?.Save(Path.Combine(_worldStoragePath, "ScrapTrader.cfg"));
    }
}
