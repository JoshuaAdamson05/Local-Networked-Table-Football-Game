using UnityEngine;
using Unity.Netcode;
using System.Collections;
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
    private Queue<BallSnapshot> _clientBallSnapshots;
    private uint _lastProcessedTick;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkTickEventBus.OnServerNetworkTick += OnServerNetworkTick;
        }

        if (!IsServer && IsClient)
        {
            _clientBallSnapshots = new Queue<BallSnapshot>();
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
        Rigidbody2D footballRb;
        _football.TryGetComponent<Rigidbody2D>(out footballRb);
        BallSnapshot ballSnapshot = new BallSnapshot(footballRb.position, footballRb.linearVelocity, networkTick.TickID);
        SendFootballSnapshotClientRpc(ballSnapshot);
        Debug.Log($"Server sent ball Client RPC");
    }

    [ClientRpc]
    private void SendFootballSnapshotClientRpc(BallSnapshot ballSnapshot)
    {
        _clientBallSnapshots.Enqueue(ballSnapshot);
    }

    private void Update()
    {
        if (!IsServer && IsClient)
        {
            if (_football == null) return;
            while (_clientBallSnapshots.Count > 0)
            {
                BallSnapshot ballSnapshot = _clientBallSnapshots.Peek();
                if (ballSnapshot.Tick <= _lastProcessedTick)
                {
                    _clientBallSnapshots.Dequeue();
                }
                else
                {
                    ballSnapshot = _clientBallSnapshots.Dequeue();
                    _football.transform.position = ballSnapshot.Position;
                    _lastProcessedTick = ballSnapshot.Tick;
                    Debug.Log("Update ball position");
                }
            }
        }
    }
}
