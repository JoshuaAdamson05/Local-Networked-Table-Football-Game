using Unity.Netcode;
using UnityEngine;

public class RodSpawner : NetworkBehaviour
{
    [SerializeField] private NetworkObject _blueRodPrefab;
    [SerializeField] private NetworkObject _redRodPrefab;

    public override void OnNetworkSpawn()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton.ConnectedClientsIds.Count > 2)
        {
            Debug.LogWarning("Maximum of 2 players reached. No more rods will be spawned.");
            return;
        }

        NetworkObject rodToSpawn = clientId == NetworkManager.Singleton.LocalClientId ? _blueRodPrefab : _redRodPrefab;
        NetworkObject spawnedRod = Instantiate(rodToSpawn, new (0, 0, 0), Quaternion.identity);
        spawnedRod.SpawnWithOwnership(clientId);
    }
}
