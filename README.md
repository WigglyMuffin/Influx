# Influx

**A time-series database export plugin for Final Fantasy XIV.**

*Track your character progression, retainer ventures, and inventory with powerful database integration*

[![GitHub Release](https://img.shields.io/github/v/release/WigglyMuffin/Influx?style=for-the-badge&logo=github&color=brightgreen)](https://github.com/WigglyMuffin/Influx/releases)
[![Discord](https://img.shields.io/badge/Discord-Join%20Server-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://discord.gg/pngyvpYVt2)

---

## Features

- **Time-Series Database Support**: Export data to either InfluxDB or QuestDB
- **Character Statistics Tracking**: Monitor character progression, quests, and activities
- **Retainer Data Collection**: Track retainer ventures and inventory via AutoRetainer integration
- **Inventory Tracking**: Monitor items through AllaganTools filter integration
- **Free Company Statistics**: Collect FC credits and other company-wide metrics
- **Multi-Character Support**: Manage multiple characters across different worlds
- **Flexible Configuration**: Per-character settings with auto-enrollment options

## Installation

Add the following URL to your Dalamud plugin repositories:

`https://github.com/WigglyMuffin/DalamudPlugins/raw/main/pluginmaster.json`

**Installation Steps:**
1. Open XIVLauncher/Dalamud
2. Go to Settings → Experimental
3. Add the repository URL above
4. Go to Plugin Installer
5. Search for "Influx" and install

## Required Dependencies

Influx requires the following to function properly:

### Core Dependencies
- **[AutoRetainer](https://github.com/PunishXIV/AutoRetainer)** - For retainer-related functionality

### Recommended Plugins
- **[AllaganTools](https://github.com/Critical-Impact/AllaganTools)** - Required for inventory filter tracking features

## Database Setup

Influx supports two time-series databases:

### InfluxDB
[InfluxDB](https://www.influxdata.com/) is a popular time-series database with cloud and self-hosted options.

**Setup:**
1. Create an InfluxDB account or self-host an instance
2. Create a bucket for your FFXIV data
3. Generate an API token with write access
4. Configure in plugin settings

### QuestDB
[QuestDB](https://questdb.io/) is a high-performance time-series database optimized for time-series analytics.

**Setup:**
1. Install QuestDB locally or use cloud hosting
2. Configure authentication (if enabled)
3. Set table prefix for organized data storage
4. Configure in plugin settings

## Configuration

Open the plugin configuration window in-game to set up your database connection:

### Connection Settings

**Enable Server Connection** - Toggle data export on/off

#### For InfluxDB:
- **Server URL**: Your InfluxDB instance URL (e.g., `https://us-east-1-1.aws.cloud2.influxdata.com`)
- **Token**: Authentication token from InfluxDB
- **Organization**: Your organization name
- **Bucket**: Target bucket for data storage

**Test Connection** - Verify your connection settings

#### For QuestDB:
- **Server URL**: Your QuestDB instance URL (e.g., `http://localhost:9000`)
- **Username**: Database username
- **Password**: Database password
- **Table Prefix**: Optional prefix for table names (e.g., `a_` creates `a_quests`, `a_retainer`)

### Character Management

**Auto-enroll characters on login** - Automatically track new characters when you log in

**Include/Exclude specific characters:**
1. Log in to a character
2. Open Influx configuration → Included Characters tab
3. Click "Include current character" or "Remove inclusion"
4. Configure per-character settings:
   - **Include Free Company statistics** - Track FC credit data

**View all tracked characters** - Organized by world with character content IDs

### Inventory Filters

Track specific item categories using AllaganTools search filters:

1. Create filters in AllaganTools
2. Open Influx configuration → Inventory Filters tab
3. Select a filter from the dropdown
4. Click "Track Filter" to add it
5. Remove filters by clicking the X icon

**Refresh Filters** - Reload available filters from AllaganTools

## Database Schema

The plugin exports data to the following tables (with optional prefix):

- **`quests`** - Quest completion tracking with timestamps
- **`retainer`** - Retainer venture and inventory data
- **`fc_stats`** - Free Company credits and statistics
- **`inventory`** - Character and retainer inventory snapshots based on AllaganTools filters

All tables include character identification (content ID) and timestamps for time-series analysis.

## Support & Community

- **Discord**: Join our community for support, updates, and discussions: [https://discord.gg/pngyvpYVt2](https://discord.gg/pngyvpYVt2)
- **Bug Reports**: Use [GitHub Issues](https://github.com/WigglyMuffin/Influx/issues) for bug reports
- **Feature Requests**: Submit suggestions via GitHub Issues

## Disclaimer

**Use at your own risk.** This plugin exports your FFXIV game data to external databases. Ensure you trust your database hosting provider and secure your credentials appropriately.

## License

This project is licensed under the GNU Affero General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

## Credits

- **Original Author**: Liza Carvelli
- **Current Maintainer**: WigglyMuffin