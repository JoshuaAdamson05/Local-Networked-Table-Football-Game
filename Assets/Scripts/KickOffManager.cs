using Unity.Netcode;
using UnityEngine;

public class KickOffManager : NetworkBehaviour
{
    [SerializeField] private FootballManager _footballManager;
    private int _lastProcessedSetupKickOffID = -1;
    private int _lastProcessedKickOffID = -1;
    private bool _wasBlueRodsAssigned = false;
    private bool _wasRedRodsAssigned = false;
    private RodInputManager _redRodManager;
    private RodInputManager _blueRodManager;

    private Vector2 RandomiseKickOffDirection()
    {
        float sign = Random.value < 0.5f ? -1f : 1f;
        return new Vector2(0, sign);
    }

    private void Update()
    {
        if (!IsServer) return;

        if (!_wasRedRodsAssigned)
        {
            _redRodManager = GameObject.FindGameObjectWithTag("RedRods")?.GetComponent<RodInputManager>();
            if (_redRodManager != null)
            {
                _wasRedRodsAssigned = true;
            }
        }

        if (!_wasBlueRodsAssigned)
        {
            _blueRodManager = GameObject.FindGameObjectWithTag("BlueRods")?.GetComponent<RodInputManager>();
            if (_blueRodManager != null)
            {
                _wasBlueRodsAssigned = true;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SetUpKickOffEventBus.OnSetUpKickOff += OnSetUpKickOff;
            KickOffEventBus.OnKickOff += OnKickOff;

            MatchStateManager matchStateManager = MatchStateManager.Instance;
            if (matchStateManager != null && matchStateManager.SetUpKickOffEvents != null && matchStateManager.SetUpKickOffEvents.Count > 0)
            {
                SetUpKickOffEvent lastKickOffEvent = matchStateManager.SetUpKickOffEvents[matchStateManager.SetUpKickOffEvents.Count - 1];
                OnSetUpKickOff(lastKickOffEvent);
            }

            if (matchStateManager != null && matchStateManager.KickOffEvents != null && matchStateManager.KickOffEvents.Count > 0)
            {
                KickOffEvent lastKickOffEvent = matchStateManager.KickOffEvents[matchStateManager.KickOffEvents.Count - 1];
                OnKickOff(lastKickOffEvent);
            }

            if (_footballManager == null)
            {
                Debug.LogError("FootballManager reference is not assigned in KickOffManager.");
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            SetUpKickOffEventBus.OnSetUpKickOff -= OnSetUpKickOff;
            KickOffEventBus.OnKickOff -= OnKickOff;
        }
    }

    private void OnSetUpKickOff(SetUpKickOffEvent setUpKickOff)
    {
        if (!IsServer) return;
        if (_footballManager == null) return;
        if (_blueRodManager == null || _redRodManager == null) return;
        if (setUpKickOff.KickOffID <= _lastProcessedSetupKickOffID) return;
        _lastProcessedSetupKickOffID = setUpKickOff.KickOffID;   
        _footballManager.SetVelocity(Vector2.zero);
        _footballManager.ResetFootballPosition(Vector2.zero);
        _redRodManager.ResetRodPosition();
        _blueRodManager.ResetRodPosition();
    }

    private void OnKickOff(KickOffEvent kickOff)
    {
        if (!IsServer) return;
        if (_footballManager == null) return;
        if (kickOff.KickOffID <= _lastProcessedKickOffID) return;
        _lastProcessedKickOffID = kickOff.KickOffID;

        Vector2 kickOffDirection = RandomiseKickOffDirection();
        if (kickOff.KickingOffTeam != TeamEnums.None)
        {
            kickOffDirection = kickOff.KickingOffTeam == TeamEnums.Red ? Vector2.up : Vector2.down;
        }    
        
        _footballManager.SetVelocity(kickOffDirection);
    }
}
