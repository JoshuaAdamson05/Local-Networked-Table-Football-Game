using UnityEngine;
using Unity.Netcode;
using TMPro;

public class UIScoreManager : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI _redTeamScoreText;
    [SerializeField] private TextMeshProUGUI _blueTeamScoreText;

    private NetworkVariable<int> _redTeamScore = new NetworkVariable<int>
        (0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _blueTeamScore = new NetworkVariable<int>
        (0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            _redTeamScore.OnValueChanged += OnRedTeamGoal;
            _blueTeamScore.OnValueChanged += OnBlueTeamGoal;

            if (_redTeamScoreText == null)
            {
                Debug.LogError("Red Team Score Text is not assigned. Please assign a TextMeshProUGUI component to redTeamScoreText.");
            }
            if (_blueTeamScoreText == null)
            {
                Debug.LogError("Blue Team Score Text is not assigned. Please assign a TextMeshProUGUI component to blueTeamScoreText.");
            }
        }

        if (IsServer)
        {
            GoalEventBus.OnGoalScored += OnGoalScored;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            _redTeamScore.OnValueChanged -= OnRedTeamGoal;
            _blueTeamScore.OnValueChanged -= OnBlueTeamGoal;
        }
        if (IsServer)
        {
            GoalEventBus.OnGoalScored -= OnGoalScored;
        }
    }

    private void OnRedTeamGoal(int oldValue, int newValue)
    {
        if (_redTeamScoreText == null)
            return;

        _redTeamScore.Value = newValue;
        _redTeamScoreText.text = newValue.ToString();
    }

    private void OnBlueTeamGoal(int oldValue, int newValue)
    {
        if (_blueTeamScoreText == null)
            return;

        _blueTeamScore.Value = newValue;
        _blueTeamScoreText.text = newValue.ToString();
    }

    private void OnGoalScored(GoalEvent goal)
    {
        if (!IsServer) return;

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
