using UnityEngine;
using Unity.Netcode;
using TMPro;

public class UIKickOffCountdownManager : NetworkBehaviour
{
    [SerializeField] private GameObject _kickOffCountdownPanel;
    [SerializeField] private TextMeshProUGUI _countdownText;
    private double _kickOffCountdownEndTime;
    private int _lastProcessedSetUpKickOffID = -1;

    public override void OnNetworkSpawn()
    {
        if (!IsClient) return;
        if (_kickOffCountdownPanel == null)
        {
            Debug.LogError("KickOffCountdownPanel reference is not set in the UIKickOffCountdownManager.");
            return;
        }
        if (_countdownText == null)
        {
            Debug.LogError("CountdownText reference is not set in the UIKickOffCountdownManager.");
            return;
        }
        _kickOffCountdownPanel.SetActive(false);

        SetUpKickOffEventBus.OnSetUpKickOff += OnKickOffSetUp;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsClient) return;
        SetUpKickOffEventBus.OnSetUpKickOff -= OnKickOffSetUp;
    }

    private void OnKickOffSetUp(SetUpKickOffEvent setUpKickOff)
    {
        if (setUpKickOff.SetUpKickOffID <= _lastProcessedSetUpKickOffID)
        {
            Debug.LogWarning($"Received a SetUpKickOff event with SetUpKickOffID {setUpKickOff.SetUpKickOffID} that has already been processed. Ignoring this event.");
            return;
        }
        _lastProcessedSetUpKickOffID = setUpKickOff.SetUpKickOffID;
        _kickOffCountdownEndTime = MatchStateManager.Instance.PhaseEndTime.Value;
    }

    private double GetRemainingKickOffCountdownTime()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager is not available. Ensure that a NetworkManager is present in the scene.");
            return 0;
        }
        double currentTime = NetworkManager.Singleton.ServerTime.Time;
        return System.Math.Max(0.0, _kickOffCountdownEndTime - currentTime);
    }

    private void FormatSeconds(double seconds)
    {
        int secondsInt = Mathf.CeilToInt((float)seconds);
        _countdownText.text = secondsInt.ToString();
    }

    private void Update()
    {
        if (!IsClient) return;
        if (MatchStateManager.Instance == null) return;
        if (GetRemainingKickOffCountdownTime() > 0)
        {
            if (MatchStateManager.Instance.CurrentMatchState.Value == MatchState.KickOff)
            {
                _kickOffCountdownPanel.SetActive(true);
                FormatSeconds(GetRemainingKickOffCountdownTime());
            }
        }
        else
        {
            _kickOffCountdownPanel.SetActive(false);
        }
    }
}
