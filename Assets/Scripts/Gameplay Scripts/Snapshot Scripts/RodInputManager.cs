using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public enum RodAuthorityMode
{
    ServerAuthoritative,
    ClientAuthoritative,
}

public struct ServerAuthoritativeRodSnapshot : INetworkSerializable
{
    public const float RodPositionScale = 500f;

    public ServerAuthoritativeRodSnapshot(Vector2 position, uint tick, uint resetId)
    {
        Position = Quantize(position.x, RodPositionScale);
        Tick = tick;
        ResetId = resetId;
    }

    public short Position;
    public uint Tick;
    public uint ResetId;

    public float DequantizedPosition => Position / RodPositionScale;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref ResetId);
    }

    private static short Quantize(float value, float scale)
    {
        int scaled = Mathf.RoundToInt(value * scale);
        return (short)Mathf.Clamp(scaled, short.MinValue, short.MaxValue);
    }
}
public struct ClientAuthoritativeRodSnapshot : INetworkSerializable
{
    public const float RodPositionScale = 500f;

    public ClientAuthoritativeRodSnapshot(Vector2 position, uint serverTick, uint processedClientTick, uint resetID)
    {
        Position = Quantize(position.x, RodPositionScale);
        ServerTick = serverTick;
        ProcessedClientTick = processedClientTick;
        ResetId = resetID;
    }

    public short Position;
    public uint ServerTick;
    public uint ProcessedClientTick;
    public uint ResetId;

    public float DequantizedPosition => Position / RodPositionScale;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref ServerTick);
        serializer.SerializeValue(ref ProcessedClientTick);
        serializer.SerializeValue(ref ResetId);
    }

    private static short Quantize(float value, float scale)
    {
        int scaled = Mathf.RoundToInt(value * scale);
        return (short)Mathf.Clamp(scaled, short.MinValue, short.MaxValue);
    }
}

public struct ClientInputSnapshot : INetworkSerializable
{
    public ClientInputSnapshot(float inputMoveValue, uint tick)
    {
        InputMoveValue = Quantize(inputMoveValue);
        Tick = tick;
    }

    public sbyte InputMoveValue;
    public uint Tick;
    public float DequantizeInputMoveValue => InputMoveValue / 127f;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T: IReaderWriter
    {
        serializer.SerializeValue(ref InputMoveValue);
        serializer.SerializeValue(ref Tick);
    }

    private static sbyte Quantize(float value)
    {
        return (sbyte)Mathf.RoundToInt(value * 127f);
    }
}

public class RodInputManager : NetworkBehaviour
{
    [SerializeField] private InputAction _moveAction;
    [SerializeField] private float _moveSpeed;
    [SerializeField] private GameObject _visualRodGameObject;
    [SerializeField] private GameObject _colliderRodGameObject;
    [SerializeField] private NetworkVariable<RodAuthorityMode> _authoritativeMode = new NetworkVariable<RodAuthorityMode>(RodAuthorityMode.ClientAuthoritative, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [SerializeField] private double _bufferWindowSeconds = 0.5;
    [SerializeField] private int _interpolationDelayMs = 100;
    [SerializeField] float _maxCorrectionDistance = 0.15f;
    private BoxCollider2D _pitchBounds;
    private float _clientMoveValue;
    private float _serverMoveValue;
    private Vector2 _rodPhysicsPosition;
    private uint _lastReceivedTick;
    private uint _lastProcessedTick;
    private uint _currentClientTick;
    private uint _lastReceivedHostRodTick;
    private uint _lastReceivedClientRodTick;
    private List<ServerAuthoritativeRodSnapshot> _serverSnapshots;
    private List<ClientAuthoritativeRodSnapshot> _reconciliationSnapshots;
    private List<InputState> _pendingInputs;
    private List<PredictedState> _predictedStates;
    private BoxCollider2D _visualRodBounds;
    private BoxCollider2D _colliderRodBounds;
    private bool _shouldCorrectClientPosition = false;
    private Rigidbody2D _colliderRb;
    private uint _delayTicks;
    private Vector2 _targetVisualPosition;
    private Vector2 _targetPhysicsPosition;
    private uint _resetId = 0;

    public struct InputState
    {
        public uint Tick;
        public float InputMoveValue;

        public InputState(uint tick, float inputMoveValue)
        {
            Tick = tick;
            InputMoveValue = inputMoveValue;
        }
    }

    public struct PredictedState
    {
        public uint Tick;
        public Vector2 Position;

        public PredictedState(uint tick, Vector2 position)
        {
            Tick = tick;
            Position = position;
        }
    }

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
        }
        else
            Debug.LogError("Visual rod game object missing");

        if (_colliderRodGameObject != null)
        {
            _colliderRodBounds = _colliderRodGameObject.GetComponentInChildren<BoxCollider2D>();
            if (_colliderRodBounds == null)
                Debug.LogError("Collider rod bounds missing");

            _colliderRb = _colliderRodGameObject.GetComponentInChildren<Rigidbody2D>();
            if (_colliderRb == null)
                Debug.LogError("Collider rb missing");
        }
        else
            Debug.LogError("Collider rod game object missing");

        if (!IsServer)
        {
            NetworkTickEventBus.OnClientNetworkTick += OnClientNetworkTick;
            _serverSnapshots = new List<ServerAuthoritativeRodSnapshot>();
            _reconciliationSnapshots = new List<ClientAuthoritativeRodSnapshot>();
            _pendingInputs = new List<InputState>();
            _predictedStates = new List<PredictedState>();
        }

        if (IsServer)
        {
            NetworkTickEventBus.OnServerNetworkTick += OnServerNetworkTick;
        }

        if (NetworkTickManager.Instance != null)
        {
            _delayTicks = NetworkTickManager.Instance.GetNumberOfTicksForDelay(_interpolationDelayMs);
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
        if (MatchStateManager.Instance == null) return;   
        if (MatchStateManager.Instance.CurrentMatchState.Value != MatchState.InPlay) return;
        if (IsOwner)
            _clientMoveValue = MatchStateManager.Instance.CurrentMatchState.Value == MatchState.InPlay ? _moveAction.ReadValue<float>() : 0f;

        if (!IsServer)
        {
            if (_authoritativeMode.Value == RodAuthorityMode.ServerAuthoritative)
            {
                if (IsOwner)
                    SubmitMoveRequestServerRpc(_clientMoveValue);

                ClientRodInterpolation();
            }
            else
            {
                if (IsOwner)
                {
                    //Reconciliation
                    if (_reconciliationSnapshots.Count == 0) return;

                    while (_reconciliationSnapshots.Count > 0)
                    {
                        ClientAuthoritativeRodSnapshot snapshot = _reconciliationSnapshots[0];
                        if (_lastProcessedTick < snapshot.ProcessedClientTick)
                        {             
                            while (_predictedStates.Count > 0 && _predictedStates[0].Tick < snapshot.ProcessedClientTick)
                            {
                                _predictedStates.RemoveAt(0);
                            }

                            if (_predictedStates.Count > 0 &&
                                _predictedStates[0].Tick == snapshot.ProcessedClientTick)
                            {
                                PredictedState predictedState = _predictedStates[0];

                                float errorDistance = Vector2.Distance(predictedState.Position, new Vector2(snapshot.DequantizedPosition, 0));

                                if (errorDistance >= _maxCorrectionDistance)
                                {
                                    _targetPhysicsPosition = new Vector2(snapshot.DequantizedPosition, 0);
                                    _shouldCorrectClientPosition = true;
                                }     
                                _predictedStates.RemoveAt(0);
                            }

                            _lastProcessedTick = snapshot.ProcessedClientTick;

                            while (_pendingInputs.Count > 0 && _pendingInputs[0].Tick <= snapshot.ProcessedClientTick)
                            {
                                _pendingInputs.RemoveAt(0);
                            }
                        }
                        _reconciliationSnapshots.RemoveAt(0);
                    }
                }
                else
                {
                    ClientRodInterpolation();
                }
            }
        }

        if (IsServer)
        {
            _targetVisualPosition = _colliderRodGameObject.transform.position;
        }
    }

    private void ClientRodInterpolation()
    {
        if (NetworkTickManager.Instance == null) return;

        CleanSnapshots(_serverSnapshots);
        if (_serverSnapshots.Count == 0) return;

        double predictedServerTick = NetworkTickManager.Instance.PredictedPreciseServerTick;
        double renderTick = predictedServerTick - _delayTicks;
        if (predictedServerTick <= _serverSnapshots[0].Tick)
            renderTick = 0;

        //Snap to position
        if (renderTick <= _serverSnapshots[0].Tick)
        {
            _targetVisualPosition = new Vector2(_serverSnapshots[0].DequantizedPosition, 0);
        }
        //Interpolate
        else if (_serverSnapshots.Count >= 2)
        {
            (uint, ServerAuthoritativeRodSnapshot) snapshotB;
            (uint, ServerAuthoritativeRodSnapshot) snapshotA;
            GetRenderBracketTicks(renderTick, _serverSnapshots, out snapshotB, out snapshotA);

            if (snapshotB.Item1 > snapshotA.Item1)
            {
                float t = (float)(renderTick - snapshotA.Item1) / (snapshotB.Item1 - snapshotA.Item1);
                t = Mathf.Clamp01(t);
                Vector2 position = Vector2.Lerp(new Vector2(snapshotA.Item2.DequantizedPosition, 0), new Vector2(snapshotB.Item2.DequantizedPosition, 0), t);
                _targetVisualPosition = position;
            }
            else
            {
                _targetVisualPosition = new Vector2(snapshotA.Item2.DequantizedPosition, 0);
            }
        }
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendRodSnapshotServerRpc(ClientInputSnapshot rodSnapshot)
    {
        if (!IsServer) return;

        if (rodSnapshot.Tick > _lastReceivedTick)
        {
            _serverMoveValue = rodSnapshot.DequantizeInputMoveValue;
            _lastReceivedTick = rodSnapshot.Tick;
        }
    }

    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendServerRodSnapshotClientRpc(ServerAuthoritativeRodSnapshot rodSnapshot)
    {
        if (IsServer) return;

        if (rodSnapshot.Tick > _lastReceivedHostRodTick && rodSnapshot.ResetId == _resetId)
        {
            _serverSnapshots.Add(rodSnapshot);
            _lastReceivedHostRodTick = rodSnapshot.Tick;
        }
    }

    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendClientRodSnapshotClientRpc(ClientAuthoritativeRodSnapshot rodSnapshot)
    {
        if (IsServer) return;

        if (_authoritativeMode.Value == RodAuthorityMode.ClientAuthoritative)
            if (rodSnapshot.ServerTick > _lastReceivedClientRodTick && rodSnapshot.ResetId == _resetId)
            {
                _reconciliationSnapshots.Add(rodSnapshot);
                _lastReceivedClientRodTick = rodSnapshot.ServerTick;
            }
    }

    private void GetRenderBracketTicks(double tick, List<ServerAuthoritativeRodSnapshot> rodSnapshots, out (uint, ServerAuthoritativeRodSnapshot) snapshotB, out (uint, ServerAuthoritativeRodSnapshot) snapshotA)
    {
        int index = -1;
        for (int i = 0; i < rodSnapshots.Count; i++)
        {
            ServerAuthoritativeRodSnapshot snapshot = rodSnapshots[i];
            if (snapshot.Tick >= tick)
            {
                index = i;
                break;
            }
        }
        if (index == -1)
            index = rodSnapshots.Count - 1;
        snapshotB = (rodSnapshots[index].Tick, rodSnapshots[index]);

        if (index <= 0)
            index = 1;
        snapshotA = (rodSnapshots[index - 1].Tick, rodSnapshots[index - 1]);
    }

    private void CleanSnapshots(List<ServerAuthoritativeRodSnapshot> rodSnapshots)
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
        if (MatchStateManager.Instance == null) return;
        if (_colliderRb == null) return;
        if (_colliderRodBounds == null) return;

        float clampedX, minX, maxX;
        switch (_authoritativeMode.Value)
        {
            case RodAuthorityMode.ServerAuthoritative:
                if (IsServer)
                {
                    if (MatchStateManager.Instance.CurrentMatchState.Value == MatchState.InPlay)
                    {
                        ClampPlayerPosition(out clampedX, out minX, out maxX, _colliderRodBounds, _colliderRb);
                        if (IsOwner)
                        {
                            ApplyPlayerMovement(minX, maxX, clampedX, _clientMoveValue, _colliderRb);
                        }
                        else
                        {
                            ApplyPlayerMovement(minX, maxX, clampedX, _serverMoveValue, _colliderRb);
                        }
                    }
                    _rodPhysicsPosition = _colliderRb.position;
                    _visualRodGameObject.transform.position = _rodPhysicsPosition;
                }
                else
                {
                    _visualRodGameObject.transform.position = _targetVisualPosition;
                }
                break;

            case RodAuthorityMode.ClientAuthoritative:
                if (IsOwner)
                {
                    if (_shouldCorrectClientPosition)
                    {
                        _colliderRb.position = _targetPhysicsPosition; 
                        int i = 0;
                        while (i < _pendingInputs.Count)
                        {
                            if (_currentClientTick > _pendingInputs[i].Tick)
                            {
                                ClampPlayerPosition(out clampedX, out minX, out maxX, _colliderRodBounds, _colliderRb);
                                ApplyPlayerMovement(minX, maxX, clampedX, _pendingInputs[i].InputMoveValue, _colliderRb);
                            }
                            else
                            {
                                break;
                            }
                            i++;
                        }
                        _shouldCorrectClientPosition = false;
                    }
                    if (MatchStateManager.Instance.CurrentMatchState.Value == MatchState.InPlay)
                    {
                        ClampPlayerPosition(out clampedX, out minX, out maxX, _colliderRodBounds, _colliderRb);
                        ApplyPlayerMovement(minX, maxX, clampedX, _clientMoveValue, _colliderRb);               
                    }
                    _rodPhysicsPosition = _colliderRb.position;    
                    _visualRodGameObject.transform.position = _colliderRb.position;

                    if (!IsServer)
                        _predictedStates.Add(new PredictedState(_currentClientTick, _rodPhysicsPosition));
                }
                else
                {
                    if (IsServer)
                    {
                        if (MatchStateManager.Instance.CurrentMatchState.Value == MatchState.InPlay)
                        {
                            ClampPlayerPosition(out clampedX, out minX, out maxX, _colliderRodBounds, _colliderRb);
                            ApplyPlayerMovement(minX, maxX, clampedX, _serverMoveValue, _colliderRb);
                        }
                        _rodPhysicsPosition = _colliderRb.position;
                        _visualRodGameObject.transform.position = _rodPhysicsPosition;
                        _lastProcessedTick = _lastReceivedTick; 
                    }
                    else
                    {
                        _visualRodGameObject.transform.position = _targetVisualPosition;
                    }
                }
                break;
        }
    }

    private void OnClientNetworkTick(NetworkTickEvent networkTick)
    {
        if (_authoritativeMode.Value == RodAuthorityMode.ServerAuthoritative) return;
        if (IsServer || !IsOwner) return;
        _currentClientTick = networkTick.TickID;
        float inputMoveValue = _clientMoveValue;
        ClientInputSnapshot rodSnapshot = new ClientInputSnapshot(inputMoveValue, networkTick.TickID);
        SendRodSnapshotServerRpc(rodSnapshot);
        InputState inputState = new InputState(networkTick.TickID, inputMoveValue);
        _pendingInputs.Add(inputState);
    }

    private void OnServerNetworkTick(NetworkTickEvent networkTick)
    {
        if (!IsServer) return;

        ServerAuthoritativeRodSnapshot saRodSnapshot = new ServerAuthoritativeRodSnapshot(_rodPhysicsPosition, networkTick.TickID, _resetId);
        SendServerRodSnapshotClientRpc(saRodSnapshot);

        if (_authoritativeMode.Value == RodAuthorityMode.ClientAuthoritative)
        {
            ClientAuthoritativeRodSnapshot rodSnapshot = new ClientAuthoritativeRodSnapshot(_rodPhysicsPosition, networkTick.TickID, _lastProcessedTick, _resetId);
            SendClientRodSnapshotClientRpc(rodSnapshot);
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
    private void ResetRodClientRpc(uint resetId)
    {
        if (_visualRodGameObject == null) return;
        if (_colliderRb == null) return;
        _colliderRb.MovePosition(Vector2.zero);
        _visualRodGameObject.transform.position = Vector2.zero;
        _rodPhysicsPosition = Vector2.zero;
        _targetVisualPosition = Vector2.zero;
        _targetPhysicsPosition = Vector2.zero;
        _shouldCorrectClientPosition = false;
        if (_serverSnapshots != null)
            _serverSnapshots.Clear();
        if (_reconciliationSnapshots != null)
            _reconciliationSnapshots.Clear();
        if (_predictedStates != null)
            _predictedStates.Clear();
        if (_pendingInputs != null)
            _pendingInputs.Clear();
        _lastReceivedTick = 0;
        _clientMoveValue = 0;
        _serverMoveValue = 0;
        _currentClientTick = 0;
        _lastProcessedTick = 0;
        _lastReceivedClientRodTick = 0;
        _lastReceivedHostRodTick = 0;
        _resetId = resetId;
    }

    public void ResetRodPosition()
    {
        if (_visualRodGameObject == null) return;
        if (_colliderRb == null) return;
        if (!IsServer) return;

        _colliderRb.MovePosition(Vector2.zero);
        _visualRodGameObject.transform.position = Vector2.zero;
        _rodPhysicsPosition = Vector2.zero;
        _targetVisualPosition = Vector2.zero;
        _targetPhysicsPosition = Vector2.zero;
        if (_serverSnapshots != null)
            _serverSnapshots.Clear();
        if (_reconciliationSnapshots != null)
            _reconciliationSnapshots.Clear();
        _lastReceivedTick = 0;       
        _resetId++;
        _shouldCorrectClientPosition = false;
        _clientMoveValue = 0;
        _serverMoveValue = 0;
        _currentClientTick = 0;
        _lastProcessedTick = 0;
        _lastReceivedClientRodTick = 0;
        _lastReceivedHostRodTick = 0;

        ResetRodClientRpc(_resetId);
    }

    public void SetAuthorityMode(RodAuthorityMode rodAuthorityMode)
    {
        _authoritativeMode.Value = rodAuthorityMode;
    }
}
