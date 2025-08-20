using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

public class ScannerTestScriptCustom : MonoBehaviour
{
    public GameObject ScannedItemPrefab;
    public GameObject toggle;
    public Text outputText; // UI Text to display received data

    private float _timeout;
    private float _startScanTimeout = 10f;
    private float _startScanDelay = 0.5f;
    private bool _startScan = true;

    private Dictionary<string, ScannedItemScript> _scannedItems;

    private string targetDeviceName = "BT06";
    private string connectedDeviceAddress = null;
    private string serviceUUID = null;
    private string characteristicUUID = null;

    private bool isConnected = false;
    private bool isConnecting = false;


    private Coroutine buttonTimeoutCoroutine; // To manage button release timer

    public void OnStopScanning()
    {
        BluetoothLEHardwareInterface.Log("**************** stopping");
        BluetoothLEHardwareInterface.StopScan();
    }

    void Start()
    {
        BluetoothLEHardwareInterface.Log("Start");
        _scannedItems = new Dictionary<string, ScannedItemScript>();

        BluetoothLEHardwareInterface.Initialize(true, false, () =>
        {
            _timeout = _startScanDelay;
        },
        (error) =>
        {
            BluetoothLEHardwareInterface.Log("Error: " + error);
            if (error.Contains("Bluetooth LE Not Enabled"))
                BluetoothLEHardwareInterface.BluetoothEnable(true);
        });
    }

    void Update()
    {
        if (_timeout > 0f)
        {
            _timeout -= Time.deltaTime;
            if (_timeout <= 0f)
            {
                if (_startScan)
                {
                    _startScan = false;
                    _timeout = _startScanTimeout;

                    BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, null, (address, name, rssi, bytes) =>
                    {
                        if (!string.IsNullOrEmpty(name) && name.Equals(targetDeviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            BluetoothLEHardwareInterface.Log("Device found: " + address);

                            // Update UI prefab
                            if (_scannedItems.ContainsKey(address))
                            {
                                var scannedItem = _scannedItems[address];
                                scannedItem.TextRSSIValue.text = rssi.ToString();
                                BluetoothLEHardwareInterface.Log("Already in list " + rssi.ToString());
                            }
                            else
                            {
                                BluetoothLEHardwareInterface.Log("New item: " + address);
                                var newItem = Instantiate(ScannedItemPrefab);
                                if (newItem != null)
                                {
                                    BluetoothLEHardwareInterface.Log("Item created: " + address);
                                    newItem.transform.SetParent(transform, false);
                                    newItem.transform.localScale = Vector3.one;

                                    var scannedItem = newItem.GetComponent<ScannedItemScript>();
                                    if (scannedItem != null)
                                    {
                                        scannedItem.TextAddressValue.text = address;
                                        scannedItem.TextNameValue.text = name;
                                        scannedItem.TextRSSIValue.text = rssi.ToString();

                                        _scannedItems[address] = scannedItem;
                                    }
                                }
                            }

                            // Stop scan and connect
                            BluetoothLEHardwareInterface.StopScan();
                            ConnectToDevice(address);
                        }
                    }, true);
                }
                else
                {
                    BluetoothLEHardwareInterface.StopScan();
                    _startScan = true;
                    _timeout = _startScanDelay;
                }
            }
        }
    }

    private void ConnectToDevice(string address)
    {
        if (isConnected || isConnecting)
            return;

        isConnecting = true;
        connectedDeviceAddress = address;

        BluetoothLEHardwareInterface.ConnectToPeripheral(address, null, null, (addr, serviceUUID, characteristicUUID) =>
        {
            BluetoothLEHardwareInterface.Log("Connected to: " + address);
            isConnected = true;
            isConnecting = false;

            this.serviceUUID = serviceUUID;
            this.characteristicUUID = characteristicUUID;

            BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(
                connectedDeviceAddress,
                serviceUUID,
                characteristicUUID,
                null,
                (deviceAddress, charUUID, bytes) =>
                {
                    string message = System.Text.Encoding.UTF8.GetString(bytes);
                    BluetoothLEHardwareInterface.Log("Received: " + message);

                    if (outputText != null)
                        outputText.text = message;


                    Debug.Log("message" + message);
                    // Also update prefab UI if exists
                    if (_scannedItems.ContainsKey(deviceAddress))
                    {
                        _scannedItems[deviceAddress].TextRSSIValue.text = message;

                    }
                    if (message.Contains("a"))
                    {
                        Debug.Log("Toggle is pressed");
                        toggle.SetActive(true);
                        //just to check button released or not
                        // Stop any existing timeout coroutine
                        if (buttonTimeoutCoroutine != null)
                            StopCoroutine(buttonTimeoutCoroutine);
                        // Start timeout to deactivate toggle after 0.5 seconds (adjust as needed)
                        buttonTimeoutCoroutine = StartCoroutine(DeactivateToggleAfterTimeout(0.2f));

                    }
                });

        },
        (disconnectAddress) =>
        {
            BluetoothLEHardwareInterface.Log("Disconnected from: " + disconnectAddress);
            isConnected = false;
            isConnecting = false;
            StartCoroutine(RetryConnection());
        });
    }
    private IEnumerator DeactivateToggleAfterTimeout(float timeout)
    {
        yield return new WaitForSeconds(timeout);
        toggle.SetActive(false);
        Debug.Log("Toggle deactivated after timeout");
        buttonTimeoutCoroutine = null;
    }
    private IEnumerator RetryConnection()
    {
        yield return new WaitForSeconds(2f);
        if (!isConnected)
        {
            BluetoothLEHardwareInterface.Log("Retrying connection...");
            StartScanAgain();
        }
    }

    private void StartScanAgain()
    {
        _startScan = true;
        _timeout = _startScanDelay;
    }

    void OnApplicationQuit()
    {
        if (isConnected)
        {
            BluetoothLEHardwareInterface.UnSubscribeCharacteristic(connectedDeviceAddress, serviceUUID, characteristicUUID, null);
            BluetoothLEHardwareInterface.DisconnectPeripheral(connectedDeviceAddress, null);
        }

        BluetoothLEHardwareInterface.DeInitialize(() =>
        {
            Debug.Log("Bluetooth Deinitialized");
        });
    }
}

//using UnityEngine;
//using UnityEngine.UI;
//using UnityEngine.Android; // For Permission API
//using System;
//using System.Collections;
//using System.Collections.Generic;

//public class ScannerTestScriptCustom : MonoBehaviour
//{
//    public GameObject ScannedItemPrefab;
//    public GameObject toggle;
//    public Text outputText; // UI Text to display raw received data (for debugging)
//    public Text joystickText; // UI Text to display joystick X/Y values

//    private float _timeout;
//    private float _startScanTimeout = 10f;
//    private float _startScanDelay = 0.5f;
//    private bool _startScan = true;

//    private Dictionary<string, ScannedItemScript> _scannedItems;

//    private string targetDeviceName = "BT06";
//    private string connectedDeviceAddress = null;
//    private string serviceUUID = null;
//    private string characteristicUUID = null;

//    private bool isConnected = false;
//    private bool isConnecting = false;

//    private Coroutine buttonTimeoutCoroutine; // To manage button release timer
//    private float lastDataReceivedTime = 0f; // Track last data time for heartbeat check
//    private const float dataTimeout = 5f; // Seconds without data before resubscribe/reconnect

//    public void OnStopScanning()
//    {
//        BluetoothLEHardwareInterface.Log("**************** stopping");
//        BluetoothLEHardwareInterface.StopScan();
//    }

//    void Start()
//    {
//        // Validate UI elements
//        if (outputText == null) Debug.LogError("outputText not assigned in Inspector!");
//        if (joystickText == null) Debug.LogError("joystickText not assigned in Inspector!");
//        if (toggle == null) Debug.LogError("toggle not assigned in Inspector!");

//        // Request runtime permissions (critical for Android 12+ / Quest 3)
//        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
//            Permission.RequestUserPermission(Permission.FineLocation);
//        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
//            Permission.RequestUserPermission("android.permission.BLUETOOTH_SCAN");
//        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
//            Permission.RequestUserPermission("android.permission.BLUETOOTH_CONNECT");

//        BluetoothLEHardwareInterface.Log("Start");
//        _scannedItems = new Dictionary<string, ScannedItemScript>();

//        BluetoothLEHardwareInterface.Initialize(true, false, () =>
//        {
//            _timeout = _startScanDelay;
//            Debug.Log("Bluetooth initialized successfully");
//        },
//        (error) =>
//        {
//            BluetoothLEHardwareInterface.Log("Error: " + error);
//            Debug.LogError("Bluetooth init error: " + error);
//            if (error.Contains("Bluetooth LE Not Enabled"))
//                BluetoothLEHardwareInterface.BluetoothEnable(true);
//        });
//    }

//    void Update()
//    {
//        if (_timeout > 0f)
//        {
//            _timeout -= Time.deltaTime;
//            if (_timeout <= 0f)
//            {
//                if (_startScan)
//                {
//                    _startScan = false;
//                    _timeout = _startScanTimeout;

//                    BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, null, (address, name, rssi, bytes) =>
//                    {
//                        Debug.Log($"Scan result: {name} at {address}, RSSI: {rssi}"); // Log RSSI for signal check
//                        if (!string.IsNullOrEmpty(name) && name.Equals(targetDeviceName, StringComparison.OrdinalIgnoreCase))
//                        {
//                            BluetoothLEHardwareInterface.Log("Device found: " + address);

//                            // Update UI prefab
//                            if (_scannedItems.ContainsKey(address))
//                            {
//                                var scannedItem = _scannedItems[address];
//                                scannedItem.TextRSSIValue.text = rssi.ToString();
//                                BluetoothLEHardwareInterface.Log("Already in list " + rssi.ToString());
//                            }
//                            else
//                            {
//                                BluetoothLEHardwareInterface.Log("New item: " + address);
//                                var newItem = Instantiate(ScannedItemPrefab);
//                                if (newItem != null)
//                                {
//                                    BluetoothLEHardwareInterface.Log("Item created: " + address);
//                                    newItem.transform.SetParent(transform, false);
//                                    newItem.transform.localScale = Vector3.one;

//                                    var scannedItem = newItem.GetComponent<ScannedItemScript>();
//                                    if (scannedItem != null)
//                                    {
//                                        scannedItem.TextAddressValue.text = address;
//                                        scannedItem.TextNameValue.text = name;
//                                        scannedItem.TextRSSIValue.text = rssi.ToString();

//                                        _scannedItems[address] = scannedItem;
//                                    }
//                                }
//                            }

//                            // Stop scan and connect only if RSSI is strong enough (optional threshold)
//                            if (rssi > -80) // Adjust based on testing; below -80 may be too weak
//                            {
//                                BluetoothLEHardwareInterface.StopScan();
//                                ConnectToDevice(address);
//                            }
//                            else
//                            {
//                                Debug.LogWarning("RSSI too low (" + rssi + "), skipping connect");
//                            }
//                        }
//                    }, true);
//                }
//                else
//                {
//                    BluetoothLEHardwareInterface.StopScan();
//                    _startScan = true;
//                    _timeout = _startScanDelay;
//                }
//            }
//        }

//        // Heartbeat check: If connected but no data for too long, resubscribe or reconnect
//        if (isConnected && Time.time - lastDataReceivedTime > dataTimeout)
//        {
//            Debug.LogWarning("No data received for " + dataTimeout + "s, attempting resubscribe/reconnect");
//            ResubscribeCharacteristic();
//            lastDataReceivedTime = Time.time; // Reset to avoid spam
//        }
//    }

//    private void ConnectToDevice(string address)
//    {
//        if (isConnected || isConnecting)
//            return;

//        isConnecting = true;
//        connectedDeviceAddress = address;

//        BluetoothLEHardwareInterface.ConnectToPeripheral(address, null, null, (addr, serviceUUID, characteristicUUID) =>
//        {
//            BluetoothLEHardwareInterface.Log("Connected to: " + address);
//            Debug.Log("Connected successfully to " + addr);
//            isConnected = true;
//            isConnecting = false;

//            this.serviceUUID = serviceUUID;
//            this.characteristicUUID = characteristicUUID;

//            SubscribeToCharacteristic();
//            lastDataReceivedTime = Time.time; // Initialize heartbeat
//        },
//        (disconnectAddress) =>
//        {
//            BluetoothLEHardwareInterface.Log("Disconnected from: " + disconnectAddress);
//            Debug.LogWarning("Disconnected from " + disconnectAddress);
//            isConnected = false;
//            isConnecting = false;
//            // Reset UI on disconnect
//            if (outputText != null) outputText.text = "Disconnected";
//            if (joystickText != null) joystickText.text = "Joystick: N/A";
//            toggle.SetActive(false);
//            StartCoroutine(RetryConnection());
//        });
//    }

//    private void SubscribeToCharacteristic()
//    {
//        if (string.IsNullOrEmpty(connectedDeviceAddress) || string.IsNullOrEmpty(serviceUUID) || string.IsNullOrEmpty(characteristicUUID))
//        {
//            Debug.LogError("Cannot subscribe: Missing address/service/characteristic");
//            return;
//        }

//        BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(
//            connectedDeviceAddress,
//            serviceUUID,
//            characteristicUUID,
//            null,
//            (deviceAddress, charUUID, bytes) =>
//            {
//                string message = System.Text.Encoding.UTF8.GetString(bytes).Trim(); // Trim any extra characters
//                BluetoothLEHardwareInterface.Log("Received: " + message);
//                lastDataReceivedTime = Time.time; // Update heartbeat on any data

//                if (outputText != null)
//                    outputText.text = message;

//                Debug.Log("message: " + message);

//                // Also update prefab UI if exists
//                if (_scannedItems.ContainsKey(deviceAddress))
//                {
//                    _scannedItems[deviceAddress].TextRSSIValue.text = message;
//                }

//                // Handle button press
//                if (message.Contains("a"))
//                {
//                    Debug.Log("Toggle is pressed");
//                    toggle.SetActive(true);
//                    // Stop any existing timeout coroutine
//                    if (buttonTimeoutCoroutine != null)
//                        StopCoroutine(buttonTimeoutCoroutine);
//                    // Start timeout to deactivate toggle after 0.5 seconds (adjust as needed)
//                    buttonTimeoutCoroutine = StartCoroutine(DeactivateToggleAfterTimeout(0.5f));
//                }
//                // Handle joystick values (assuming any non-"a" message is joystick data)
//                else
//                {
//                    if (joystickText != null)
//                        joystickText.text = "Joystick: " + message;
//                }
//            });
//    }

//    private void ResubscribeCharacteristic()
//    {
//        // Unsubscribe first to reset
//        BluetoothLEHardwareInterface.UnSubscribeCharacteristic(connectedDeviceAddress, serviceUUID, characteristicUUID, null);
//        Debug.Log("Unsubscribed from characteristic, resubscribing...");
//        SubscribeToCharacteristic();
//    }

//    private IEnumerator DeactivateToggleAfterTimeout(float timeout)
//    {
//        yield return new WaitForSeconds(timeout);
//        toggle.SetActive(false);
//        Debug.Log("Toggle deactivated after timeout");
//        buttonTimeoutCoroutine = null;
//    }

//    private IEnumerator RetryConnection()
//    {
//        yield return new WaitForSeconds(2f);
//        if (!isConnected)
//        {
//            BluetoothLEHardwareInterface.Log("Retrying connection...");
//            StartScanAgain();
//        }
//    }

//    private void StartScanAgain()
//    {
//        _startScan = true;
//        _timeout = _startScanDelay;
//    }

//    void OnApplicationQuit()
//    {
//        if (isConnected)
//        {
//            BluetoothLEHardwareInterface.UnSubscribeCharacteristic(connectedDeviceAddress, serviceUUID, characteristicUUID, null);
//            BluetoothLEHardwareInterface.DisconnectPeripheral(connectedDeviceAddress, null);
//        }

//        BluetoothLEHardwareInterface.DeInitialize(() =>
//        {
//            Debug.Log("Bluetooth Deinitialized");
//        });
//    }
//}