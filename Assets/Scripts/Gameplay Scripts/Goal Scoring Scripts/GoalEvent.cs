using System;
using Unity.Netcode;
using UnityEngine;

public struct GoalEvent : IEquatable<GoalEvent>, INetworkSerializable
{
    public ushort GoalID;
    public TeamEnums ScoringTeam;

    public bool Equals(GoalEvent other)
    {
        return GoalID == other.GoalID && ScoringTeam.Equals(other.ScoringTeam);
    }

    public override bool Equals(object obj)
    {
        return obj is GoalEvent other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GoalID, ScoringTeam);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref GoalID);
        serializer.SerializeValue(ref ScoringTeam);
    }
}
