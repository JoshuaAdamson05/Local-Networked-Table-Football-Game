using UnityEngine;
using Unity.Netcode;
using TMPro;

public class UIScoreManager : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI _redTeamScoreText;
    [SerializeField] private TextMeshProUGUI _blueTeamScoreText;

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            ScoreManager.Instance._redTeamScore.OnValueChanged += OnRedTeamGoal;
            ScoreManager.Instance._blueTeamScore.OnValueChanged += OnBlueTeamGoal;

            if (_redTeamScoreText == null)
            {
                Debug.LogError("Red Team Score Text is not assigned. Please assign a TextMeshProUGUI component to redTeamScoreText.");
            }
            if (_blueTeamScoreText == null)
            {
                Debug.LogError("Blue Team Score Text is not assigned. Please assign a TextMeshProUGUI component to blueTeamScoreText.");
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            ScoreManager.Instance._redTeamScore.OnValueChanged -= OnRedTeamGoal;
            ScoreManager.Instance._blueTeamScore.OnValueChanged -= OnBlueTeamGoal;
        }
    }

    private void OnRedTeamGoal(int oldValue, int newValue)
    {
        if (_redTeamScoreText == null)
            return;

        _redTeamScoreText.text = newValue.ToString();
    }

    private void OnBlueTeamGoal(int oldValue, int newValue)
    {
        if (_blueTeamScoreText == null)
            return;

        _blueTeamScoreText.text = newValue.ToString();
    }
}
