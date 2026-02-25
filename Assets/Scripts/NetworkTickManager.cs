using UnityEngine;
using Unity.Netcode;

public class NetworkTickManager : NetworkBehaviour
{
    public static NetworkTickManager Instance { get; private set; }
    public int ServerTick { get; private set; }
    private double _nextTickTime;

    [SerializeField] private double _tickRateHertz = 30;
    private double _tickInterval => 1.0 / _tickRateHertz;

    private NetworkVariable<double> _serverStartTime
        = new NetworkVariable<double>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Multiple instances of NetworkTickManager detected! There should only be one NetworkTickManager in the scene.");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    { 
        if (!IsServer) return;
        _serverStartTime.Value = NetworkManager.Singleton.ServerTime.Time;
        _nextTickTime = NetworkManager.Singleton.ServerTime.Time + _tickInterval;
        ServerTick = 0;
    }

    private double _lastTickTime = 0;

    private void Update()
    {
        if (!IsServer) return;

        while (NetworkManager.Singleton.ServerTime.Time >= _nextTickTime)
        {
            double lateBy = NetworkManager.Singleton.ServerTime.Time - _nextTickTime;       
            double dt = (ServerTick == 1) ? 0 : (NetworkManager.Singleton.ServerTime.Time - _lastTickTime);

            _nextTickTime += _tickInterval;
            ServerTick++;
            _lastTickTime = NetworkManager.Singleton.ServerTime.Time;

            Debug.Log($"Server Tick: {ServerTick} | Delta Time: {dt:F6} | Late By: {lateBy:F6} | Target Interval {_tickInterval:F6}");
        }
    }

    public int PredictedServerTick
    {
        get
        {
            if (_serverStartTime.Equals(0))
                return 0;

            double elapsedTime = NetworkManager.Singleton.ServerTime.Time - _serverStartTime.Value;
            if (elapsedTime < 0) return 0;
            return Mathf.FloorToInt((float)(elapsedTime / _tickInterval));
        }
    }
}
