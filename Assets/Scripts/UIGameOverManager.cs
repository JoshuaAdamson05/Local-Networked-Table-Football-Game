using UnityEngine;
using Unity.Netcode;

public class UIGameOverManager : NetworkBehaviour
{
    [SerializeField] private GameObject _gameOverPanel;
    public override void OnNetworkSpawn()
    {
        if (!IsClient) return;

        if (_gameOverPanel == null)
        {
            Debug.LogError("GameOverPanel reference is not set in the UIGameOverManager.");
            return;
        }
        _gameOverPanel.SetActive(false);
    }

    private void Update()
    {
        if (!IsClient) return;
        if (MatchStateManager.Instance != null)
        {
            if (MatchStateManager.Instance.CurrentMatchState.Value == MatchState.GameOver)
            {
                _gameOverPanel.SetActive(true);
            }
            else
            {
                _gameOverPanel.SetActive(false);
            }
        }
    }
}
