# ScrapTrader

A Torch server-side economy plugin for Space Engineers that turns NPC stations into dynamic scrap markets.

Players can sell their ships to the station for Space Credits based on block condition and component value. The station maintains a restocking inventory of salvaged blocks available for purchase, refreshed on every server restart.

---

## Requirements

- [Torch Server](https://torchapi.com/)
- ScrapTrader Blocks mod on Steam Workshop (required for prefab spawning)

---

## Installation

1. Download the latest release zip
2. Place the zip in your Torch `Plugins` folder
3. Add the plugin GUID to `torch.cfg`:
   ```xml
   <Plugins>
     <guid>b7e4f2a1-3c8d-4e90-a5b6-d1234567890f</guid>
   </Plugins>
   ```
4. Add the ScrapTrader Blocks mod to your world
5. Start Torch – config files are generated automatically in:
   ```
   Instance\Saves\<WorldName>\Storage\ScrapTrader\
   ```

---

## Station Setup

Build an NPC station owned by an allowed faction (configured in `ScrapTrader.cfg`) and add:

- A connector named **`ScrapTrader Connector Planet`** for planet stations
- A connector named **`ScrapTrader Connector Space`** for space stations
- An LCD panel named **`ScrapTrader Inventory`** to display available items

---

## Configuration

### ScrapTrader.cfg
| Setting | Default | Description |
|---|---|---|
| `GlobalPriceMultiplier` | 0.7 | Station cut on purchases (0.7 = 30% cut) |
| `InteractionRange` | 50 | Max distance to buy (meters) |
| `SpawnDistanceFromConnector` | 15 | Spawn distance for purchased items (meters) |
| `LcdUpdateIntervalSeconds` | 900 | LCD refresh interval (seconds) |
| `DisplayItemCount` | 5 | Number of random items shown per cycle |
| `AllowedStationFactions` | Military, The Ferryman, Prime Broker | Factions that can own ScrapTrader stations |

### TradeableBlocks.xml
Defines which blocks are available in the station inventory.

```xml
<Block type="LargeAssembler"
       sellMultiplier="1.4"
       prefab="ScrapTrader_Assembler_20"
       maxStock="1"
       stationType="Any"/>
```

| Attribute | Description |
|---|---|
| `type` | Block SubtypeId |
| `sellMultiplier` | Price multiplier for buyers (1.4 = 40% markup) |
| `prefab` | Prefab Subtype from the ScrapTrader Blocks mod |
| `maxStock` | Max items of this type restocked per server restart |
| `stationType` | `Any`, `Space` or `Planet` |

---

## Player Commands

| Command | Description |
|---|---|
| `!scrap sell` | Evaluate and sell your docked ship |
| `!scrap confirm` | Accept the offer |
| `!scrap cancel` | Decline the offer |
| `!scrap list` | Show available items (must be within 100m of station) |
| `!scrap buy <ID>` | Purchase an item by ID |
| `!scrap help` | Show help |

---

## Admin Commands

| Command | Description |
|---|---|
| `!scraptrader status` | Show plugin status and inventory count |
| `!scraptrader restock` | Manually trigger inventory restock |
| `!scraptrader clearinventory` | Clear all items from inventory |
| `!scraptrader updatelcd` | Force update all LCD panels |
| `!scraptrader multiplier <X>` | Set global price multiplier (0.0-1.0) |

---

## How It Works

**Selling:**
- Dock your ship to a ScrapTrader connector
- Run `!scrap sell` to get an offer based on block condition and component value
- Run `!scrap confirm` to accept – you receive Space Credits and the ship is scrapped

**Buying:**
- Stand within 100m of a ScrapTrader station
- Run `!scrap list` to see available items (filtered by station type)
- Run `!scrap buy <ID>` to purchase – the item spawns 15m in front of the connector

**Inventory:**
- Station inventory is defined in `TradeableBlocks.xml`
- Inventory restocks to `maxStock` on every server restart
- Items sold by players do not restock the inventory
- LCD panels show 5 random items, refreshed every 15 minutes

---

## License

MIT
