using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

public enum RodAuthorityMode
{
    ServerAuthoritative,
    ClientAuthoritative,
}

public struct RodSnapshot : INetworkSerializable
{
    public RodSnapshot(Vector2 position, float moveValue, uint tick)
    {
        Position = position;
        MoveValue = moveValue;
        Tick = tick;
    }

    public Vector2 Position;
    public float MoveValue;
    public uint Tick;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref MoveValue);
        serializer.SerializeValue(ref Tick);
    }
}

public class RodInputManager : NetworkBehaviour
{
    [SerializeField] private InputAction _moveAction;
    [SerializeField] private float _moveSpeed;
    [SerializeField] private GameObject _visualRodGameObject;
    [SerializeField] private GameObject _colliderGameObject;
    [SerializeField] private RodAuthorityMode _authoritativeMode = RodAuthorityMode.ServerAuthoritative;
    [SerializeField] private double _bufferWindowSeconds = 0.5;
    [SerializeField] private int _interpolationDelayMs = 100;
    private BoxCollider2D _pitchBounds;
    private float _clientMoveValue;
    private float _serverMoveValue;
    private Vector2 _rodPhysicsPosition;
    private uint _lastReceivedTick;
    private List<RodSnapshot> _serverRodSnapshots;
    private List<RodSnapshot> _clientRodSnapshots;
    private BoxCollider2D _visualRodBounds;
    private BoxCollider2D _colliderRodBounds;
    private Rigidbody2D _visualRb;
    private Rigidbody2D _colliderRb;
    private uint _delayTicks;
    private Vector2 _targetPosition;

    public override void OnNetworkSpawn()
    {
        if (IsOwner) 
            _moveAction.Enable();

        GameObject boundsObject = GameObject.FindGameObjectWithTag("PitchBounds");
        if (boundsObject != null)
        {
            boundsObject.TryGetComponent<BoxCollider2D>(out BoxCollider2D collider);
            _pitchBounds = collider;
        }

        if (_pitchBounds == null)
        {
            Debug.LogError("PitchBounds object not found in the scene. Please ensure there is a GameObject with the 'PitchBounds' tag.");
        }

        if (_visualRodGameObject != null)
        {
            _visualRodBounds = _visualRodGameObject.GetComponentInChildren<BoxCollider2D>();
            if (_visualRodBounds == null)
                Debug.LogError("Visual rod bounds missing");

            _visualRb = _visualRodGameObject.GetComponentInChildren<Rigidbody2D>();
            if (_visualRb == null)
                Debug.LogError("Visual rb missing");
        }
        else
            Debug.LogError("Visual rod game object missing");

        if (_colliderGameObject != null)
        {
            _colliderRodBounds = _colliderGameObject.GetComponentInChildren<BoxCollider2D>();
            if (_colliderRodBounds == null)
                Debug.LogError("Collider rod bounds missing");

            _colliderRb = _colliderGameObject.GetComponentInChildren<Rigidbody2D>();
            if (_colliderRb == null)
                Debug.LogError("Collider rb missing");
        }
        else
            Debug.LogError("Collider rod game object missing");

        if (!IsServer)
        {
            NetworkTickEventBus.OnClientNetworkTick += OnClientNetworkTick;
            _clientRodSnapshots = new List<RodSnapshot>();
            if (NetworkTickManager.Instance != null)
            {
                _delayTicks = NetworkTickManager.Instance.GetNumberOfTicksForDelay(_interpolationDelayMs);
            }
        }

        if (IsServer)
        {
            NetworkTickEventBus.OnServerNetworkTick += OnServerNetworkTick;
            _serverRodSnapshots = new List<RodSnapshot>();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            _moveAction.Disable();
        }

        if (!IsServer)
            NetworkTickEventBus.OnClientNetworkTick -= OnClientNetworkTick;

        if (IsServer)
            NetworkTickEventBus.OnServerNetworkTick -= OnServerNetworkTick;
    }

    void Update()
    {
        if (!IsServer)
        {
            if (MatchStateManager.Instance == null) return;
            _clientMoveValue = MatchStateManager.Instance.CurrentMatchState.Value == MatchState.InPlay ? _moveAction.ReadValue<float>() : 0f;
            if (_authoritativeMode == RodAuthorityMode.ServerAuthoritative)
            {
                if (IsOwner)
                    SubmitMoveRequestServerRpc(_clientMoveValue);
            }
            if (NetworkManager.Singleton == null) return;
            if (NetworkTickManager.Instance == null) return;
            if (_clientRodSnapshots.Count == 0) return;

            double predictedServerTick = NetworkTickManager.Instance.PredictedPreciseServerTick;
            double renderTick = predictedServerTick - _delayTicks;
            if (predictedServerTick <= _clientRodSnapshots[0].Tick)
                renderTick = 0;
            CleanSnapshots(_clientRodSnapshots);
            if (_clientRodSnapshots.Count == 0) return;

            //Snap to position
            if (renderTick <= _clientRodSnapshots[0].Tick)
            {
                _targetPosition = _clientRodSnapshots[0].Position;
            }
            //Interpolate
            else if (_clientRodSnapshots.Count >= 2)
            {
                (uint, RodSnapshot) snapshotB;
                (uint, RodSnapshot) snapshotA;
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
            _visualRb.MovePosition(_targetPosition);
        }

        if (IsServer)
        {
            _clientMoveValue = MatchStateManager.Instance.CurrentMatchState.Value == MatchState.InPlay ? _moveAction.ReadValue<float>() : 0f;
            _visualRb.MovePosition(_colliderRb.position);
        }
    }

    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendRodClientRpc(RodSnapshot rodSnapshot)
    {
        if (IsServer) return;

        if (rodSnapshot.Tick > _lastReceivedTick)
        {
            _clientRodSnapshots.Add(rodSnapshot);
            _lastReceivedTick = rodSnapshot.Tick;
        }
    }

    private void GetRenderBracketTicks(double tick, out (uint, RodSnapshot) snapshotB, out (uint, RodSnapshot) snapshotA)
    {
        int index = -1;
        for (int i = 0; i < _clientRodSnapshots.Count; i++)
        {
            RodSnapshot snapshot = _clientRodSnapshots[i];
            if (snapshot.Tick >= tick)
            {
                index = i;
                break;
            }
        }
        if (index == -1)
            index = _clientRodSnapshots.Count - 1;
        snapshotB = (_clientRodSnapshots[index].Tick, _clientRodSnapshots[index]);

        if (index <= 0)
            index = 1;
        snapshotA = (_clientRodSnapshots[index - 1].Tick, _clientRodSnapshots[index - 1]);
    }

    private void CleanSnapshots(List<RodSnapshot> rodSnapshots)
    {
        if (NetworkTickManager.Instance == null) return;
        if (rodSnapshots.Count == 0) return;

        int bufferTicksInt = NetworkTickManager.Instance.GetBufferTicks(_bufferWindowSeconds);
        uint bufferTicks = (uint)Math.Max(0, bufferTicksInt);

        uint newestTick = rodSnapshots[^1].Tick;
        uint minAllowedTick = newestTick > bufferTicks ? newestTick - bufferTicks : 0;

        while (rodSnapshots.Count > 0 && rodSnapshots[0].Tick < minAllowedTick)
        {
            rodSnapshots.RemoveAt(0);
        }
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void SubmitMoveRequestServerRpc(float moveValue)
    {
        _serverMoveValue = moveValue;
    }

    void FixedUpdate()
    {
        if (MatchStateManager.Instance.CurrentMatchState.Value != MatchState.InPlay) return;
        if (_colliderRb == null) return;
        if (_colliderRodBounds == null) return;

        float clampedX, minX, maxX;
        switch (_authoritativeMode)
        {
            case RodAuthorityMode.ServerAuthoritative:
                if (!IsServer) return;

                if (IsOwner)
                {
                    ClampPlayerPosition(out clampedX, out minX, out maxX, _colliderRodBounds, _colliderRb);
                    ApplyPlayerMovement(minX, maxX, clampedX, _clientMoveValue, _colliderRb);
                    _rodPhysicsPosition = _colliderRb.position;
                }
                else
                {
                    ClampPlayerPosition(out clampedX, out minX, out maxX, _colliderRodBounds, _colliderRb);
                    ApplyPlayerMovement(minX, maxX, clampedX, _serverMoveValue, _colliderRb);
                    _rodPhysicsPosition = _colliderRb.position;
                }
                break;
            case RodAuthorityMode.ClientAuthoritative:
                if (IsServer || !IsOwner) return;
                ClampPlayerPosition(out clampedX, out minX, out maxX, _colliderRodBounds, _colliderRb);
                ApplyPlayerMovement(minX, maxX, clampedX, _clientMoveValue, _colliderRb);
                _rodPhysicsPosition = _colliderRb.position;
                break;
        }
    }

    private void OnClientNetworkTick(NetworkTickEvent networkTick)
    {
        if (_authoritativeMode == RodAuthorityMode.ServerAuthoritative) return;
        if (IsServer || !IsOwner) return;
        RodSnapshot rodSnapshot = new RodSnapshot(_rodPhysicsPosition, _clientMoveValue, networkTick.TickID);
        SendRodSnapshotServerRpc(rodSnapshot);
    }

    private void OnServerNetworkTick(NetworkTickEvent networkTick)
    {
        if (!IsServer) return;
        RodSnapshot rodSnapshot = new RodSnapshot(_rodPhysicsPosition, _serverMoveValue, networkTick.TickID);
        SendRodClientRpc(rodSnapshot);
    }

    [ServerRpc]
    private void SendRodSnapshotServerRpc(RodSnapshot rodSnapshot)
    {
        if (!IsServer) return;
        if (rodSnapshot.Tick > _lastReceivedTick)
        {
            _serverRodSnapshots.Add(rodSnapshot);
            _lastReceivedTick = rodSnapshot.Tick;
        }
    }

    private void ClampPlayerPosition(out float clampedX, out float minX, out float maxX, BoxCollider2D rodBounds, Rigidbody2D rb)
    {
        minX = _pitchBounds.bounds.min.x + rodBounds.bounds.extents.x;
        maxX = _pitchBounds.bounds.max.x - rodBounds.bounds.extents.x;

        clampedX = Mathf.Clamp(rb.position.x, minX, maxX);
        rb.position = new Vector2(clampedX, rb.position.y);   
    }

    private void ApplyPlayerMovement(float minX, float maxX, float clampedX, float moveValue, Rigidbody2D rb)
    {
        float deltaPosition = moveValue * _moveSpeed * Time.fixedDeltaTime;
        bool atMinX = Mathf.Abs(clampedX - minX) < 0.0001f;
        bool atMaxX = Mathf.Abs(clampedX - maxX) < 0.0001f;

        if ((atMinX && deltaPosition < 0) || (atMaxX && deltaPosition > 0))
        {
            deltaPosition = 0f;
        }

        Vector2 newPos = rb.position;
        newPos.x += deltaPosition;
        rb.MovePosition(newPos);
    }

    [ClientRpc]
    private void ResetRodClientRpc()
    {
        if (_visualRb == null) return;
        if (_colliderRb == null) return;
        if (IsServer) return;
        _colliderRb.MovePosition(Vector2.zero);
        _visualRb.MovePosition(Vector2.zero);
        _rodPhysicsPosition = Vector2.zero;
    }

    public void ResetRodPosition()
    {
        if (_visualRb == null) return;
        if (_colliderRb == null) return;
        if (!IsServer) return;

        if (_authoritativeMode == RodAuthorityMode.ServerAuthoritative)
        {
            _colliderRb.MovePosition(Vector2.zero);
            _visualRb.MovePosition(Vector2.zero);
            _rodPhysicsPosition = Vector2.zero;
        }
        else
        {
            ResetRodClientRpc();
        }
    }
}
