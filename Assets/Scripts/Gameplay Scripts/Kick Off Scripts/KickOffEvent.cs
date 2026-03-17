using System;
using Unity.Netcode;
using UnityEngine;

public struct KickOffEvent : IEquatable<KickOffEvent>, INetworkSerializable
{
    public ushort KickOffID;
    public TeamEnums KickingOffTeam;
    public bool Equals(KickOffEvent other)
    {
        return KickOffID == other.KickOffID && KickingOffTeam.Equals(other.KickingOffTeam);
    }
    public override bool Equals(object obj)
    {
        return obj is KickOffEvent other && Equals(other);
    }
    public override int GetHashCode()
    {
        return HashCode.Combine(KickOffID, KickingOffTeam);
    }
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref KickOffID);
        serializer.SerializeValue(ref KickingOffTeam);
    }
}
