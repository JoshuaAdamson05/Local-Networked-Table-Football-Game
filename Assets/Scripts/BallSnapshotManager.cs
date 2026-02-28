using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System;

public struct BallSnapshot : INetworkSerializable
{
    public BallSnapshot(Vector2 pos, Vector2 vel, uint tick)
    {
        Position = pos;
        Velocity = vel;
        Tick = tick;
    }

    public Vector2 Position;
    public Vector2 Velocity;
    public uint Tick;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Velocity);
        serializer.SerializeValue(ref Tick);
    }
}

public class BallSnapshotManager : NetworkBehaviour
{
    [SerializeField] private GameObject _football;
    [SerializeField] private double _bufferWindowSeconds = 0.5;
    [SerializeField] private int _interpolationDelayMs = 100;
    [SerializeField] private uint _maxExtrapolationTicks = 3;
    private uint _delayTicks;
    private List<BallSnapshot> _clientBallSnapshots;
    private uint _lastReceivedTick;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkTickEventBus.OnServerNetworkTick += OnServerNetworkTick;
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
        }

        if (_football == null)
        {
            Debug.LogError("Football not set on ball snapshot manager.");
        }

        Debug.Log($"Ball manager spawned on {IsServer} (Server) or on Client {(IsClient && !IsServer)}");
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
        Rigidbody2D footballRb;
        _football.TryGetComponent<Rigidbody2D>(out footballRb);
        BallSnapshot ballSnapshot = new BallSnapshot(footballRb.position, footballRb.linearVelocity, networkTick.TickID);
        SendFootballSnapshotClientRpc(ballSnapshot);
    }

    [ClientRpc]
    private void SendFootballSnapshotClientRpc(BallSnapshot ballSnapshot)
    {
        if (IsServer) return;
        if (ballSnapshot.Tick > _lastReceivedTick)
        {
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

        //Snap to position
        if (renderTick <= _clientBallSnapshots[0].Tick)
        {
            _football.transform.position = _clientBallSnapshots[0].Position;
        }
        //Extrapolate
        else if (renderTick >= _clientBallSnapshots[^1].Tick)
        {
            BallSnapshot lastSnapshot = _clientBallSnapshots[^1];
            double tickDelta = renderTick - lastSnapshot.Tick;
            tickDelta = Math.Clamp(tickDelta, 0, _maxExtrapolationTicks);
            float timeDelta = NetworkTickManager.Instance.GetTimeDelta(tickDelta);
            Vector2 predictedPosition = lastSnapshot.Position + lastSnapshot.Velocity * timeDelta;
            _football.transform.position = predictedPosition;
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
                _football.transform.position = position;
            }
        }
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

    //private void Update()
    //{
    //    if (!IsServer && IsClient)
    //    {
    //        if (_football == null) return;
    //        while (_clientBallSnapshots.Count > 0)
    //        {
    //            BallSnapshot ballSnapshot = _clientBallSnapshots.Peek();
    //            if (ballSnapshot.Tick <= _lastProcessedTick)
    //            {
    //                _clientBallSnapshots.Dequeue();
    //            }
    //            else
    //            {
    //                ballSnapshot = _clientBallSnapshots.Dequeue();
    //                _football.transform.position = ballSnapshot.Position;
    //                _lastProcessedTick = ballSnapshot.Tick;
    //                Debug.Log("Update ball position");
    //            }
    //        }
    //    }
    //}
}
