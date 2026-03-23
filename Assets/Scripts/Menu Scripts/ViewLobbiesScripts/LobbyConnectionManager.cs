using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyConnectionManager : MonoBehaviour
{
    [SerializeField] private float _timeoutSeconds = 5f;
    [SerializeField] private GameObject _persistentDisconnectionManagerPrefab;
    private float _timer = 0;

    private void Start()
    {
        _timer = _timeoutSeconds;
    }

    private void Update()
    {
        if (_timer <= 0)
        {
            if (_persistentDisconnectionManagerPrefab != null)
            {
                Instantiate(_persistentDisconnectionManagerPrefab);
                PersistentDisconnectionManager.Instance.SetDisconnectionReason("Cannot connect to lobby!");
                SceneManager.LoadScene("DisconnectedScene");
            }
            else
                Debug.LogError("Persistent disconnection manager prefab missing!");
        }
        else
        {
            _timer -= Time.deltaTime;
        }
    }
}
