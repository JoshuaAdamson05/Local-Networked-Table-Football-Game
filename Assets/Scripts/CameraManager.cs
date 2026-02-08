using Unity.Netcode;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Camera _camera;

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
        else
        {
            Debug.LogError("NetworkManager is not found in the scene. Please ensure it is present and properly set up.");
        }      
        
        if (_camera == null)
        {
            Debug.LogError("Camera reference is not assigned in CameraManager.");
            return;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        if (!NetworkManager.Singleton.IsHost)
            _camera.transform.Rotate(0, 0, 180);
    }
}
