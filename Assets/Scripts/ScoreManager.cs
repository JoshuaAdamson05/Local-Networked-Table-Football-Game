using UnityEngine;
using Unity.Netcode;

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance { get; private set; }
    private int _lastProcessedGoalID = -1;

    public NetworkVariable<int> _redTeamScore = new NetworkVariable<int>
        (0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> _blueTeamScore = new NetworkVariable<int>
        (0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Multiple instances of ScoreManager detected. There should only be one ScoreManager in the scene.");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            GoalEventBus.OnGoalScored += OnGoalScored;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            GoalEventBus.OnGoalScored -= OnGoalScored;
        }
    }

    private void OnGoalScored(GoalEvent goal)
    {
        if (!IsServer) return;
        if (goal.GoalID <= _lastProcessedGoalID) return;

        _lastProcessedGoalID = goal.GoalID;

        if (goal.ScoringTeam == TeamEnums.Red)
        {
            _redTeamScore.Value++;
        }
        else if (goal.ScoringTeam == TeamEnums.Blue)
        {
            _blueTeamScore.Value++;
        }
    }
}
