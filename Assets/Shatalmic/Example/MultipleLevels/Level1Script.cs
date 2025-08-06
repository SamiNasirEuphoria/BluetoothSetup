using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class Level1Script : MonoBehaviour
{
	public GameObject canvas_1, canvas_2;
	public void OnScanClick ()
	{
		BluetoothLEHardwareInterface.Initialize (true, false, () => {

			FoundDeviceListScript.DeviceAddressList = new List<DeviceObject> ();

			BluetoothLEHardwareInterface.ScanForPeripheralsWithServices (null, (address, name) => {

				FoundDeviceListScript.DeviceAddressList.Add (new DeviceObject (address, name));

			}, null);

		}, (error) => {

			BluetoothLEHardwareInterface.Log ("BLE Error: " + error);

		});
	}

	public void OnStartLevel2 ()
	{
		canvas_1.SetActive(false);
		canvas_2.SetActive(true);
	}
}
