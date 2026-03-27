using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace ApexBreezePlugin
{
    [PluginDescription("Controls Wahoo KICKR Headwind fan speed via BLE from wheel buttons")]
    [PluginAuthor("ApexBreeze")]
    [PluginName("ApexBreeze")]
    public class ApexBreezePlugin : IPlugin, IDataPlugin
    {
        private static readonly Guid ServiceUuid =
            new Guid("a026ee0c-0a7d-4ab3-97fa-f1500f9feb8b");
        private static readonly Guid CharacteristicUuid =
            new Guid("a026e038-0a7d-4ab3-97fa-f1500f9feb8b");

        private static readonly byte[] CmdManualMode = { 0x04, 0x04, 0x00, 0x00 };

        private const int SpeedStep = 10;
        private const int SpeedMin = 0;
        private const int SpeedMax = 100;
        private const double ReconnectIntervalSeconds = 5.0;
        private const double ScanTimeoutSeconds = 15.0;

        private GattCharacteristic _characteristic;
        private BluetoothLEDevice _device;
        private int _currentSpeed;
        private int _lastSentSpeed = -1;
        private bool _connected;
        private bool _connecting;
        private DateTime _lastReconnectAttempt = DateTime.MinValue;

        public PluginManager PluginManager { get; set; }

        public void Init(PluginManager pluginManager)
        {
            SimHub.Logging.Current.Info("ApexBreeze: Initializing");

            _currentSpeed = 0;

            // Expose fan speed property for dashboards
            this.AttachDelegate("FanSpeed", () => _currentSpeed);
            this.AttachDelegate("Connected", () => _connected);

            // Register button actions for SimHub input mapping
            this.AddAction("FanSpeedUp", (a, b) =>
            {
                _currentSpeed = Math.Min(SpeedMax, _currentSpeed + SpeedStep);
                SimHub.Logging.Current.Info($"ApexBreeze: Speed up -> {_currentSpeed}");
            });

            this.AddAction("FanSpeedDown", (a, b) =>
            {
                _currentSpeed = Math.Max(SpeedMin, _currentSpeed - SpeedStep);
                SimHub.Logging.Current.Info($"ApexBreeze: Speed down -> {_currentSpeed}");
            });

            this.AddAction("FanOff", (a, b) =>
            {
                _currentSpeed = 0;
                SimHub.Logging.Current.Info("ApexBreeze: Fan off");
            });

            this.AddAction("FanMax", (a, b) =>
            {
                _currentSpeed = SpeedMax;
                SimHub.Logging.Current.Info("ApexBreeze: Fan max");
            });

            // Start initial BLE connection
            Task.Run(() => ConnectAsync());
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            // Attempt reconnect if disconnected
            if (!_connected && !_connecting)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastReconnectAttempt).TotalSeconds >= ReconnectIntervalSeconds)
                {
                    _lastReconnectAttempt = now;
                    Task.Run(() => ConnectAsync());
                }
                return;
            }

            // Only write to BLE when the speed value has actually changed
            if (_connected && _currentSpeed != _lastSentSpeed)
            {
                var speed = _currentSpeed;
                _lastSentSpeed = speed;
                Task.Run(() => WriteSpeedAsync(speed));
            }
        }

        public void End(PluginManager pluginManager)
        {
            SimHub.Logging.Current.Info("ApexBreeze: Shutting down");

            // Turn fan off before disconnecting
            if (_connected && _characteristic != null)
            {
                try
                {
                    WriteSpeedAsync(0).Wait(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    SimHub.Logging.Current.Warn($"ApexBreeze: Failed to turn off fan: {ex.Message}");
                }
            }

            Cleanup();
        }

        /// <summary>
        /// Scans for the Headwind using BluetoothLEAdvertisementWatcher (active BLE scan).
        /// This finds unpaired devices — unlike DeviceInformation.FindAllAsync which only
        /// returns devices already known to Windows.
        /// </summary>
        private Task<ulong?> ScanForHeadwindAsync()
        {
            var tcs = new TaskCompletionSource<ulong?>();
            var watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            Timer timeoutTimer = null;

            void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
            {
                if (args.Advertisement.LocalName != null &&
                    args.Advertisement.LocalName.StartsWith("HEADWIND", StringComparison.OrdinalIgnoreCase))
                {
                    SimHub.Logging.Current.Info($"ApexBreeze: Found {args.Advertisement.LocalName} (addr: {args.BluetoothAddress:X12})");
                    watcher.Stop();
                    timeoutTimer?.Dispose();
                    tcs.TrySetResult(args.BluetoothAddress);
                }
            }

            void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
            {
                tcs.TrySetResult(null);
            }

            watcher.Received += OnReceived;
            watcher.Stopped += OnStopped;

            // Timeout after ScanTimeoutSeconds
            timeoutTimer = new Timer(_ =>
            {
                SimHub.Logging.Current.Info("ApexBreeze: Scan timed out");
                watcher.Stop();
            }, null, TimeSpan.FromSeconds(ScanTimeoutSeconds), Timeout.InfiniteTimeSpan);

            SimHub.Logging.Current.Info("ApexBreeze: Starting BLE scan...");
            watcher.Start();

            return tcs.Task;
        }

        private async Task ConnectAsync()
        {
            if (_connecting) return;
            _connecting = true;

            try
            {
                // Active BLE scan for the Headwind
                var bluetoothAddress = await ScanForHeadwindAsync();

                if (!bluetoothAddress.HasValue)
                {
                    SimHub.Logging.Current.Warn("ApexBreeze: Headwind fan not found");
                    return;
                }

                // Connect using the Bluetooth address from the advertisement
                _device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress.Value);

                if (_device == null)
                {
                    SimHub.Logging.Current.Warn("ApexBreeze: Could not connect to Headwind");
                    return;
                }

                SimHub.Logging.Current.Info($"ApexBreeze: Connected to {_device.Name}");
                _device.ConnectionStatusChanged += OnConnectionStatusChanged;

                // Discover the fan control service
                var servicesResult = await _device.GetGattServicesForUuidAsync(ServiceUuid);
                if (servicesResult.Status != GattCommunicationStatus.Success ||
                    servicesResult.Services.Count == 0)
                {
                    SimHub.Logging.Current.Warn("ApexBreeze: Fan control service not found");
                    Cleanup();
                    return;
                }

                var service = servicesResult.Services[0];

                // Discover the speed characteristic
                var charsResult = await service.GetCharacteristicsForUuidAsync(CharacteristicUuid);
                if (charsResult.Status != GattCommunicationStatus.Success ||
                    charsResult.Characteristics.Count == 0)
                {
                    SimHub.Logging.Current.Warn("ApexBreeze: Speed characteristic not found");
                    Cleanup();
                    return;
                }

                _characteristic = charsResult.Characteristics[0];

                // Subscribe to notifications — required before the fan accepts writes
                var notifyStatus = await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);

                if (notifyStatus != GattCommunicationStatus.Success)
                {
                    SimHub.Logging.Current.Warn($"ApexBreeze: Failed to enable notifications: {notifyStatus}");
                    Cleanup();
                    return;
                }

                SimHub.Logging.Current.Info("ApexBreeze: Notifications enabled");

                // Send manual mode command
                var manualResult = await _characteristic.WriteValueAsync(
                    CmdManualMode.AsBuffer(), GattWriteOption.WriteWithoutResponse);

                if (manualResult != GattCommunicationStatus.Success)
                {
                    SimHub.Logging.Current.Warn($"ApexBreeze: Failed to enter manual mode: {manualResult}");
                    Cleanup();
                    return;
                }

                await Task.Delay(1000);
                SimHub.Logging.Current.Info("ApexBreeze: Manual mode active, ready to control fan");

                _connected = true;
                _lastSentSpeed = -1; // Force a write on next DataUpdate
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"ApexBreeze: Connection error: {ex.Message}");
                Cleanup();
            }
            finally
            {
                _connecting = false;
            }
        }

        private async Task WriteSpeedAsync(int speed)
        {
            if (_characteristic == null) return;

            try
            {
                var cmd = new byte[] { 0x02, (byte)speed, 0x00, 0x00 };
                var result = await _characteristic.WriteValueAsync(
                    cmd.AsBuffer(), GattWriteOption.WriteWithoutResponse);

                if (result != GattCommunicationStatus.Success)
                {
                    SimHub.Logging.Current.Warn($"ApexBreeze: Write failed: {result}");
                    _connected = false;
                }
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"ApexBreeze: Write error: {ex.Message}");
                _connected = false;
            }
        }

        private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                SimHub.Logging.Current.Warn("ApexBreeze: Headwind disconnected");
                _connected = false;
                _lastSentSpeed = -1;
            }
        }

        private void Cleanup()
        {
            _connected = false;
            _characteristic = null;

            if (_device != null)
            {
                _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _device.Dispose();
                _device = null;
            }
        }
    }
}
