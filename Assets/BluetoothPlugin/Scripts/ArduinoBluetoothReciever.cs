using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Android;

public class ArduinoBluetoothReciever : MonoBehaviour
{
    [Header("Bluetooth Settings")]
    [Tooltip("UUID of the BLE service (from Arduino)")]
    public string serviceUUID = "FFE0"; // Replace if needed

    [Tooltip("UUID of the BLE characteristic (from Arduino)")]
    public string characteristicUUID = "FFE1"; // Replace if needed

    [Header("Action Settings")]
    [Tooltip("Object to activate on button press from Arduino")]
    public GameObject actionTarget;

    private bool isConnected = false;
    private string connectedDeviceName = "";

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.CoarseLocation))
            Permission.RequestUserPermission(Permission.CoarseLocation);
#endif
        StartCoroutine(StartBluetoothFlow());
    }

    IEnumerator StartBluetoothFlow()
    {
        yield return new WaitForSeconds(1f);

        BluetoothLEHardwareInterface.Initialize(true, false, () =>
        {
            Debug.Log("Bluetooth Initialized");
            StartScan();
        },
        (error) =>
        {
            Debug.LogError("Bluetooth Initialization Error: " + error);
        });
    }

    private void StartScan()
    {
        BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, (address, name) =>
        {
            Debug.Log("Found Device: " + name);

            // Optionally filter by name or MAC
            if (name.ToLower().Contains("hc") || name.ToLower().Contains("arduino"))
            {
                Debug.Log("Connecting to device: " + name);
                BluetoothLEHardwareInterface.StopScan();
                ConnectToDevice(address);
            }
        },
        null);
    }

    private void ConnectToDevice(string address)
    {
        BluetoothLEHardwareInterface.ConnectToPeripheral(address, null, null, (deviceAddress, serviceUUID, characteristicUUID) =>
        {
            Debug.Log("Connected to device: " + deviceAddress);
            isConnected = true;
            connectedDeviceName = deviceAddress;

            SubscribeToCharacteristic(deviceAddress);
        });
    }

    private void SubscribeToCharacteristic(string deviceAddress)
    {
        BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(deviceAddress, serviceUUID, characteristicUUID, null,
        (address, characteristic, data) =>
        {
            if (data == null || data.Length == 0)
                return;

            string received = System.Text.Encoding.UTF8.GetString(data);
            Debug.Log("Received: " + received);

            if (received == "1")
                PerformAction();
        });
    }

    private void PerformAction()
    {
        Debug.Log("Button Pressed from Arduino. Performing action...");
        if (actionTarget != null)
        {
            actionTarget.SetActive(!actionTarget.activeSelf);
        }
    }

    private void OnDestroy()
    {
        if (isConnected && !string.IsNullOrEmpty(connectedDeviceName))
        {
            BluetoothLEHardwareInterface.DisconnectPeripheral(connectedDeviceName, (address) =>
            {
                BluetoothLEHardwareInterface.DeInitialize(() =>
                {
                    Debug.Log("Bluetooth Deinitialized");
                });
            });
        }
    }
}
