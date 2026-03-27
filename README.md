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
[GT Neo Buttons] → [SimHub Custom Property] → [C# Plugin] → [Windows BLE API] → [Headwind Fan]
```

| Component | Location | Purpose |
|-----------|----------|---------|
| Python PoC | `poc/headwind_ble.py` | Validate BLE protocol before building the plugin |
| SimHub Plugin | `ApexBreezePlugin/` | C# plugin that reads SimHub property and writes to fan via BLE |

## Project Structure

```
ApexBreeze/
├── poc/
│   └── headwind_ble.py          # Phase 1: BLE proof of concept
├── ApexBreezePlugin/
│   ├── ApexBreezePlugin.cs      # Phase 3: SimHub plugin
│   ├── ApexBreezePlugin.csproj
│   └── Properties/
├── CLAUDE.md
├── README.md
└── .gitignore
```

## Build Phases

### Phase 1 — Python PoC (BLE Validation)

Prove the BLE control protocol works using Python and the `bleak` library. Type a speed value, fan changes. Nothing else.

**Status:** Complete

### Phase 2 — SimHub Input Wiring

Configure SimHub to map GT Neo encoder buttons to a custom property (`ApexBreeze.FanSpeed`) that increments/decrements in steps of 10, clamped to 0–100. This is UI configuration only — no code.

**Status:** Not started

### Phase 3 — C# SimHub Plugin

Build the SimHub plugin that reads `ApexBreeze.FanSpeed` on each data tick, connects to the Headwind over BLE, and writes speed commands. Includes reconnect logic and debouncing (only writes when the value changes).

**Status:** Not started

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

### Phase 1 — Running the PoC

```bash
cd poc
pip install bleak
python headwind_ble.py
```

**Before running:**

- Headwind fan must be powered on (light visible on the unit)
- Close the Wahoo mobile app — the Headwind only supports one BLE connection at a time and will be invisible to scans if the app has it
- Do **not** pair the Headwind through Windows Bluetooth settings (Settings > Bluetooth & devices). It will fail — this is a known Windows issue with the Headwind and is not a blocker. `bleak` bypasses Windows pairing entirely and talks to the BLE stack directly

### Phase 3 — Building the Plugin

Build `ApexBreezePlugin.dll` and copy it to your SimHub plugins directory. Details will be added once Phase 3 development begins.

## License

Private project — not currently licensed for redistribution.
