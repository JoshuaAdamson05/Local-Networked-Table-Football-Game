using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.VisualScripting;
using UnityEngine;

public class NetworkManagerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject networkManagerObjectPrefab;
    private void Awake()
    {
        if (NetworkManager.Singleton == null)
        {
            if (networkManagerObjectPrefab == null)
            {
                Debug.LogError("Network manager object prefab missing!");
                return;
            }

            Instantiate(networkManagerObjectPrefab);
            Debug.Log("Spawning network manager!");
        }
        else Debug.LogWarning("Network Manager already exists!");
    }
}
