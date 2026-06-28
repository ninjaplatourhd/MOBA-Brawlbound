using System;
using Unity.Collections;
using Unity.Netcode;

public struct BuildQueueItemNet : INetworkSerializable, IEquatable<BuildQueueItemNet>
{
    public const int TypeUnit = 0;
    public const int TypeUpgrade = 1;

    public int ItemType;

    public FixedString64Bytes UnitId;
    public FixedString64Bytes DisplayName;

    public int MineralCost;
    public int PowerUpkeep;

    public float BuildTime;
    public float RemainingTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ItemType);
        serializer.SerializeValue(ref UnitId);
        serializer.SerializeValue(ref DisplayName);
        serializer.SerializeValue(ref MineralCost);
        serializer.SerializeValue(ref PowerUpkeep);
        serializer.SerializeValue(ref BuildTime);
        serializer.SerializeValue(ref RemainingTime);
    }

    public bool Equals(BuildQueueItemNet other)
    {
        return ItemType == other.ItemType
            && UnitId.Equals(other.UnitId)
            && DisplayName.Equals(other.DisplayName)
            && MineralCost == other.MineralCost
            && PowerUpkeep == other.PowerUpkeep
            && BuildTime.Equals(other.BuildTime)
            && RemainingTime.Equals(other.RemainingTime);
    }
}