using UnityEngine;
using Unity.Netcode;

public enum MatchState
{
    WaitingForPlayers,
    InPlay,
    GoalScored,
    KickOff,
    GameOver,
    ConnectingToLobby
}

public class MatchStateManager : NetworkBehaviour
{
    private ushort _nextSetupKickOffID;
    private ushort _nextKickOffID;
    [SerializeField] private float _goalFreezeDuration = 3.0f;
    [SerializeField] private float _kickOffDuration = 3.0f;
    [SerializeField] private float _gameLobbyConnectionWaitSeconds = 5f;
    private int _lastProcessedGoalID = -1;

    private TeamEnums _lastKickingOffTeam = TeamEnums.None;

    public static MatchStateManager Instance { get; private set; }
    public NetworkVariable<MatchState> CurrentMatchState = new NetworkVariable<MatchState>
        (MatchState.WaitingForPlayers, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<double> PhaseEndTime = new NetworkVariable<double>
        (0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkList<SetUpKickOffEvent> SetUpKickOffEvents { get; private set; }
    public NetworkList<KickOffEvent> KickOffEvents { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Multiple instances of MatchStateManager detected. There should only be one MatchStateManager in the scene.");
            Destroy(this);
            return;
        }
        Instance = this;
        SetUpKickOffEvents = new NetworkList<SetUpKickOffEvent>();
        KickOffEvents = new NetworkList<KickOffEvent>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            GoalEventBus.OnGoalScored += OnGoalScored;
            Debug.Log($"Current Match State : {CurrentMatchState.ToString()}");
        }

        if (IsClient)
        {
            SetUpKickOffEvents.OnListChanged += OnSetUpKickOffEventsChanged;
            KickOffEvents.OnListChanged += OnKickOffEventsChanged;

            if (SetUpKickOffEvents.Count > 0)
            {
                SetUpKickOffEvent setUpKickOffEvent = SetUpKickOffEvents[SetUpKickOffEvents.Count - 1];
                SetUpKickOffEventBus.RaiseKickOffSetup(setUpKickOffEvent);
            }

            if (KickOffEvents.Count > 0)
            {
                KickOffEvent kickOffEvent = KickOffEvents[KickOffEvents.Count - 1];
                KickOffEventBus.RaiseKickOff(kickOffEvent);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            GoalEventBus.OnGoalScored -= OnGoalScored;
        }

        if (IsClient)
        {
            SetUpKickOffEvents.OnListChanged -= OnSetUpKickOffEventsChanged;
            KickOffEvents.OnListChanged -= OnKickOffEventsChanged;
        }
    }

    private void OnGoalScored(GoalEvent goal)
    {
        if (!IsServer) return;
        if (CurrentMatchState.Value != MatchState.InPlay) return;
        if (goal.GoalID <= _lastProcessedGoalID) return;
        _lastKickingOffTeam = goal.ScoringTeam == TeamEnums.Red ? TeamEnums.Blue : TeamEnums.Red;
        _lastProcessedGoalID = goal.GoalID;
        CurrentMatchState.Value = MatchState.GoalScored;
        PhaseEndTime.Value = NetworkManager.Singleton.ServerTime.Time + _goalFreezeDuration;
    }

    private void RegisterKickOffSetUp()
    {
        SetUpKickOffEvent newKickOff = new SetUpKickOffEvent
        {
            SetUpKickOffID = _nextSetupKickOffID++,
        };
        SetUpKickOffEvents.Add(newKickOff);

        const int maxKickOffsToKeep = 2;
        if (SetUpKickOffEvents.Count > maxKickOffsToKeep)
            SetUpKickOffEvents.RemoveAt(0);
    }

    private void RegisterKickOff(TeamEnums kickOffTeam)
    {
        KickOffEvent newKickOff = new KickOffEvent
        {
            KickOffID = _nextKickOffID++,
            KickingOffTeam = kickOffTeam
        };
        KickOffEvents.Add(newKickOff);

        const int maxKickOffsToKeep = 2;
        if (KickOffEvents.Count > maxKickOffsToKeep)
            KickOffEvents.RemoveAt(0);
    }

    private void OnSetUpKickOffEventsChanged(NetworkListEvent<SetUpKickOffEvent> change)
    {
        if (change.Type == NetworkListEvent<SetUpKickOffEvent>.EventType.Add)
        {
            SetUpKickOffEvent newKickOff = change.Value;
            SetUpKickOffEventBus.RaiseKickOffSetup(newKickOff);
        }
    }

    private void OnKickOffEventsChanged(NetworkListEvent<KickOffEvent> change)
    {
        if (change.Type == NetworkListEvent<KickOffEvent>.EventType.Add)
        {
            KickOffEvent newKickOff = change.Value;
            KickOffEventBus.RaiseKickOff(newKickOff);
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        double currentTime = NetworkManager.Singleton.ServerTime.Time;

        if (CurrentMatchState.Value == MatchState.WaitingForPlayers && NetworkManager.Singleton.ConnectedClientsIds.Count == 2)
        {
            CurrentMatchState.Value = MatchState.KickOff;     
            PhaseEndTime.Value = currentTime + _kickOffDuration;
            RegisterKickOffSetUp();
            Debug.Log("Two players connected. Transitioning to KickOff phase.");
            return;
        }

        if (CurrentMatchState.Value == MatchState.InPlay && TimeManager.Instance.MatchEndTime.Value > 0 && currentTime >= TimeManager.Instance.MatchEndTime.Value)
        {
            CurrentMatchState.Value = MatchState.GameOver;
            Debug.Log("Match time ended. Transitioning to GameOver phase.");
            PhaseEndTime.Value = currentTime + _gameLobbyConnectionWaitSeconds;
            return;
        }

        if (PhaseEndTime.Value <= 0) return;

        if (currentTime < PhaseEndTime.Value) return;

        PhaseEndTime.Value = 0;

        switch (CurrentMatchState.Value)
        {
            case MatchState.KickOff:
                CurrentMatchState.Value = MatchState.InPlay;
                Debug.Log("Kick off! Transitioning to InPlay phase.");
                PhaseEndTime.Value = 0;
                RegisterKickOff(_lastKickingOffTeam);
                break;
            case MatchState.GoalScored:
                CurrentMatchState.Value = MatchState.KickOff;
                Debug.Log($"Goal scored! Transitioning to KickOff phase.");  
                PhaseEndTime.Value = currentTime + _kickOffDuration;
                RegisterKickOffSetUp();
                break;
            case MatchState.GameOver:
                CurrentMatchState.Value = MatchState.ConnectingToLobby;
                if (NetworkManager.Singleton != null)
                {
                    Debug.Log("Switching Scenes!");
                    NetworkManager.Singleton.SceneManager.LoadScene("GameLobbyScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
                }
                break;
        }
    }

    public double GetTimeRemainingInCurrentPhase()
    {
        if (PhaseEndTime.Value <= 0) return 0;
        double timeRemaining = PhaseEndTime.Value - NetworkManager.Singleton.ServerTime.Time;
        return Mathf.Max(0, (float)timeRemaining);
    }
}
