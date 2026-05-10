using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using NLog;

namespace ScrapTrader
{
    public class InventoryManager
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly string _inventoryPath;
        private readonly object _lock = new object();
        private ScrapInventory _inventory;
        private int _nextId = 1;

        // Random items shown on LCD and !scrap list – refreshed every cycle
        private List<InventoryItem> _currentDisplay = new List<InventoryItem>();
        private int _displayCount = 5;
        private readonly Random _rng = new Random();

        public InventoryManager(string storagePath)
        {
            _inventoryPath = Path.Combine(storagePath, "Inventory.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(_inventoryPath));
            Load();
        }

        // ── Restocking ────────────────────────────────────────────────────

        /// <summary>
        /// Called at server startup. Fills inventory up to maxStock for each
        /// block type defined in TradeableBlocks.xml.
        /// </summary>
        public void RestockFromConfig(TradeableBlocksConfig tradeableBlocks, GridEvaluator evaluator)
        {
            int added = 0;
            foreach (var block in tradeableBlocks.Blocks)
            {
                if (!block.HasPrefab) continue;

                // Count how many of this type already exist
                int existing;
                lock (_lock)
                    existing = _inventory.Items.Count(i => i.BlockSubtype == block.Type);

                int toAdd = block.MaxStock - existing;
                if (toAdd <= 0) continue;

                // Calculate sell price from component value
                float componentValue = evaluator.GetComponentValueBySubtype(block.Type);
                long sellPrice = (long)(componentValue * block.SellMultiplier);
                if (sellPrice <= 0) sellPrice = 1000; // fallback

                for (int i = 0; i < toAdd; i++)
                {
                    AddItem(block.Type, block.Prefab, 0.20f, 0, sellPrice, block.StationType);
                    added++;
                }
            }

            Log.Info($"ScrapTrader: Restocked {added} item(s) into inventory.");
            RefreshDisplay();
        }

        /// <summary>Refresh the random display selection.</summary>
        public void RefreshDisplay()
        {
            lock (_lock)
            {
                var all = new List<InventoryItem>(_inventory.Items);
                // Shuffle
                for (int i = all.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    var tmp = all[i]; all[i] = all[j]; all[j] = tmp;
                }
                _currentDisplay = all.Take(_displayCount).ToList();
            }
            Log.Info($"ScrapTrader: Display refreshed with {_currentDisplay.Count} random item(s).");
        }

        public List<InventoryItem> GetDisplayItems() => new List<InventoryItem>(_currentDisplay);

        // ── Public API ────────────────────────────────────────────────────

        public int AddItem(string blockSubtype, string blockDisplayName, float condition,
                           long buyPrice, long sellPrice, string stationType = "Any")
        {
            lock (_lock)
            {
                var item = new InventoryItem
                {
                    Id = _nextId++,
                    BlockSubtype = blockSubtype,
                    BlockDisplayName = blockDisplayName,
                    Condition = condition,
                    BuyPrice = buyPrice,
                    SellPrice = sellPrice,
                    StationType = stationType,
                    AddedAt = DateTime.UtcNow
                };
                _inventory.Items.Add(item);
                Save();
                return item.Id;
            }
        }

        public bool TryRemoveItem(int id, out InventoryItem item)
        {
            lock (_lock)
            {
                item = _inventory.Items.Find(i => i.Id == id);
                if (item == null) return false;
                _inventory.Items.Remove(item);
                // Remove from display if present
                _currentDisplay.RemoveAll(i => i.Id == id);
                Save();
                return true;
            }
        }

        public List<InventoryItem> GetAllItems()
        {
            lock (_lock)
                return new List<InventoryItem>(_inventory.Items);
        }

        public InventoryItem GetItem(int id)
        {
            lock (_lock)
                return _inventory.Items.Find(i => i.Id == id);
        }

        public int Count
        {
            get { lock (_lock) return _inventory.Items.Count; }
        }

        // ── Persistence ───────────────────────────────────────────────────

        private void Load()
        {
            if (!File.Exists(_inventoryPath))
            {
                _inventory = new ScrapInventory();
                return;
            }
            try
            {
                var serializer = new XmlSerializer(typeof(ScrapInventory));
                using (var reader = new StreamReader(_inventoryPath))
                    _inventory = (ScrapInventory)serializer.Deserialize(reader);

                foreach (var item in _inventory.Items)
                    if (item.Id >= _nextId)
                        _nextId = item.Id + 1;

                Log.Info($"ScrapTrader: Inventory loaded – {_inventory.Items.Count} items.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ScrapTrader: Failed to load inventory.");
                _inventory = new ScrapInventory();
            }
        }

        private void Save()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(ScrapInventory));
                using (var writer = new StreamWriter(_inventoryPath))
                    serializer.Serialize(writer, _inventory);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ScrapTrader: Failed to save inventory.");
            }
        }
    }

    [XmlRoot("ScrapInventory")]
    public class ScrapInventory
    {
        [XmlElement("Item")]
        public List<InventoryItem> Items { get; set; } = new List<InventoryItem>();
    }

    public class InventoryItem
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlAttribute("blockSubtype")]
        public string BlockSubtype { get; set; } = "";

        [XmlAttribute("blockDisplayName")]
        public string BlockDisplayName { get; set; } = "";

        [XmlAttribute("condition")]
        public float Condition { get; set; }

        [XmlAttribute("buyPrice")]
        public long BuyPrice { get; set; }

        [XmlAttribute("sellPrice")]
        public long SellPrice { get; set; }

        [XmlAttribute("stationType")]
        public string StationType { get; set; } = "Any";

        [XmlAttribute("addedAt")]
        public DateTime AddedAt { get; set; }
    }
}
