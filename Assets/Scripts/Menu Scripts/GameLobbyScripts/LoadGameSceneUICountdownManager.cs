using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LoadGameSceneCountdownManager : NetworkBehaviour
{
    private int _lastProcessedGameSceneLoadCountdownId = -1;
    private double _countdownEndTime;
    [SerializeField] private GameObject _countdownCanvas;
    [SerializeField] private TextMeshProUGUI _countdownText;


    public override void OnNetworkSpawn()
    {
        if (_countdownCanvas == null)
            Debug.LogError("Countdown canvas is missing!");

        if (_countdownText == null)
            Debug.LogError("Countdown text is missing!");

        GameSceneLoadEventBus.OnGameSceneLoadCountdown += OnGameSceneLoadEvent;
    }

    public override void OnNetworkDespawn()
    {
        GameSceneLoadEventBus.OnGameSceneLoadCountdown -= OnGameSceneLoadEvent;
    }

    private void Update()
    {
        if (LobbyStateManager.Instance == null) return;
        if (GetRemainingKickOffCountdownTime() > 0)
        {
            if (LobbyStateManager.Instance.CurrentLobbyState.Value == LobbyState.ReadyToPlay)
            {
                _countdownCanvas.SetActive(true);
                FormatSeconds(GetRemainingKickOffCountdownTime());
            }
            else
            {
                _countdownCanvas.SetActive(false);
            }
        }
    }

    private void OnGameSceneLoadEvent(GameSceneLoadCountdownEvent countdownEvent)
    {
        if (countdownEvent.GameSceneLoadCountdownId > _lastProcessedGameSceneLoadCountdownId)
        {
            _lastProcessedGameSceneLoadCountdownId = countdownEvent.GameSceneLoadCountdownId;
            _countdownEndTime = LobbyStateManager.Instance.PhaseEndTime.Value;
        }
    }

    private double GetRemainingKickOffCountdownTime()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager is not available. Ensure that a NetworkManager is present in the scene.");
            return 0;
        }
        double currentTime = NetworkManager.Singleton.ServerTime.Time;
        return System.Math.Max(0.0, _countdownEndTime - currentTime);
    }

    private void FormatSeconds(double seconds)
    {
        int secondsInt = Mathf.CeilToInt((float)seconds);
        _countdownText.text = secondsInt.ToString();
    }
}
