using System;
using Unity.Netcode;
using UnityEngine;

public struct SetUpKickOffEvent : IEquatable<SetUpKickOffEvent>, INetworkSerializable
{
    public ushort KickOffID;
    public bool Equals(SetUpKickOffEvent other)
    {
        return KickOffID == other.KickOffID;
    }
    public override bool Equals(object obj)
    {
        return obj is SetUpKickOffEvent other && Equals(other);
    }
    public override int GetHashCode()
    {
        return KickOffID.GetHashCode();
    }
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref KickOffID);
    }
}
