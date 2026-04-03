using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace ScrapTrader
{
    [XmlRoot("ScrapTraderConfig")]
    public class ScrapTraderConfig
    {
        // ── Economy ───────────────────────────────────────────────────────
        public float ConditionMultiplier { get; set; } = 1.0f;
        public float GlobalPriceMultiplier { get; set; } = 0.7f;
        public long MinimumPayout { get; set; } = 100;

        // ── Station ───────────────────────────────────────────────────────
        public float InteractionRange { get; set; } = 50f;
        public string ConnectorNameSpace { get; set; } = "ScrapTrader Connector Space";
        public string ConnectorNamePlanet { get; set; } = "ScrapTrader Connector Planet";
        public string LcdInventoryName { get; set; } = "ScrapTrader Inventory";
        public int LcdUpdateIntervalSeconds { get; set; } = 900; // 15 minutes
        public int DisplayItemCount { get; set; } = 5;
        public float SpawnDistanceFromConnector { get; set; } = 15f;

        // ── Access control ────────────────────────────────────────────────
        public List<string> AllowedStationFactions { get; set; } = new List<string>
        {
            "Military",
            "The Ferryman",
            "Prime Broker",
        };

        public List<string> BlacklistedBlockTypes { get; set; } = new List<string>
        {
            "SafeZoneBlock",
            "StoreBlock",
        };

        [XmlIgnore]
        public Dictionary<string, float> FallbackPrices { get; set; } = new Dictionary<string, float>
        {
            { "SteelPlate",         8f   },
            { "InteriorPlate",      4f   },
            { "SmallTube",          5f   },
            { "LargeTube",         15f   },
            { "MetalGrid",         12f   },
            { "Motor",             40f   },
            { "Computer",          20f   },
            { "Construction",       6f   },
            { "Display",           15f   },
            { "Detector",          60f   },
            { "Explosives",        50f   },
            { "Girder",             5f   },
            { "GravityGenerator", 300f   },
            { "Medical",          250f   },
            { "RadioCommunication",70f   },
            { "Reactor",          200f   },
            { "SolarCell",         60f   },
            { "SuperConductor",   150f   },
            { "Thrust",            80f   },
            { "PowerCell",         30f   },
            { "BulletproofGlass",  10f   },
            { "ZoneChip",        1000f   },
        };

        public static ScrapTraderConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                var cfg = new ScrapTraderConfig();
                cfg.Save(path);
                return cfg;
            }
            try
            {
                var serializer = new XmlSerializer(typeof(ScrapTraderConfig));
                using (var reader = new StreamReader(path))
                {
                    var cfg = (ScrapTraderConfig)serializer.Deserialize(reader);
                    cfg.FallbackPrices = new ScrapTraderConfig().FallbackPrices;
                    return cfg;
                }
            }
            catch { return new ScrapTraderConfig(); }
        }

        public void Save(string path)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(ScrapTraderConfig));
                using (var writer = new StreamWriter(path))
                    serializer.Serialize(writer, this);
            }
            catch { }
        }
    }
}
