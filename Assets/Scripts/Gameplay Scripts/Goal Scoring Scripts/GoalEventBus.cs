using System;

public static class GoalEventBus
{
    public static event Action<GoalEvent> OnGoalScored;
    public static void RaiseGoalScored(GoalEvent goal) => OnGoalScored?.Invoke(goal);
}
