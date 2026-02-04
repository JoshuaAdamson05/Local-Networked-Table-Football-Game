using UnityEngine;
using Unity.Netcode;
using NUnit.Framework;
using System.Collections.Generic;

public class FootballManager : NetworkBehaviour
{
    [SerializeField] private Rigidbody2D _footballRb;
    [SerializeField] private CircleCollider2D _footballCollider;
    [SerializeField] private float _footballSpeed;
    private BoxCollider2D _pitchBounds;
    private Vector2 _lastVelocity;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Debug.Log("FootballManager spawned on the server.");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
        else
        {
            Debug.Log("FootballManager spawned on a client.");
        }

        GameObject boundsObject = GameObject.FindGameObjectWithTag("PitchBounds");
        if (boundsObject != null)
        {
            boundsObject.TryGetComponent<BoxCollider2D>(out BoxCollider2D collider);
            _pitchBounds = collider;
        }

        if (_pitchBounds == null)
        {
            Debug.Log("Pitchbounds could not be found!");
        }

        if (_footballCollider == null)
        {
            Debug.Log("Football collider not added!");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (_footballRb == null || _pitchBounds == null || _footballCollider == null) return;

        if (_footballRb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            _footballRb.linearVelocity = _footballRb.linearVelocity.normalized * _footballSpeed;
        }
        _lastVelocity = _footballRb.linearVelocity;

        ReflectBallAtPitchBounds();
    }

    public void OnClientConnected(ulong clientID)
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton.ConnectedClientsIds.Count == 2)
        {
            _footballRb.linearVelocity = RandomiseKickOffDirection() * _footballSpeed;
        }
    }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsServer) return;
        if (_footballRb == null) return;

        Debug.Log($"Football collided with {collision.gameObject.name}");

        ContactPoint2D contactPoint = collision.GetContact(0);

        Vector2 collisionNormal = contactPoint.normal.normalized;

        Vector2 inDirection = _lastVelocity.sqrMagnitude > 0.0001f ? 
            _lastVelocity.normalized :
            _footballRb.linearVelocity.normalized;

        Vector2 outDirection = Vector2.Reflect(inDirection, collisionNormal).normalized;

        _footballRb.linearVelocity = outDirection * _footballSpeed;

        Debug.Log($"Collision normal: {collisionNormal}");
    }

    private void ReflectBallAtPitchBounds()
    {
        float minX = _pitchBounds.bounds.min.x + _footballCollider.bounds.extents.x;
        float maxX = _pitchBounds.bounds.max.x - _footballCollider.bounds.extents.x;

        //Left Bound
        if (_footballRb.position.x < minX && _footballRb.linearVelocity.x < 0f)
        {
            Vector2 velocity = _lastVelocity;
            velocity.x = -velocity.x;
            Vector2 pos = _footballRb.position;
            pos.x = minX;
            _footballRb.linearVelocity = velocity;
            _footballRb.position = pos;
        }
        //Right Bound
        else if (_footballRb.position.x > maxX && _footballRb.linearVelocity.x > 0f)
        {
            Vector2 velocity = _lastVelocity;
            velocity.x = -velocity.x;
            Vector2 pos = _footballRb.position;
            pos.x = maxX;
            _footballRb.linearVelocity = velocity;
            _footballRb.position = pos;
        }
    }

    private Vector2 RandomiseKickOffDirection()
    {
        float sign = Random.value < 0.5f ? -1f : 1f;
        return new Vector2(0, sign);
    }
}
