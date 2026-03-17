using System;
using Unity.Netcode;
using UnityEngine;

public struct NetworkTickEvent : IEquatable<NetworkTickEvent>
{
    public uint TickID;
    public bool Equals(NetworkTickEvent other)
    {
        return TickID == other.TickID;
    }
    public override bool Equals(object obj)
    {
        return obj is NetworkTickEvent other && Equals(other);
    }
    public override int GetHashCode()
    {
        return TickID.GetHashCode();
    }
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref TickID);
    }
}
