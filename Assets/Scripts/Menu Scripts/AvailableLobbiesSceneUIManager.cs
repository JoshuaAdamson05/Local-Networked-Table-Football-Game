using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AvailableLobbiesSceneUIManager : MonoBehaviour
{
    [SerializeField] private GameObject _lobbyObjectPrefab;
    [SerializeField] private GameObject _scrollbarContent;
    [SerializeField] private GameObject _availableLobbiesCanvas;
    [SerializeField] private GameObject _connectingCanvas;
    [SerializeField] private Button _refreshAvailableLobbbiesButton;
    [SerializeField] private TextMeshProUGUI _listeningText;
    private List<GameObject> _lobbyObjects = new List<GameObject>();

    private void Start()
    {
        ClientLobbyListener.OnLobbyDetailsUpdated += RefreshLobbyUI;
        ClientLobbyListener.OnSearchingAction += OnListeningAction;
    }

    private void OnDestroy()
    {
        ClientLobbyListener.OnLobbyDetailsUpdated -= RefreshLobbyUI;
        ClientLobbyListener.OnSearchingAction -= OnListeningAction;
    }

    private void RefreshLobbyUI(List<LobbyDetailsMessage> lobbyDetails)
    {
        foreach (GameObject gameObject in _lobbyObjects)
        {
            Destroy(gameObject);
        }
        _lobbyObjects.Clear();

        if (lobbyDetails.Count > 0)
        {
            for (int i = 0; i < lobbyDetails.Count; i++)
            {
                LobbyDetailsMessage lobbyDetailsMessage = lobbyDetails[i];
                GameObject lobbyObject = Instantiate(_lobbyObjectPrefab, _scrollbarContent.transform);
                _lobbyObjects.Add(lobbyObject);
                TextMeshProUGUI lobbyObjectDetailsText = lobbyObject.GetComponentInChildren<TextMeshProUGUI>();
                Button lobbyObjectButton = lobbyObject.GetComponentInChildren<Button>();

                if (lobbyObjectDetailsText != null)
                {
                    lobbyObjectDetailsText.text = lobbyDetailsMessage.LobbyName;
                }
                else Debug.LogError("Cannot find lobby details text in lobby object!");

                lobbyObjectButton.onClick.AddListener(() =>
                {
                    if (PersistentLobbyManager.Instance == null)
                    {
                        Debug.LogError("Persistent lobby manager instance missing!");
                        return;
                    }

                    PersistentLobbyManager.Instance.SetLobbyIP(lobbyDetailsMessage.IPAddress);
                    PersistentLobbyManager.Instance.SetGamePort(lobbyDetailsMessage.GameLobbyPort);
                    UnityTransport unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                    unityTransport.ConnectionData.Address = lobbyDetailsMessage.IPAddress;
                    unityTransport.ConnectionData.Port = lobbyDetailsMessage.GameLobbyPort;
                    _availableLobbiesCanvas.SetActive(false);
                    _connectingCanvas.SetActive(true);
                    NetworkManager.Singleton.StartClient();
                });
            }
        }
    }

    private void OnListeningAction(bool isListening)
    {
        if (isListening)
        {
            _listeningText.text = "Searching for Lobbies...";
            _refreshAvailableLobbbiesButton.gameObject.SetActive(false);
        }
        else
        {
            _listeningText.text = "Not currently searching!";
            _refreshAvailableLobbbiesButton.gameObject.SetActive(true);
        }
    }
}
