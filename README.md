# ScrapTrader

A server-side Torch plugin for Space Engineers that enables the Salvage and Repair functions of the new Services Terminal block on NPC trade stations.

Built to work alongside [NPC Provider (Bookburner's mod)](https://steamcommunity.com/sharedfiles/filedetails/?id=3099943209).

---

## Features

- Sell ships to NPC stations for Space Credits via the **Salvage** service
- Repair damaged ships at NPC stations via the **Repair** service
- Payout and cost calculated from vanilla component prices
- Station detection based on grid name and SafeZone proximity
- Supports planet and space station connectors separately

---

## Requirements

- [Torch Server](https://torchapi.com/)
- [NPC Provider (Bookburner's mod)](https://steamcommunity.com/sharedfiles/filedetails/?id=3099943209)

---

## Installation

1. Download the latest release from the [Releases](https://github.com/Spiroxy/ScrapTrader/releases) page
2. Extract the plugin folder into your Torch `Plugins` directory
3. Enable the plugin in Torch and restart the server

---

## Station Setup

ScrapTrader identifies valid NPC economy stations by two criteria:

**1. Grid name** must contain:
```
(NPC-ECONOMY)
```

**2. A SafeZone** named with the prefix:
```
SZ:(NPC-ECONOMY)
```
must exist within **2000m** of the station.

**3. Connectors** on the station grid must be named:
```
ScrapTrader Connector Planet
```
or
```
ScrapTrader Connector Space
```

The player interacts with the **Services Terminal** on the station. The terminal lists ships owned by the player that are within the SafeZone — dynamic grids only, not static. The player selects a ship and confirms the transaction.

---

## Commands

| Command | Description |
|---|---|
| `!scraptrader registerstations` | Manually scans and registers all valid NPC economy stations |

---

## Configuration

Configuration is stored in `ScrapTrader.cfg` in the plugin directory.

| Setting | Default | Description |
|---|---|---|
| `SalvageFeePercent` | `0.30` | Station fee deducted from salvage payout (30%) |
| `RepairFeePercent` | `0.40` | Station markup added to repair cost (40%) |

Prices are calculated from vanilla Space Engineers component values.

---

## How It Works

1. Player opens the **Services Terminal** on the station
2. The terminal lists ships owned by the player within the SafeZone — dynamic grids only, not static
3. **Salvage** — the plugin evaluates the ship's components and condition, calculates a payout minus the station fee, and pays the player in Space Credits upon confirmation
4. **Repair** — the plugin calculates the cost to restore missing components, adds the station markup, and charges the player upon confirmation

---

## Plugin GUID

```
b7e4f2a1-3c8d-4e90-a5b6-d1234567890f
```

---

## License

MIT
