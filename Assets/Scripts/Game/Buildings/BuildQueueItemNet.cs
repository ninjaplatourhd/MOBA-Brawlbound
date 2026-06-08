using System;
using Unity.Collections;
using Unity.Netcode;

public struct BuildQueueItemNet : INetworkSerializable, IEquatable<BuildQueueItemNet>
{
    public FixedString64Bytes UnitId;
    public FixedString64Bytes DisplayName;
    public float BuildTime;
    public float RemainingTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref UnitId);
        serializer.SerializeValue(ref DisplayName);
        serializer.SerializeValue(ref BuildTime);
        serializer.SerializeValue(ref RemainingTime);
    }

    public bool Equals(BuildQueueItemNet other)
    {
        return UnitId.Equals(other.UnitId) &&
               DisplayName.Equals(other.DisplayName) &&
               BuildTime.Equals(other.BuildTime) &&
               RemainingTime.Equals(other.RemainingTime);
    }
}