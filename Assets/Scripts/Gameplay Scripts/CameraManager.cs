using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Camera _camera;

    private void Start()
    {
        if (_camera == null)
        {
            Debug.LogError("Camera reference is not assigned in CameraManager.");
            return;
        }  
        
        if (!NetworkManager.Singleton.IsHost)
            _camera.transform.Rotate(0, 0, 180);
    }
}
