using System;
using Unity.Netcode;
using UnityEngine;

public struct SetUpKickOffEvent : IEquatable<SetUpKickOffEvent>, INetworkSerializable
{
    public ushort SetUpKickOffID;
    public bool Equals(SetUpKickOffEvent other)
    {
        return SetUpKickOffID == other.SetUpKickOffID;
    }
    public override bool Equals(object obj)
    {
        return obj is SetUpKickOffEvent other && Equals(other);
    }
    public override int GetHashCode()
    {
        return SetUpKickOffID.GetHashCode();
    }
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SetUpKickOffID);
    }
}
