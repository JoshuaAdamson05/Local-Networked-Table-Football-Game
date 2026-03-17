using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DisconnectionSceneManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _disconnectionReasonText;

    private void Start()
    {
        if (_disconnectionReasonText == null)
        {
            Debug.LogError("Disconnection reason text is missing!");
            return;
        }

        if (PersistentDisconnectionManager.Instance == null)
        {
            Debug.LogError("Persistent disconnection manager is missing!");
            return;
        }

        _disconnectionReasonText.text = PersistentDisconnectionManager.Instance.DisconnectionReason;
        DestroyNetworkManager();
    }

    private void DestroyNetworkManager()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }
        else
            Debug.LogWarning("Network manager instance not found!");
    }

    public void ReturnToMainMenu()
    {
        if (PersistentDisconnectionManager.Instance != null)
        {
            Destroy(PersistentDisconnectionManager.Instance.gameObject);
        }

        SceneManager.LoadScene("MainMenuScene");
    }
}
