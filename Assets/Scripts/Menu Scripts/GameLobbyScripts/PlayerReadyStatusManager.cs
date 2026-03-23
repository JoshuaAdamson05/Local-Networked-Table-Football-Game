using UnityEngine;
using Unity.Netcode;
using TMPro;

public class PlayerReadyStatusManager : NetworkBehaviour
{
    private TextMeshProUGUI _clientObjectReadyStatusText;
    [SerializeField] private TextMeshProUGUI _hostReadyStatusText;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            _clientObjectReadyStatusText = GameObject.FindGameObjectWithTag("ClientReadyStatusText").GetComponent<TextMeshProUGUI>();
            if (_clientObjectReadyStatusText != null)
            {
                _clientObjectReadyStatusText.text = "Not Ready";
            }
            else
                Debug.LogError("Cannot find client object ready status text!");
        }
        else
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }   
        
        if (NetworkManager.Singleton.ConnectedClients.Count > 1)
        {
            _clientObjectReadyStatusText = GameObject.FindGameObjectWithTag("ClientReadyStatusText").GetComponent<TextMeshProUGUI>();
        }

        if (LobbyStateManager.Instance != null)
        {
            LobbyStateManager.Instance.IsHostReady.OnValueChanged += OnHostReadyToggle;
            LobbyStateManager.Instance.IsClientReady.OnValueChanged += OnClientReadyToggle;
            ApplyHostReadyStatus(LobbyStateManager.Instance.IsHostReady.Value);
            ApplyClientReadyStatus(LobbyStateManager.Instance.IsClientReady.Value);
        }
        else Debug.LogError("LobbyStateManager instance is missing!");
    }

    public override void OnNetworkDespawn()
    {
        if (LobbyStateManager.Instance != null)
        { 
            LobbyStateManager.Instance.IsHostReady.OnValueChanged -= OnHostReadyToggle;
            LobbyStateManager.Instance.IsClientReady.OnValueChanged -= OnClientReadyToggle;
        }

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }  
    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleClientStatusServerRpc()
    {
        if (LobbyStateManager.Instance != null)
        {
            LobbyStateManager.Instance.IsClientReady.Value = !LobbyStateManager.Instance.IsClientReady.Value;
        }
        else Debug.LogError("LobbyStateManager instance is missing!");
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        if (IsServer)
        {
            _clientObjectReadyStatusText = GameObject.FindGameObjectWithTag("ClientReadyStatusText").GetComponent<TextMeshProUGUI>();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer)
        {
            if (LobbyStateManager.Instance != null)
            {
                LobbyStateManager.Instance.IsClientReady.Value = false;
                LobbyStateManager.Instance.IsHostReady.Value = false;
            }
            else Debug.LogError("LobbyStateManager instance is missing!");

            _clientObjectReadyStatusText = null;
        }
    } 

    public void ToggleReadyStatus()
    {
        if (IsServer)
        {
            if (LobbyStateManager.Instance != null)
            {
                LobbyStateManager.Instance.IsHostReady.Value = !LobbyStateManager.Instance.IsHostReady.Value;
            }
            else Debug.LogError("LobbyStateManager instance is missing!");
        }
        else
        {
            ToggleClientStatusServerRpc();
        }
    } 

    private void OnClientReadyToggle(bool oldValue, bool newValue)
    {
        ApplyClientReadyStatus(newValue);
    }

    private void OnHostReadyToggle(bool previousValue, bool newValue)
    {
        ApplyHostReadyStatus(newValue);
    }

    private void ApplyHostReadyStatus(bool isReady)
    {
        if (_hostReadyStatusText == null)
        {
            Debug.LogError("Host ready status text missing!");
        }

        if (isReady)
        {
            _hostReadyStatusText.text = "Ready";
        }
        else
        {
            _hostReadyStatusText.text = "Not Ready";
        }
    }

    private void ApplyClientReadyStatus(bool isReady)
    {
        if (_clientObjectReadyStatusText == null) return;

        if (isReady)
            _clientObjectReadyStatusText.text = "Ready";
        else
            _clientObjectReadyStatusText.text = "Not Ready";
    }
}
