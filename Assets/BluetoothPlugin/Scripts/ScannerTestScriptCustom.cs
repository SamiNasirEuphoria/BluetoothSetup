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

    private string targetDeviceName = "BT05-A";
    private string connectedDeviceAddress = null;
    private string serviceUUID = null;
    private string characteristicUUID = null;

    private bool isConnected = false;
    private bool isConnecting = false;

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

                    // Also update prefab UI if exists
                    if (_scannedItems.ContainsKey(deviceAddress))
                    {
                        _scannedItems[deviceAddress].TextRSSIValue.text = message;
                    }
                    if (message.Contains("1"))
                    {
                        toggle.SetActive(true);
                    }
                    else if(message.Contains("0"))
                    {
                        toggle.SetActive(false);
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
