using UnityEngine;

public class FpsManager : MonoBehaviour
{
    [SerializeField] private int _targetFps = 60;

    private void Awake()
    {
        Application.targetFrameRate = _targetFps;
    }
}
