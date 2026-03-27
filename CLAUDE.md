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
- State notifications are cyclic not true notify — we don't need
  the data, but you MUST subscribe to notifications (enable CCCD)
  on the characteristic before the fan will accept write commands
- Notification subscribe → Manual mode → Speed writes (this order
  is required)

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

### Phase 2+3 - SimHub Plugin (merged)

Phase 2 (SimHub UI config) was folded into Phase 3. The plugin
handles both input and BLE control internally.

Project: `ApexBreezePlugin`

- Target .NET Framework 4.8, SimHub plugin SDK
- Implement `IPlugin`, `IDataPlugin` interfaces
- On `Init`:
  - Register actions: FanSpeedUp, FanSpeedDown, FanOff, FanMax
  - Expose properties: ApexBreeze.FanSpeed, ApexBreeze.Connected
  - Connect to Headwind via `Windows.Devices.Bluetooth`
  - Subscribe to notifications (CCCD), send Manual mode command
  - Handle connection failure gracefully (log, retry every 5s)
- On `DataUpdate` tick:
  - Compare current speed to last sent value
  - Only write to BLE if value has changed (debounce)
  - Write `[0x02, speed, 0x00, 0x00]` to characteristic
  - Attempt reconnect if disconnected (every 5 seconds)
- On `End`: turn fan off, dispose BLE resources
- Build: `dotnet build -c Release` — auto-copies DLL to SimHub
- User maps GT Neo buttons to actions in SimHub Controls & Events

## Key Constraints

- Write without response (not write with response) on the characteristic
- Must enter Manual mode `[0x04, 0x04, 0x00, 0x00]` before speed
  commands work — do this on connect, not on every speed update
- Speed range: 0-100 as a single byte
- Must subscribe to notifications (CCCD) before writes are accepted
- We don't use notification data — subscription is just a protocol
  prerequisite

## Start With

Phase 1 Python PoC. Get the fan responding to keyboard input
before touching SimHub or C#. This validates the BLE layer first.
