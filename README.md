# ApexBreeze

Control your Wahoo KICKR Headwind fan directly from your SimMagic GT Neo steering wheel during iRacing sessions. No phone app, no separate software — just press a button and the fan responds.

## What It Does

ApexBreeze is a SimHub plugin that bridges your sim racing wheel buttons to the Wahoo KICKR Headwind fan over Bluetooth Low Energy (BLE). Press an encoder on your GT Neo wheel to step the fan speed up or down (0–100% in increments of 10).

## Hardware

- **Fan:** Wahoo KICKR Headwind (BLE)
- **Wheel Base:** SimMagic Alpha Evo Sport
- **Steering Wheel:** SimMagic GT Neo (encoders/buttons mapped via SimHub)
- **PC:** Windows with Bluetooth, running SimHub and iRacing

## Architecture

```
[GT Neo Buttons] → [SimHub Action] → [ApexBreeze Plugin] → [Windows BLE API] → [Headwind Fan]
```

The plugin owns all state internally. Wheel buttons trigger SimHub actions (`FanSpeedUp` / `FanSpeedDown`) which the plugin handles directly — no separate custom property wiring needed.

| Component | Location | Purpose |
|-----------|----------|---------|
| Python PoC | `poc/headwind_ble.py` | Validate BLE protocol before building the plugin |
| SimHub Plugin | `ApexBreezePlugin/` | C# plugin with built-in button handling and BLE control |

## Project Structure

```
ApexBreeze/
├── poc/
│   ├── headwind_ble.py          # Phase 1: BLE proof of concept
│   └── requirements.txt
├── ApexBreezePlugin/
│   ├── ApexBreezePlugin.cs      # SimHub plugin (BLE + input handling)
│   ├── ApexBreezePlugin.csproj
│   └── Properties/
│       └── AssemblyInfo.cs
├── CLAUDE.md
├── README.md
└── .gitignore
```

## Build Phases

### Phase 1 — Python PoC (BLE Validation)

Prove the BLE control protocol works using Python and the `bleak` library.

**Status:** Complete

### Phase 2+3 — SimHub Plugin (Input Wiring + BLE Control)

Originally Phase 2 (SimHub UI config) and Phase 3 (C# plugin) were separate. These were merged — the plugin handles both button input and BLE control internally.

The plugin:
- Registers SimHub actions: `FanSpeedUp`, `FanSpeedDown`, `FanOff`, `FanMax`
- Maintains speed state internally (0–100, step 10)
- Exposes `ApexBreeze.FanSpeed` and `ApexBreeze.Connected` as SimHub properties
- Connects to the Headwind over BLE, subscribes to notifications (CCCD), enters manual mode
- Writes speed commands only when the value changes (debounced)
- Auto-reconnects every 5 seconds if BLE drops
- Turns fan off on plugin shutdown

**Status:** Built, ready for testing

## BLE Protocol Summary

| Command | Bytes | Notes |
|---------|-------|-------|
| Enter Manual Mode | `04 04 00 00` | Must send before speed commands work |
| Set Speed | `02 XX 00 00` | `XX` = 0–100 |

- **Service UUID:** `a026ee0c-0a7d-4ab3-97fa-f1500f9feb8b`
- **Characteristic UUID:** `a026e038-0a7d-4ab3-97fa-f1500f9feb8b`
- Write without response
- You **must** subscribe to notifications (enable CCCD) on the characteristic before the fan will accept writes — even though we don't use the notification data
- Required sequence: subscribe to notifications → send manual mode → send speed commands
- Device advertises as "HEADWIND" (service UUID not in advertisement)

## Getting Started

### Running the PoC

```bash
cd poc
pip install -r requirements.txt
python headwind_ble.py
```

**Before running (PoC or plugin):**

- Headwind fan must be powered on (light visible on the unit)
- Close the Wahoo mobile app — the Headwind only supports one BLE connection at a time and will be invisible to scans if the app has it
- Do **not** pair the Headwind through Windows Bluetooth settings (Settings > Bluetooth & devices). It will fail — this is a known Windows issue with the Headwind and is not a blocker

### Building the Plugin

```bash
cd ApexBreezePlugin
dotnet build -c Release
```

The build automatically copies `ApexBreezePlugin.dll` to the SimHub install directory. Restart SimHub to load the plugin.

### SimHub Configuration

After the plugin loads in SimHub:

1. Go to **Controls and Events**
2. Map your GT Neo encoder up button to the **ApexBreeze > FanSpeedUp** action
3. Map your GT Neo encoder down button to the **ApexBreeze > FanSpeedDown** action
4. (Optional) Map buttons to **FanOff** and **FanMax** for quick access

The `ApexBreeze.FanSpeed` property is available for use in SimHub dashboards to display the current fan speed.

## License

Private project — not currently licensed for redistribution.
