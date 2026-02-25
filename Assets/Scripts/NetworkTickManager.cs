using UnityEngine;
using Unity.Netcode;
using NUnit.Framework;
using System.Collections.Generic;

public class NetworkTickManager : NetworkBehaviour
{
    public static NetworkTickManager Instance { get; private set; }
    public uint ServerTick { get; private set; }
    public uint ClientTick { get; private set; }
    private double _nextTickTime;
    private double _lastTickTime = 0;
    private bool _clientTickInitialised = false;

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
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer && _serverStartTime.Value <= 0.0)
        {
            double currentTime = NetworkManager.Singleton.ServerTime.Time;
            if (currentTime > 0.0001)
            {
                _serverStartTime.Value = currentTime;
                _nextTickTime = currentTime + _tickInterval;
                ServerTick = 0;
            }
        }
    }

    private void Update()
    {
        // Ensure server start time is set before processing ticks
        if (_serverStartTime.Value <= 0.0)
        {
            double now = NetworkManager.Singleton.ServerTime.Time;
            if (now > 0.0001)
            {
                _serverStartTime.Value = now;
                _nextTickTime = now + _tickInterval;
                ServerTick = 0;
                _lastTickTime = 0;
                Debug.Log($"[Server] Tick start initialised (late) at {now:F6}");
            }
            return;
        }

        if (IsServer)
        {    
            double currentTime = NetworkManager.Singleton.ServerTime.Time;
            while (currentTime >= _nextTickTime)
            {          
                double lateBy = currentTime - _nextTickTime;

                _nextTickTime += _tickInterval;
                ServerTick++;
                NetworkTickEventBus.RaiseServerNetworkTick(new NetworkTickEvent { TickID = ServerTick });

                double dt = (_lastTickTime <= 0) ? 0 : (currentTime - _lastTickTime);
                _lastTickTime = currentTime;

                if (ServerTick % 30 == 0)
                    Debug.Log($"Server Tick: {ServerTick} | Delta Time: {dt:F6} | Late By: {lateBy:F6} | Target Interval {_tickInterval:F6}");
                currentTime = NetworkManager.Singleton.ServerTime.Time;
            }
        }

        if (IsClient && !IsServer)
        {
            if (!NetworkManager.Singleton.IsConnectedClient)
            {
                Debug.LogWarning("Client is not connected to a server. Cannot process ticks.");
                return;
            }

            if (!_clientTickInitialised)
            {
                if (_serverStartTime.Value <= 0) return;

                ClientTick = 0;
                _clientTickInitialised = true;
                Debug.Log($"Client tick initialised. Server start time: {_serverStartTime.Value:F6}");
            }

            uint target = PredictedServerTick;
            int processedTicks = 0;
            const int maxCatchUpTicksPerFrame = 5;

            while (ClientTick < target && processedTicks < maxCatchUpTicksPerFrame)
            {
                ClientTick++;
                processedTicks++;
                NetworkTickEventBus.RaiseClientNetworkTick(new NetworkTickEvent { TickID = ClientTick });
                if (ClientTick % 30 == 0)
                    Debug.Log($"Client Tick: {ClientTick} | Target Server Tick: {target} | Processed Ticks This Frame: {processedTicks}");
            }
        }
    }

    public uint PredictedServerTick
    {
        get
        {
            double startTime = _serverStartTime.Value;
            if (startTime <= 0)
                return 0;

            double elapsedTime = NetworkManager.Singleton.ServerTime.Time - startTime;
            if (elapsedTime < 0)
                return 0;

            return (uint)Mathf.FloorToInt((float)(elapsedTime / _tickInterval));
        }
    }
}
