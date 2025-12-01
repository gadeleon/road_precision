# Road Precision
![](Road%20Precision\Properties\Thumbnail.png)

My first attempt at a Cities: Skylines II mod. It displays precise decimal values for road building tooltips, replacing the default rounded integer values where it can or adding new tooltips for ones I could not patch/replace.

This is not how I would have preferred to have done this (simply modifying the UI) but instead takes the distance/angle calculations and converts them to strings before the game UI itself has a chance to round-down the calculations.

## Compatibility
Not compatible with ExtendedTooltips (for now). We are both patching the same code and I have been unable to resolve the conflicts.
## Features

### Precise Measurements
- **Distance/Length**: Shows more precise measurements with configurable decimal places (e.g., "12.34m" instead of "12m")
- **Slope/Grade**: Displays slope percentages (e.g., "4.23%" instead of "4%")
- **Angles**: Shows more accurate angles when placing curved roads and connecting to existing roads (e.g., "89.73°" instead of "90°")

### Connection Angles
- Displays precise angles when connecting new roads to existing roads
- Shows both supplementary angles at each connection point
- Works with both edge connections and node/intersection connections
- Should handle multiple connected roads at corners

### [P] Tooltip
- All precise tooltips are prefixed with `[P]` to distinguish them from vanilla tooltips
- Precise tooltips appear alongside vanilla tooltips for easy comparison (and because my attempts at replacing the tooltips failed gloriously).

## Settings

Access mod settings from the game's Options menu:

- **Distance Decimal Places** (0-4): Number of decimal places for distance measurements
- **Angle Decimal Places** (0-4): Number of decimal places for angle measurements
- **Enable Float Distance**: Toggle precise distance display on/off
- **Enable Float Angle**: Toggle precise angle display on/off

Setting decimal places to 0 is equivalent to disabling the feature.

## Installation

1. Subscribe to the mod on Paradox Mods or manually install
2. Enable the mod in the game's mod manager
3. Restart the game
4. Configure settings in Options > Mod Settings > Road Precision

## Technical Details

I got this to work by:
- Patching the vanilla `NetCourseTooltipSystem` and `GuideLineTooltipSystem` to disable their default behavior
- Registering custom tooltip systems that calculate and display precise values
- Accessing `NetToolSystem` control points to calculate angles directly from road geometry
- Using the Entity Component System (ECS) to query road edge and node data for connection angles

## License

MIT License - See LICENSE file for details

## Contributing

Bug reports and feature requests are welcome, just be courteous! Please include:
- Game version
- Mod version
- Steps to reproduce the issue
- Screenshots if applicable

## Thanks To:

Developed using:
- Cities: Skylines II Modding Tools
- HarmonyX for runtime patching
- dnSpy for game code analysis

## Known Issues:
- The continuous curve tool may not always show precise tooltips.