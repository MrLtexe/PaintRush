using Unity.Netcode;

public struct PlayerLobbyData : INetworkSerializable, System.IEquatable<PlayerLobbyData>
{
    public ulong ClientId;
    public int TeamId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref TeamId);
    }

    public bool Equals(PlayerLobbyData other) => ClientId == other.ClientId;
}
