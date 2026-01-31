using UnityEngine;
using Unity.Netcode;

public class RodMovementClamp : NetworkBehaviour
{
    private Transform pitchBounds;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private float padding = 0.5f;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        Transform transform = GameObject.FindGameObjectWithTag("PitchBounds").transform;
        pitchBounds = transform;
        if (pitchBounds == null)
        {
            Debug.LogError("PitchBounds object not found in the scene. Please ensure there is a GameObject with the 'PitchBounds' tag.");
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;
        if (pitchBounds == null) return;

        float clampedX = Mathf.Clamp(rb.position.x, 
            pitchBounds.position.x - pitchBounds.localScale.x / 2 + padding, 
            pitchBounds.position.x + pitchBounds.localScale.x / 2 - padding);

        rb.position = new Vector2(clampedX, rb.position.y);
    }
}
