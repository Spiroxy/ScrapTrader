using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace ScrapTrader
{
    [XmlRoot("TradeableBlocks")]
    public class TradeableBlocksConfig
    {
        [XmlElement("Block")]
        public List<TradeableBlock> Blocks { get; set; } = new List<TradeableBlock>();

        private Dictionary<string, TradeableBlock> _lookup;

        public void BuildLookup()
        {
            _lookup = new Dictionary<string, TradeableBlock>();
            foreach (var b in Blocks)
                if (!_lookup.ContainsKey(b.Type))
                    _lookup[b.Type] = b;
        }

        public bool TryGet(string subtype, out TradeableBlock block)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(subtype, out block);
        }

        public bool IsWhitelisted(string subtype) => TryGet(subtype, out _);

        public static TradeableBlocksConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                var defaults = CreateDefaults();
                defaults.Save(path);
                return defaults;
            }
            try
            {
                var serializer = new XmlSerializer(typeof(TradeableBlocksConfig));
                using (var reader = new StreamReader(path))
                {
                    var cfg = (TradeableBlocksConfig)serializer.Deserialize(reader);
                    cfg.BuildLookup();
                    return cfg;
                }
            }
            catch { return CreateDefaults(); }
        }

        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var serializer = new XmlSerializer(typeof(TradeableBlocksConfig));
                using (var writer = new StreamWriter(path))
                    serializer.Serialize(writer, this);
            }
            catch { }
        }

        private static TradeableBlocksConfig CreateDefaults()
        {
            var cfg = new TradeableBlocksConfig
            {
                Blocks = new List<TradeableBlock>
                {
                    new TradeableBlock { Type = "LargeAssembler",                          SellMultiplier = 1.4f, Prefab = "ScrapTrader_Assembler_20",              MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "LargeAssemblerIndustrial",                SellMultiplier = 1.6f, Prefab = "ScrapTrader_IndustrialAssembler_20",     MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "LargeBlockDrillReskin",                   SellMultiplier = 1.3f, Prefab = "Drill type II lg",                       MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "SmallBlockDrillReskin",                   SellMultiplier = 1.3f, Prefab = "Drill type II sg",                       MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "LargeShipGrinderReskin",                  SellMultiplier = 1.3f, Prefab = "Grinder Type II lg",                     MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "SmallShipGrinderReskin",                  SellMultiplier = 1.3f, Prefab = "Grinder Type II sg",                     MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "LargeJumpDriveReskin",                    SellMultiplier = 1.8f, Prefab = "Jumpdrive Type II",                      MaxStock = 1, StationType = "Space" },
                    new TradeableBlock { Type = "LargeBlockOxygenTankLab",                 SellMultiplier = 1.3f, Prefab = "Lab O2 Tank lg",                         MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "OxygenTankSmall",                         SellMultiplier = 1.2f, Prefab = "O2 Tank sg",                             MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "LargeBlockLargeAtmosphericThrust",        SellMultiplier = 1.4f, Prefab = "Large Atmos lg",                         MaxStock = 1, StationType = "Planet" },
                    new TradeableBlock { Type = "SmallBlockLargeAtmosphericThrust",        SellMultiplier = 1.4f, Prefab = "Large Atmos sg",                         MaxStock = 1, StationType = "Planet" },
                    new TradeableBlock { Type = "LargeBlockSmallAtmosphericThrust",        SellMultiplier = 1.4f, Prefab = "Small Atmos lg",                         MaxStock = 1, StationType = "Planet" },
                    new TradeableBlock { Type = "SmallBlockSmallAtmosphericThrust",        SellMultiplier = 1.4f, Prefab = "Small Atmos sg",                         MaxStock = 1, StationType = "Planet" },
                    new TradeableBlock { Type = "LargeBlockLargeIndustrialContainer",      SellMultiplier = 1.3f, Prefab = "Large Cargo lg",                         MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "SmallBlockLargeContainer",                SellMultiplier = 1.3f, Prefab = "Large Cargo sg",                         MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "LargeBlockSmallContainer",                SellMultiplier = 1.2f, Prefab = "Small Cargo lg",                         MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "LargeBlockLargeGeneratorWarfare2",        SellMultiplier = 1.6f, Prefab = "Warfare Reactor lg",                     MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "LargeBlockSmallGeneratorWarfare2",        SellMultiplier = 1.5f, Prefab = "Warfare Reactor sg",                     MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "LargeHydrogenTank",                       SellMultiplier = 1.3f, Prefab = "Large H2 Tank lg",                       MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "SmallHydrogenTank",                       SellMultiplier = 1.3f, Prefab = "Large H2 Tank sg",                       MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "SmallBlockLargeHydrogenThrustIndustrial", SellMultiplier = 1.4f, Prefab = "Large H2 Thruster sg",                   MaxStock = 1, StationType = "Space" },
                    new TradeableBlock { Type = "LargeBlockSmallHydrogenThrustIndustrial", SellMultiplier = 1.4f, Prefab = "Small H2 Thruster lg",                   MaxStock = 1, StationType = "Space" },
                    new TradeableBlock { Type = "SmallBlockSmallHydrogenThrustIndustrial", SellMultiplier = 1.4f, Prefab = "Small H2 Thruster sg",                   MaxStock = 1, StationType = "Space" },
                    new TradeableBlock { Type = "LgParachute",                             SellMultiplier = 1.2f, Prefab = "Parachute Hatch lg",                     MaxStock = 1, StationType = "Planet" },
                    new TradeableBlock { Type = "SmParachute",                             SellMultiplier = 1.2f, Prefab = "Parachute Hatch sg",                     MaxStock = 1, StationType = "Planet" },
                    new TradeableBlock { Type = "LargeProjector",                          SellMultiplier = 1.3f, Prefab = "Projector lg",                           MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "SmallProjector",                          SellMultiplier = 1.3f, Prefab = "Projector sg",                           MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "SurvivalKitLargeReskin",                  SellMultiplier = 1.3f, Prefab = "Survival kit II lg",                     MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "SurvivalKitSmallReskin",                  SellMultiplier = 1.3f, Prefab = "Survival kit II sg",                     MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "LargeShipWelderReskin",                   SellMultiplier = 1.3f, Prefab = "Welder Type II lg",                      MaxStock = 1, StationType = "Any" },
                    new TradeableBlock { Type = "SmallShipWelderReskin",                   SellMultiplier = 1.3f, Prefab = "Welder Type II sg",                      MaxStock = 1, StationType = "Any" },
                }
            };
            cfg.BuildLookup();
            return cfg;
        }
    }

    public class TradeableBlock
    {
        [XmlAttribute("type")]
        public string Type { get; set; } = "";

        [XmlAttribute("sellMultiplier")]
        public float SellMultiplier { get; set; } = 1.3f;

        [XmlAttribute("prefab")]
        public string Prefab { get; set; } = "";

        [XmlAttribute("maxStock")]
        public int MaxStock { get; set; } = 1;

        /// <summary>Any / Space / Planet</summary>
        [XmlAttribute("stationType")]
        public string StationType { get; set; } = "Any";

        public bool HasPrefab => !string.IsNullOrEmpty(Prefab);
    }
}
