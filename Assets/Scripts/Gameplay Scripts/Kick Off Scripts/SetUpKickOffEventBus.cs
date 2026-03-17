using System;

public static class SetUpKickOffEventBus
{
    public static event Action<SetUpKickOffEvent> OnSetUpKickOff;
    public static void RaiseKickOffSetup(SetUpKickOffEvent kickOff) => OnSetUpKickOff?.Invoke(kickOff);
}
