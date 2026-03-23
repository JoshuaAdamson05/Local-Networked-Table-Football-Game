using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RodSpawnManager : NetworkBehaviour
{
    private byte _connectedPlayersCounter = 0;
    [SerializeField] private NetworkObject _blueRodPrefab;
    [SerializeField] private NetworkObject _redRodPrefab;
    private List<NetworkObject> _rods = new List<NetworkObject>();

    public override void OnNetworkSpawn()
    {
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnClientLoadedScene;
    }

    private void OnClientLoadedScene(ulong clientId, string sceneName, LoadSceneMode mode)
    {
        if (!IsServer) return;
        _connectedPlayersCounter++;

        if (_connectedPlayersCounter == NetworkManager.Singleton.ConnectedClients.Count)
        {
            Debug.Log("All players have loaded scene!");

            if (PersistentLobbySessionManager.Instance != null)
            {
                SpawnRod(NetworkManager.Singleton.ConnectedClientsIds[0], PersistentLobbySessionManager.Instance.AuthorityMode);
                SpawnRod(NetworkManager.Singleton.ConnectedClientsIds[1], PersistentLobbySessionManager.Instance.AuthorityMode);
            }
        }
    }

    private void SpawnRod(ulong clientId, RodAuthorityMode rodAuthorityMode)
    {
        NetworkObject rodToSpawn = clientId == NetworkManager.Singleton.LocalClientId ? _blueRodPrefab : _redRodPrefab; 
        RodInputManager rodInputManager = rodToSpawn.GetComponent<RodInputManager>();
        rodInputManager.SetAuthorityMode(rodAuthorityMode);
        NetworkObject spawnedRod = Instantiate(rodToSpawn, new(0, 0, 0), Quaternion.identity);
        spawnedRod.SpawnWithOwnership(clientId);      
        _rods.Add(spawnedRod);
    }

    private void Update()
    {
        if (MatchStateManager.Instance == null) return;

        if (MatchStateManager.Instance.CurrentMatchState.Value == MatchState.GameOver && _rods.Count > 0)
        {
            foreach (NetworkObject rod in _rods)
            {
                rod.Despawn();
                Destroy(rod.gameObject);
            }
            _rods.Clear();
        }
    }
}
