using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System;

public struct BallSnapshot : INetworkSerializable
{
    public const float PositionScale = 1000f;
    public const float VelocityScale = 100f;

    public short PosX, PosY, VelX, VelY;

    public BallSnapshot(Vector2 pos, Vector2 vel, uint tick)
    {
        PosX = Quantize(pos.x, PositionScale);
        PosY = Quantize(pos.y, PositionScale);
        VelX = Quantize(vel.x, VelocityScale);
        VelY = Quantize(vel.y, VelocityScale);
        Tick = tick;
    }

    public Vector2 Position => new Vector2(PosX / PositionScale, PosY / PositionScale);
    public Vector2 Velocity => new Vector2(VelX / VelocityScale, VelY / VelocityScale);
    public uint Tick;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PosX);
        serializer.SerializeValue(ref PosY);
        serializer.SerializeValue(ref VelX);
        serializer.SerializeValue(ref VelY);
        serializer.SerializeValue(ref Tick);
    }

    private static short Quantize(float value, float scale)
    {
        int scaledValue = Mathf.RoundToInt(value * scale);
        return (short)Mathf.Clamp(scaledValue, short.MinValue, short.MaxValue);
    }
}

public class BallSnapshotManager : NetworkBehaviour
{
    [SerializeField] private GameObject _football;
    [SerializeField] private double _bufferWindowSeconds = 0.5;
    [SerializeField] private int _interpolationDelayMs = 100;
    [SerializeField] private uint _maxExtrapolationTicks = 3;
    private Rigidbody2D _footballRb;
    private float _maxSnapDistance;
    private uint _delayTicks;
    private List<BallSnapshot> _clientBallSnapshots;
    private uint _lastReceivedTick;
    private Vector2 _targetPosition;
    private Vector2 _lastPhysicPosition;
    private Vector2 _lastPhysicsVelocity;

    public override void OnNetworkSpawn()
    {
        if (_football == null)
        {
            Debug.LogError("Football not set on ball snapshot manager.");
        }

        if (IsServer)
        {
            NetworkTickEventBus.OnServerNetworkTick += OnServerNetworkTick;
            _football.TryGetComponent<Rigidbody2D>(out _footballRb);
        }

        if (!IsServer)
        {
            _clientBallSnapshots = new List<BallSnapshot>();
            if (NetworkTickManager.Instance == null)
            {
                Debug.LogError("Ball snapshot couldnt find network tick manager instance");
                return;
            }

            _delayTicks = NetworkTickManager.Instance.GetNumberOfTicksForDelay(_interpolationDelayMs);

            CircleCollider2D collider = _football.GetComponent<CircleCollider2D>();
            float worldRadius = collider.radius * Mathf.Max(_football.transform.lossyScale.x, _football.transform.lossyScale.y);
            _maxSnapDistance = worldRadius * 1.25f;
        }

        Debug.Log($"Ball manager spawned on {IsServer} (Server) or on Client {(!IsServer)}");
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkTickEventBus.OnServerNetworkTick -= OnServerNetworkTick;
        }
    }

    private void OnServerNetworkTick(NetworkTickEvent networkTick)
    {
        if (!IsServer) return;
        if (_football == null) return;
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.ConnectedClients.Count == 0) return;
        BallSnapshot ballSnapshot = new BallSnapshot(_lastPhysicPosition, _lastPhysicsVelocity, networkTick.TickID);
        SendFootballSnapshotClientRpc(ballSnapshot);
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        _lastPhysicPosition = _footballRb.position;
        _lastPhysicsVelocity = _footballRb.linearVelocity;
    }

    //Stop retransmits blocking newer data
    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendFootballSnapshotClientRpc(BallSnapshot ballSnapshot)
    {
        if (IsServer) return;
        if (ballSnapshot.Tick > _lastReceivedTick)
        {
            if (NetworkMetricsLogger.Instance != null)
            {
                if (_lastReceivedTick != 0)
                {
                    uint gap = ballSnapshot.Tick - _lastReceivedTick;
                    NetworkMetricsLogger.Instance.BallMetricData.RecordSnapshotGap(gap);
                }
            }

            _clientBallSnapshots.Add(ballSnapshot);
            _lastReceivedTick = ballSnapshot.Tick;
        }    
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null) return;             
        if (NetworkTickManager.Instance == null) return;
        if (!NetworkManager.Singleton.IsConnectedClient) return;
        if (IsServer) return;

        double predictedServerTick = NetworkTickManager.Instance.PredictedPreciseServerTick;
        double renderTick = predictedServerTick - _delayTicks;
        if (predictedServerTick <= _delayTicks)
            renderTick = 0;
        CleanSnapshots();
        if (_clientBallSnapshots.Count == 0) return;

        if (NetworkMetricsLogger.Instance != null && NetworkMetricsLogger.Instance.IsLogging)
        {
            bool isExtrapolating = renderTick >= _clientBallSnapshots[^1].Tick;
            NetworkMetricsLogger.Instance.BallMetricData.RecordTickSample(isExtrapolating);
        }

        //Snap to position
        if (renderTick <= _clientBallSnapshots[0].Tick)
        {
            if (NetworkMetricsLogger.Instance != null && NetworkMetricsLogger.Instance.IsLogging)
            {
                NetworkMetricsLogger.Instance.BallMetricData.RecordSnap();
            }

            _targetPosition = _clientBallSnapshots[0].Position;
        }
        //Extrapolate
        else if (renderTick >= _clientBallSnapshots[^1].Tick)
        {
            BallSnapshot lastSnapshot = _clientBallSnapshots[^1];
            double tickDelta = renderTick - lastSnapshot.Tick;
            tickDelta = Math.Clamp(tickDelta, 0, _maxExtrapolationTicks);
            float timeDelta = NetworkTickManager.Instance.GetTimeDelta(tickDelta);
            Vector2 predictedPosition = lastSnapshot.Position + lastSnapshot.Velocity * timeDelta;
            _targetPosition = predictedPosition;
        }
        //Interpolate
        else if (_clientBallSnapshots.Count >= 2)
        {
            (uint, BallSnapshot) snapshotB; 
            (uint, BallSnapshot) snapshotA;
            GetRenderBracketTicks(renderTick, out snapshotB, out snapshotA);

            if (snapshotB.Item1 > snapshotA.Item1)
            {
                float t = (float)((renderTick - snapshotA.Item1) / (snapshotB.Item1 - snapshotA.Item1));
                t = Math.Clamp(t, 0, 1);
                Vector2 position = Vector2.Lerp(snapshotA.Item2.Position, snapshotB.Item2.Position, t);
                _targetPosition = position;
            }
            else
            {
                _targetPosition = snapshotA.Item2.Position;
            }
        }

        Vector2 currentPosition = _football.transform.position;
        float errorDistance = Vector2.Distance(currentPosition, _targetPosition);

        if (NetworkMetricsLogger.Instance != null && NetworkMetricsLogger.Instance.IsLogging)
        {
            NetworkMetricsLogger.Instance.BallMetricData.RecordError(errorDistance);
        }

        _football.transform.position = _targetPosition;
    }

    private void CleanSnapshots()
    {
        if (IsServer) return;
        if (NetworkTickManager.Instance == null) return;
        if (_clientBallSnapshots.Count == 0) return;

        int bufferTicksInt = NetworkTickManager.Instance.GetBufferTicks(_bufferWindowSeconds);
        uint bufferTicks = (uint)Math.Max(0, bufferTicksInt);

        uint newestTick = _clientBallSnapshots[^1].Tick;
        uint minAllowedTick = newestTick > bufferTicks ? newestTick - bufferTicks : 0;

        while (_clientBallSnapshots.Count > 0 && _clientBallSnapshots[0].Tick < minAllowedTick)
        {
            _clientBallSnapshots.RemoveAt(0);
        }
    }

    private void GetRenderBracketTicks(double tick, out (uint, BallSnapshot) snapshotB, out (uint, BallSnapshot) snapshotA)
    {
        int index = -1;
        for (int i = 0; i < _clientBallSnapshots.Count; i++)
        {
            BallSnapshot snapshot = _clientBallSnapshots[i];
            if (snapshot.Tick >= tick)
            {
                index = i;
                break;
            }
        }
        if (index == -1)
            index = _clientBallSnapshots.Count - 1;
        snapshotB = (_clientBallSnapshots[index].Tick, _clientBallSnapshots[index]);

        if (index <= 0)
            index = 1;
        snapshotA = (_clientBallSnapshots[index - 1].Tick, _clientBallSnapshots[index - 1]);
    }
}
