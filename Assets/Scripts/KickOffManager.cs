using Unity.Netcode;
using UnityEngine;

public class KickOffManager : NetworkBehaviour
{
    [SerializeField] private FootballManager _footballManager;

    private Vector2 RandomiseKickOffDirection()
    {
        float sign = Random.value < 0.5f ? -1f : 1f;
        return new Vector2(0, sign);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            GoalEventBus.OnGoalScored += OnGoalScored;

            if (_footballManager == null)
            {
                Debug.LogError("FootballManager reference is not assigned in KickOffManager.");
            }
        }
    }

    public void OnClientConnected(ulong clientID)
    {
        if (!IsServer) return;
        if (_footballManager == null) return;

        if (NetworkManager.Singleton.ConnectedClientsIds.Count == 2)
        {
            Vector2 kickOffDirection = RandomiseKickOffDirection();
            _footballManager.SetVelocity(kickOffDirection);     
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        GoalEventBus.OnGoalScored -= OnGoalScored;
    }

    private void OnGoalScored(GoalEvent goal)
    {
        if (!IsServer) return;
        if (_footballManager == null) return;
        _footballManager.ResetFootballPosition(Vector2.zero);
        Vector2 kickOffDirection = goal.ScoringTeam == TeamEnums.Red ? Vector2.down : Vector2.up;
        _footballManager.SetVelocity(kickOffDirection);
    }
}
