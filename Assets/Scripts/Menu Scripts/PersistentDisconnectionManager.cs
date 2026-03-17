using UnityEngine;

public class PersistentDisconnectionManager : MonoBehaviour
{
    public static PersistentDisconnectionManager Instance;
    public string DisconnectionReason { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(Instance);
        }
        else
        {
            Debug.LogWarning("Persistent disconnection manager instance already exists!");
            Destroy(this.gameObject);
        }
    }

    public void SetDisconnectionReason(string reason)
    {
        DisconnectionReason = reason;
    }
}
