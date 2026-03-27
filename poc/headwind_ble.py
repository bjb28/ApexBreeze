"""
ApexBreeze Phase 1 - Wahoo KICKR Headwind BLE Proof of Concept

Scans for the Headwind fan, connects, enters manual mode,
then accepts speed values (0-100) from keyboard input.
"""

import asyncio
import sys

from bleak import BleakClient, BleakScanner

DEVICE_NAME_PREFIX = "HEADWIND"
SERVICE_UUID = "a026ee0c-0a7d-4ab3-97fa-f1500f9feb8b"
CHARACTERISTIC_UUID = "a026e038-0a7d-4ab3-97fa-f1500f9feb8b"

CMD_MANUAL_MODE = bytes([0x04, 0x04, 0x00, 0x00])


def build_speed_command(speed: int) -> bytes:
    """Build the 4-byte speed command packet."""
    return bytes([0x02, speed, 0x00, 0x00])


async def find_headwind() -> str | None:
    """Scan for a device whose name starts with HEADWIND and return its address."""
    print("Scanning for Headwind fan...")
    devices = await BleakScanner.discover(timeout=10.0)
    for device in devices:
        if device.name and device.name.startswith(DEVICE_NAME_PREFIX):
            print(f"Found: {device.name} ({device.address})")
            return device.address
    return None


async def main():
    address = await find_headwind()
    if not address:
        print("ERROR: Headwind fan not found. Is it powered on?")
        sys.exit(1)

    print(f"Connecting to {address}...")
    async with BleakClient(address) as client:
        print(f"Connected: {client.is_connected}")

        # Discover the target characteristic
        char = None
        for service in client.services:
            if service.uuid == SERVICE_UUID:
                for c in service.characteristics:
                    if c.uuid == CHARACTERISTIC_UUID:
                        char = c
                        break
        if char is None:
            print("ERROR: Could not find the speed characteristic.")
            sys.exit(1)

        print(f"\nUsing characteristic: {char.uuid}")
        print(f"  Properties: {', '.join(char.properties)}")

        # Enable notifications — the Headwind requires the CCCD to be
        # written before it will process write commands
        def on_notify(sender, data):
            pass  # We don't need the data, just the subscription

        await client.start_notify(char, on_notify)
        print("Notifications enabled.")

        # Enter manual mode
        print("Sending manual mode command...")
        await client.write_gatt_char(char, CMD_MANUAL_MODE, response=False)
        # Give the fan time to process the mode switch
        await asyncio.sleep(1.0)
        print("Manual mode active.")

        # Interactive speed loop
        print("\nEnter a fan speed (0-100), or 'q' to quit:")
        while True:
            try:
                raw = await asyncio.get_event_loop().run_in_executor(
                    None, lambda: input("> ")
                )
            except EOFError:
                break

            raw = raw.strip()
            if raw.lower() == "q":
                break

            try:
                speed = int(raw)
            except ValueError:
                print("Invalid input. Enter a number 0-100 or 'q'.")
                continue

            if not 0 <= speed <= 100:
                print("Speed must be between 0 and 100.")
                continue

            cmd = build_speed_command(speed)
            await client.write_gatt_char(char, cmd, response=False)
            print(f"Fan speed set to {speed}%")

        # Turn off fan before disconnecting
        print("Setting fan to 0 before disconnect...")
        await client.write_gatt_char(char, build_speed_command(0), response=False)
        print("Done.")


if __name__ == "__main__":
    asyncio.run(main())
