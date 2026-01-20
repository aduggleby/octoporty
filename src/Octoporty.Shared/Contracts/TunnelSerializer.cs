using MessagePack;

namespace Octoporty.Shared.Contracts;

public static class TunnelSerializer
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithCompression(MessagePackCompression.Lz4BlockArray);

    public static byte[] Serialize(TunnelMessage message)
    {
        return MessagePackSerializer.Serialize(message, Options);
    }

    public static TunnelMessage Deserialize(ReadOnlyMemory<byte> data)
    {
        return MessagePackSerializer.Deserialize<TunnelMessage>(data, Options);
    }

    public static TunnelMessage Deserialize(byte[] data)
    {
        return MessagePackSerializer.Deserialize<TunnelMessage>(data, Options);
    }
}
