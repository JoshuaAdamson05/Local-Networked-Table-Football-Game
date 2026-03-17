using UnityEngine;
using Unity.Netcode;

public class TimeManager : NetworkBehaviour
{
    [SerializeField] private float _matchDuration = 180f;
    private int _lastProcessedKickOffID = -1;  
    private double _pauseStartTime;
    public NetworkVariable<double> MatchEndTime { get; private set; } = new NetworkVariable<double>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> MatchTimerPaused { get; private set; } = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public static TimeManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Multiple instances of TimeManager detected. There should only be one TimeManager in the scene.");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        KickOffEventBus.OnKickOff += OnKickOff;
        GoalEventBus.OnGoalScored += OnGoalScored;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        KickOffEventBus.OnKickOff -= OnKickOff;
        GoalEventBus.OnGoalScored -= OnGoalScored;
    }

    private void OnKickOff(KickOffEvent kickOff)
    {
        if (!IsServer) return;
        if (kickOff.KickOffID <= _lastProcessedKickOffID) return;
        _lastProcessedKickOffID = kickOff.KickOffID;

        double currentTime = NetworkManager.Singleton.ServerTime.Time;

        if (kickOff.KickingOffTeam == TeamEnums.None)
        {
            MatchEndTime.Value = currentTime + _matchDuration;
            return;
        }

        double pauseDuration = _pauseStartTime > 0 ? currentTime - _pauseStartTime : 0;
        MatchEndTime.Value += pauseDuration;
        MatchTimerPaused.Value = false;
        _pauseStartTime = 0;
    }

    private void OnGoalScored(GoalEvent goal)
    {
        if (!IsServer) return;
        MatchTimerPaused.Value = true;
        _pauseStartTime = NetworkManager.Singleton.ServerTime.Time;
    }
}
