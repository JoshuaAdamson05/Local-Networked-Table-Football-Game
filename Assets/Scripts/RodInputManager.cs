using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class RodInputManager : NetworkBehaviour
{
    [SerializeField] private InputAction moveAction;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private float moveSpeed;
    [SerializeField] private BoxCollider2D rodBounds;
    private BoxCollider2D pitchBounds;
    private float _moveValue;

    public override void OnNetworkSpawn()
    {
        rb.gravityScale = 0f;
        if (!IsOwner) return;

        moveAction.Enable();

        GameObject boundsObject = GameObject.FindGameObjectWithTag("PitchBounds");
        if (boundsObject != null)
        {
            boundsObject.TryGetComponent<BoxCollider2D>(out BoxCollider2D collider);
            pitchBounds = collider;
        }

        if (pitchBounds == null)
        {
            Debug.LogError("PitchBounds object not found in the scene. Please ensure there is a GameObject with the 'PitchBounds' tag.");
        }

        Debug.Log($"spawn: owner={IsOwner} server={IsServer} clientId={NetworkManager.Singleton.LocalClientId} ownerId={OwnerClientId}");
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        moveAction.Disable();
    }

    void Update()
    {
        if (!IsOwner) return;
        _moveValue = moveAction.ReadValue<float>();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;
        if (pitchBounds == null || rodBounds == null) return;

        // Clamp position within bounds
        float minX = pitchBounds.bounds.min.x + rodBounds.bounds.extents.x;
        float maxX = pitchBounds.bounds.max.x - rodBounds.bounds.extents.x;

        float clampedX = Mathf.Clamp(rb.position.x, minX, maxX);
        rb.position = new Vector2(clampedX, rb.position.y);

        // Apply movement
        float desiredVelocityX = _moveValue * moveSpeed;
        bool atMinX = Mathf.Abs(clampedX - minX) < 0.0001f;
        bool atMaxX = Mathf.Abs(clampedX - maxX) < 0.0001f;

        if ((atMinX && desiredVelocityX < 0) || (atMaxX && desiredVelocityX > 0))
        {
            desiredVelocityX = 0f;
        }

        rb.linearVelocityX = desiredVelocityX;

        Debug.Log($"x={rb.position.x:F2} min={minX:F2} max={maxX:F2} rodExt={rodBounds.bounds.extents.x:F2} pitchMin={pitchBounds.bounds.min.x:F2} pitchMax={pitchBounds.bounds.max.x:F2}");
    }
}
