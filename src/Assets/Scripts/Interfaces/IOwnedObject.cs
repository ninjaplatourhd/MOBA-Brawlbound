public interface IOwnedObject
{
    ulong OwnerClientId { get; }
    bool BelongsToLocalPlayer();
}