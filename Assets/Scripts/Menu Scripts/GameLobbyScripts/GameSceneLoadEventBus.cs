using System;

public static class GameSceneLoadEventBus
{
    public static event Action<GameSceneLoadCountdownEvent> OnGameSceneLoadCountdown;
    public static void RaiseGameSceneLoadCountdownEvent(GameSceneLoadCountdownEvent countdownEvent) => OnGameSceneLoadCountdown?.Invoke(countdownEvent);
}
