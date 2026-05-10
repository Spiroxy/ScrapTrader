using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace ScrapTrader
{
    [XmlRoot("ScrapTraderConfig")]
    public class ScrapTraderConfig
    {
        // ── Economy ───────────────────────────────────────────────────────
        public float SalvageFeePercent { get; set; } = 0.30f;
        public float RepairFeePercent  { get; set; } = 0.40f;
        public long  MinimumPayout     { get; set; } = 100;

        // ── Station identification ────────────────────────────────────────
        public string GridPrefix          { get; set; } = "(NPC-ECONOMY)";
        public string SafeZonePrefix      { get; set; } = "SZ:(NPC-ECONOMY)";
        public float  SafeZoneMaxDistance { get; set; } = 2000f;
        public string ConnectorNameSpace  { get; set; } = "ScrapTrader Connector Space";
        public string ConnectorNamePlanet { get; set; } = "ScrapTrader Connector Planet";

        // ── Blacklist ─────────────────────────────────────────────────────
        public List<string> BlacklistedBlockTypes { get; set; } = new List<string>
        {
            "SafeZoneBlock",
            "StoreBlock",
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
                    return (ScrapTraderConfig)serializer.Deserialize(reader);
            }
            catch { return new ScrapTraderConfig(); }
        }

        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var serializer = new XmlSerializer(typeof(ScrapTraderConfig));
                using (var writer = new StreamWriter(path))
                    serializer.Serialize(writer, this);
            }
            catch { }
        }
    }
}
