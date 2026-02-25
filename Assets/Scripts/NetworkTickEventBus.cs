using System;

public static class NetworkTickEventBus
{
    public static event Action<NetworkTickEvent> OnNetworkTick;
    public static void RaiseServerNetworkTick(NetworkTickEvent networkTick) => OnNetworkTick?.Invoke(networkTick);
    public static void RaiseClientNetworkTick(NetworkTickEvent networkTick) => OnNetworkTick?.Invoke(networkTick);
}
