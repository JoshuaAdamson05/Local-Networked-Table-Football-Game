using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

public class RodInputManager : NetworkBehaviour
{
    [SerializeField] private InputAction _moveAction;
    [SerializeField] private Rigidbody2D _rb;
    [SerializeField] private float _moveSpeed;
    [SerializeField] private BoxCollider2D _rodBounds;
    [SerializeField] private NetworkTransform _networkTransform;
    private BoxCollider2D _pitchBounds;
    private float _moveValue;
    private float _serverMoveValue;
    private bool _serverAuthoritative => _networkTransform.IsServerAuthoritative();
    private bool _hasMoveAuthority;

    public override void OnNetworkSpawn()
    {
        _hasMoveAuthority = _serverAuthoritative ? IsServer : IsOwner;

        if (IsOwner) _moveAction.Enable();

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

        if (_rodBounds == null)
        {
            Debug.LogError("RodBounds is not assigned. Please assign a BoxCollider2D to rodBounds.");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        _moveAction.Disable();
    }

    void Update()
    {  
        if (!IsOwner) return;
        _moveValue = _moveAction.ReadValue<float>();
        if (_serverAuthoritative)
        {
            SubmitMoveRequestServerRpc(_moveValue);
        }
    }

    [ServerRpc]
    private void SubmitMoveRequestServerRpc(float moveValue)
    {
        _serverMoveValue = moveValue;
    }

    void FixedUpdate()
    {
        if (!_hasMoveAuthority) return;
        if (_pitchBounds == null || _rodBounds == null) return;
        float moveValue = _serverAuthoritative ? _serverMoveValue : _moveValue;
        ClampPlayerPosition(out float clampedX, out float minX, out float maxX);
        ApplyPlayerMovement(minX, maxX, clampedX, moveValue);
    }

    private void ClampPlayerPosition(out float clampedX, out float minX, out float maxX)
    {
        minX = _pitchBounds.bounds.min.x + _rodBounds.bounds.extents.x;
        maxX = _pitchBounds.bounds.max.x - _rodBounds.bounds.extents.x;

        clampedX = Mathf.Clamp(_rb.position.x, minX, maxX);
        _rb.position = new Vector2(clampedX, _rb.position.y);   
    }

    private void ApplyPlayerMovement(float minX, float maxX, float clampedX, float moveValue)
    {
        float deltaPosition = moveValue * _moveSpeed * Time.fixedDeltaTime;
        bool atMinX = Mathf.Abs(clampedX - minX) < 0.0001f;
        bool atMaxX = Mathf.Abs(clampedX - maxX) < 0.0001f;

        if ((atMinX && deltaPosition < 0) || (atMaxX && deltaPosition > 0))
        {
            deltaPosition = 0f;
        }

        Vector2 newPos = _rb.position;
        newPos.x += deltaPosition;
        _rb.MovePosition(newPos);
    }
}
