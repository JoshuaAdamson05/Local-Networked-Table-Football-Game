using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public enum LobbyState
{
    WaitingForPlayersToBeReady,
    ReadyToPlay,
    ConnectingToMatch
}

public class LobbyStateManager : NetworkBehaviour
{
    public NetworkVariable<LobbyState> CurrentLobbyState = new NetworkVariable<LobbyState>(LobbyState.WaitingForPlayersToBeReady, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsHostReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsClientReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private ushort _nextGameSceneLoadCountdownEventId;
    [SerializeField] private float _readyToPlayCountdownTime = 5f;
    public NetworkVariable<double> PhaseEndTime = new NetworkVariable<double>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public static LobbyStateManager Instance;
    public NetworkList<GameSceneLoadCountdownEvent> CountdownEvents { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Multiple instances of LobbyStateManager detected. There should only be one LobbyStateManager in the scene.");
            Destroy(this.gameObject);
            return;
        }
        Instance = this;      
        CountdownEvents = new NetworkList<GameSceneLoadCountdownEvent>();
        CountdownEvents.OnListChanged += OnCountdownEventsChanged;
    }

    public override void OnNetworkDespawn()
    {
        CountdownEvents.OnListChanged -= OnCountdownEventsChanged;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton == null) return;

        double currentTime = NetworkManager.Singleton.ServerTime.Time;

        if (CurrentLobbyState.Value == LobbyState.WaitingForPlayersToBeReady && (IsHostReady.Value && IsClientReady.Value))
        {
            Debug.Log("Ready to play... Beginning countdown!");
            CurrentLobbyState.Value = LobbyState.ReadyToPlay;
            PhaseEndTime.Value = currentTime + _readyToPlayCountdownTime;         
            RegisterGameSceneLoadCountdownEvent();
        }

        if (CurrentLobbyState.Value == LobbyState.ReadyToPlay && (!IsHostReady.Value || !IsClientReady.Value))
        {
            Debug.Log("A player is no longer ready!");
            CurrentLobbyState.Value = LobbyState.WaitingForPlayersToBeReady;
            PhaseEndTime.Value = 0;
            IsHostReady.Value = false;
            IsClientReady.Value = false;
        }

        if (CurrentLobbyState.Value == LobbyState.ConnectingToMatch)
        {          
            Debug.Log("Connecting to game scene!");
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }

        if (PhaseEndTime.Value <= 0) return;
        if (currentTime < PhaseEndTime.Value) return;
        PhaseEndTime.Value = 0;

        switch (CurrentLobbyState.Value)
        {
            case LobbyState.ReadyToPlay:
                CurrentLobbyState.Value = LobbyState.ConnectingToMatch;
                break;
        }
    }

    private void RegisterGameSceneLoadCountdownEvent()
    {
        GameSceneLoadCountdownEvent countdownEvent = new GameSceneLoadCountdownEvent{
            GameSceneLoadCountdownId = _nextGameSceneLoadCountdownEventId++
        };

        CountdownEvents.Add(countdownEvent);

        const int maxCountdownEvents = 1;
        if (CountdownEvents.Count > maxCountdownEvents)
            CountdownEvents.RemoveAt(0);
    }

    private void OnCountdownEventsChanged(NetworkListEvent<GameSceneLoadCountdownEvent> change)
    {
        if (change.Type == NetworkListEvent<GameSceneLoadCountdownEvent>.EventType.Add)
        {
            GameSceneLoadCountdownEvent countdownEvent = change.Value;
            GameSceneLoadEventBus.RaiseGameSceneLoadCountdownEvent(countdownEvent);
        }
    }
}
