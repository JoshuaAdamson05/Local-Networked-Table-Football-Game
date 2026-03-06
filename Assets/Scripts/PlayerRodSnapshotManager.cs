using UnityEngine;
using Unity.Netcode;

public class PlayerRodSnapshotManager : NetworkBehaviour
{
    [SerializeField] private GameObject _playerRod;
    [SerializeField] private double _bufferWindowSeconds = 0.5;
    [SerializeField] private int _interpolationDelayMs = 100;
}
