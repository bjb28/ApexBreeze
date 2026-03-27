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

**Status:** Not started

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
- Write without response — do not subscribe to notifications
- Device advertises as "HEADWIND" (service UUID not in advertisement)

## Getting Started

### Phase 1 — Running the PoC

```bash
cd poc
pip install bleak
python headwind_ble.py
```

Make sure your Headwind fan is powered on and Bluetooth is enabled on your PC.

### Phase 3 — Building the Plugin

Build `ApexBreezePlugin.dll` and copy it to your SimHub plugins directory. Details will be added once Phase 3 development begins.

## License

Private project — not currently licensed for redistribution.
