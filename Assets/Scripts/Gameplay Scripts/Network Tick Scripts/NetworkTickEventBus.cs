using System;

public static class NetworkTickEventBus
{
    public static event Action<NetworkTickEvent> OnClientNetworkTick;
    public static event Action<NetworkTickEvent> OnServerNetworkTick;
    public static void RaiseServerNetworkTick(NetworkTickEvent networkTick) => OnServerNetworkTick?.Invoke(networkTick);
    public static void RaiseClientNetworkTick(NetworkTickEvent networkTick) => OnClientNetworkTick?.Invoke(networkTick);
}
