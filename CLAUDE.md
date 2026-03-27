# ApexBreeze - SimHub to Wahoo Headwind Fan Controller

## Project Goal

Build a SimHub plugin that lets me control my Wahoo KICKR Headwind
fan speed using buttons on my SimMagic GT Neo wheel during iRacing
sessions. No phone app, no separate software — just turn the wheel
button and the fan responds.

## Hardware

- Wahoo KICKR Headwind fan (BLE device)
- SimMagic Alpha Evo Sport wheel base
- SimMagic GT Neo steering wheel (has encoders/buttons mapped in SimHub)
- Windows PC running SimHub and iRacing

## Input Model

The GT Neo wheel buttons will act as increment/decrement. SimHub
will track a fan speed state variable (0-100, stepping by ~10).
This is NOT a continuous analog input — it is button-driven state
that we maintain internally.

## Confirmed BLE Protocol (reverse engineered, verified)

- **Service UUID:** `a026ee0c-0a7d-4ab3-97fa-f1500f9feb8b`
- **Characteristic UUID:** `a026e038-0a7d-4ab3-97fa-f1500f9feb8b`
- **Packet size:** 4 bytes, write without response
- **Enter Manual mode:** `[0x04, 0x04, 0x00, 0x00]`
- **Set speed:** `[0x02, speed, 0x00, 0x00]` where speed = 0-100
- You MUST send Manual mode first before speed will be accepted
- The Headwind advertises by name "HEADWIND" — service UUID is NOT
  in the advertisement packet so you must connect first then discover
- State notifications are cyclic not true notify — ignore for our
  use case, we are write-only

## Architecture

`[GT Neo Buttons] → [SimHub Custom Property] → [C# Plugin] → [Windows BLE API] → [Headwind]`

ApexBreeze/
├── poc/
│ └── headwind_ble.py
├── ApexBreezePlugin/
│ ├── ApexBreezePlugin.cs
│ ├── ApexBreezePlugin.csproj
│ └── Properties/
├── README.md
└── .gitignore

## Build Phases

### Phase 1 - Python PoC (prove BLE control works)

File: `poc/headwind_ble.py`

- Use the `bleak` library
- Scan for device advertising name starting with "HEADWIND"
- Connect and send Manual mode command
- Accept speed value from keyboard input (0-100)
- Send speed command and confirm fan responds
- Test at 0, 50, 100
- Goal: type a number, fan changes. Nothing else.

### Phase 2 - SimHub Input Wiring

- No code needed here — this is SimHub UI config
- Map GT Neo encoder up = increment speed variable by 10
- Map GT Neo encoder down = decrement speed variable by 10
- Clamp to 0-100
- Expose as SimHub custom property: `ApexBreeze.FanSpeed`

### Phase 3 - C# SimHub Plugin

Project: `ApexBreezePlugin`

- Target SimHub plugin SDK (reference SimHub DLLs)
- Implement `IPlugin`, `IDataPlugin` interfaces
- On `Init`: connect to Headwind via `Windows.Devices.Bluetooth`
  - Scan by name "HEADWIND"
  - Connect, discover service, get characteristic reference
  - Send initial Manual mode command
  - Handle connection failure gracefully (log, retry on next tick)
- On `DataUpdate` tick:
  - Read `ApexBreeze.FanSpeed` custom property
  - Compare to last sent value
  - Only write to BLE if value has changed (debounce — do NOT
    blast BLE every frame)
  - Write `[0x02, speed, 0x00, 0x00]` to characteristic
- Reconnect logic: if BLE drops, attempt reconnect every 5 seconds
- Build output: `ApexBreezePlugin.dll` → drop into SimHub plugins folder

## Key Constraints

- Write without response (not write with response) on the characteristic
- Must enter Manual mode `[0x04, 0x04, 0x00, 0x00]` before speed
  commands work — do this on connect, not on every speed update
- Speed range: 0-100 as a single byte
- Do not read/subscribe to notifications — write only is sufficient

## Start With

Phase 1 Python PoC. Get the fan responding to keyboard input
before touching SimHub or C#. This validates the BLE layer first.
