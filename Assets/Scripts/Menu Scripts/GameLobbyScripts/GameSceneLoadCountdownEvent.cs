using System;
using Unity.Netcode;
using UnityEngine;

public struct GameSceneLoadCountdownEvent : IEquatable<GameSceneLoadCountdownEvent>, INetworkSerializable
{
    public ushort GameSceneLoadCountdownId;

    public bool Equals(GameSceneLoadCountdownEvent other)
    {
        return GameSceneLoadCountdownId == other.GameSceneLoadCountdownId;
    }

    public override bool Equals(object obj)
    {
        return obj is GameSceneLoadCountdownEvent other && Equals(other);
    }

    public override int GetHashCode()
    {
        return GameSceneLoadCountdownId.GetHashCode();
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref GameSceneLoadCountdownId);
    }
}
