using System;
using Unity.Netcode;

[Serializable]
public struct PlayerGameData : INetworkSerializable, IEquatable<PlayerGameData>
{
    public ulong ClientId;

    public int Minerals;

    public int PowerProduced;
    public int PowerUsed;

    public int TechTier;

    public bool IsDefeated;

    public int PowerAvailable => PowerProduced - PowerUsed;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Minerals);
        serializer.SerializeValue(ref PowerProduced);
        serializer.SerializeValue(ref PowerUsed);
        serializer.SerializeValue(ref TechTier);
        serializer.SerializeValue(ref IsDefeated);
    }

    public bool Equals(PlayerGameData other)
    {
        return ClientId == other.ClientId
            && Minerals == other.Minerals
            && PowerProduced == other.PowerProduced
            && PowerUsed == other.PowerUsed
            && TechTier == other.TechTier
            && IsDefeated == other.IsDefeated;
    }
}