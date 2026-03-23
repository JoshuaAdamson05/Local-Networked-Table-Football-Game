using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Collections;
using TMPro;

public class LobbyRodAuthorityManager : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> AuthorityModeString = new NetworkVariable<FixedString64Bytes>("Client Authority", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [SerializeField] private Button _clientAuthorityButton;
    [SerializeField] private Button _serverAuthorityButton;  
    [SerializeField] private GameObject _clientAuthorityImage;
    private TextMeshProUGUI _clientAuthorityText;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (NetworkManager.Singleton.ConnectedClients.Count > 1)
            {
                if (PersistentLobbySessionManager.Instance != null)
                {
                    SetAuthorityMode(PersistentLobbySessionManager.Instance.AuthorityMode);
                }
            }
        }

        if (!IsServer)
        {
            AuthorityModeString.OnValueChanged += OnAuthorityStringChanged;
            
            if (_clientAuthorityButton != null)
            {
                _clientAuthorityButton.gameObject.SetActive(false);
            }
            else
                Debug.LogError("Client authority button missing!");

            if (_serverAuthorityButton != null)
            {
                _serverAuthorityButton.gameObject.SetActive(false);
            }
            else
                Debug.LogError("Server authority button missing");

            if (_clientAuthorityImage != null)
            {
                _clientAuthorityImage.gameObject.SetActive(true);
                _clientAuthorityText = _clientAuthorityImage.GetComponentInChildren<TextMeshProUGUI>();
            }
            else
                Debug.LogError("Client Authority image missing!");

            if (_clientAuthorityText != null)
            {
                _clientAuthorityText.text = AuthorityModeString.Value.ToString();
            }
            else Debug.LogError("Cannot find client authority text!");
        }
    }

    public override void OnNetworkDespawn()
    {
        AuthorityModeString.OnValueChanged -= OnAuthorityStringChanged;
    }
    
    private void SetAuthorityMode(RodAuthorityMode rodAuthorityMode)
    {
        if (!IsServer) return;
        if (PersistentLobbySessionManager.Instance == null) return;
        if (_clientAuthorityButton == null) return;
        if (_serverAuthorityButton == null) return;
        if (PersistentLobbySessionManager.Instance.SetAuthorityMode(rodAuthorityMode))
        {
            switch (PersistentLobbySessionManager.Instance.AuthorityMode)
            {
                case RodAuthorityMode.ServerAuthoritative:
                    SetButtonColour(_serverAuthorityButton, Color.green);
                    SetButtonColour(_clientAuthorityButton, Color.red);
                    AuthorityModeString.Value = "Server Authority";
                    break;

                case RodAuthorityMode.ClientAuthoritative:
                    SetButtonColour(_clientAuthorityButton, Color.green);
                    SetButtonColour(_serverAuthorityButton, Color.red);
                    AuthorityModeString.Value = "Client Authority";
                    break;
            }
        }
        else
            Debug.LogError("Cannot toggle authority mode!");
    }
    
    public void SetClientAuthority()
    {
        if (IsServer)
        {
            SetAuthorityMode(RodAuthorityMode.ClientAuthoritative);
        }
    }

    public void SetServerAuthority()
    {
        if (IsServer)
        {
            SetAuthorityMode(RodAuthorityMode.ServerAuthoritative);
        }
    }

    private void OnAuthorityStringChanged(FixedString64Bytes oldString, FixedString64Bytes newString)
    {
        if (_clientAuthorityText != null)
        {
            _clientAuthorityText.text = AuthorityModeString.Value.ToString();
        }
        else Debug.LogError("Client authority text is missing!");
    }

    private void SetButtonColour(Button button, Color colour)
    {
        if (!IsServer) return;

        ColorBlock cb = button.colors;
        cb.normalColor = colour;
        cb.pressedColor = colour;
        cb.selectedColor = colour;
        button.colors = cb;
    }
}
