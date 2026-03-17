using UnityEngine;
using Unity.Netcode;

public class UIGoalScored : NetworkBehaviour
{
    [SerializeField] private GameObject _goalScoredPanel;
    private int _lastProcessedGoalID = -1;

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            GoalEventBus.OnGoalScored += OnGoalScored;
            SetUpKickOffEventBus.OnSetUpKickOff += OnSetUpKickOff;
            if (_goalScoredPanel == null)
            {
                Debug.LogError("Goal scored panel is missing from UIGoalScored script, please add the goal scored panel!");
                return;
            }

            _goalScoredPanel.SetActive(false);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            GoalEventBus.OnGoalScored -= OnGoalScored;
            SetUpKickOffEventBus.OnSetUpKickOff -= OnSetUpKickOff;
        }
    }

    private void OnGoalScored(GoalEvent goalEvent)
    {
        if (goalEvent.GoalID <= _lastProcessedGoalID)
        {
            Debug.LogWarning($"Received a GoalScored event with GoalID {goalEvent.GoalID} that has already been processed. Ignoring this event.");
            return;
        }
        _lastProcessedGoalID = goalEvent.GoalID;
        _goalScoredPanel.SetActive(true);
    }

    private void OnSetUpKickOff(SetUpKickOffEvent setUpKickOff)
    {
        _goalScoredPanel.SetActive(false);
    }
}
