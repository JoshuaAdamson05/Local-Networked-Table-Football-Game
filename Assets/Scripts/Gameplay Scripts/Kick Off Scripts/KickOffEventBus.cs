using System;

public static class KickOffEventBus
{
    public static event Action<KickOffEvent> OnKickOff;
    public static void RaiseKickOff(KickOffEvent kickOff) => OnKickOff?.Invoke(kickOff);
}
