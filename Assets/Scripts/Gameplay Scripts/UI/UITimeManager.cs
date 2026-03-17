using UnityEngine;
using Unity.Netcode;
using TMPro;

public class UITimeManager : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI _timeText;
    private double _matchEndTime;
    private double _pausedTimeRemaining;

    public override void OnNetworkSpawn()
    {
        if (!IsClient) return;

        TimeManager.Instance.MatchTimerPaused.OnValueChanged += OnMatchTimerPausedChanged;
        TimeManager.Instance.MatchEndTime.OnValueChanged += OnMatchTimeChanged;
        if (_timeText == null)
        {
            Debug.LogError("Time Text is not assigned. Please assign a TextMeshProUGUI component to timeText.");
        }

        _matchEndTime = TimeManager.Instance.MatchEndTime.Value;
        if (TimeManager.Instance.MatchTimerPaused.Value)
        {
            _pausedTimeRemaining = GetRemainingSeconds();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsClient) return;
        TimeManager.Instance.MatchTimerPaused.OnValueChanged -= OnMatchTimerPausedChanged;
        TimeManager.Instance.MatchEndTime.OnValueChanged -= OnMatchTimeChanged;
    }

    private double GetRemainingSeconds()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager is not available. Ensure that a NetworkManager is present in the scene.");
            return 0;
        }

        double currentTime = NetworkManager.Singleton.ServerTime.Time;
        return System.Math.Max(0.0, TimeManager.Instance.MatchEndTime.Value - currentTime);
    }

    private void OnMatchTimerPausedChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            _pausedTimeRemaining = GetRemainingSeconds();
        }
    }

    private void OnMatchTimeChanged(double oldValue, double newValue)
    {
        _matchEndTime = newValue;
        if (TimeManager.Instance.MatchTimerPaused.Value)
        {
            _pausedTimeRemaining = GetRemainingSeconds();
        }
    }

    private void FormatTime(double time)
    {
        int minutes = Mathf.FloorToInt((float)time / 60);
        int seconds = Mathf.FloorToInt((float)time % 60);
        _timeText.text = $"{minutes:00}:{seconds:00}";
    }

    private void Update()
    {
        if (!IsClient) return;
        if (_timeText == null) return;

        if (TimeManager.Instance.MatchTimerPaused.Value)
        {
            FormatTime(_pausedTimeRemaining);
        }
        else
        {
            FormatTime(GetRemainingSeconds());
        }
    }
}
