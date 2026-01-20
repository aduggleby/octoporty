using MessagePack;

namespace Octoporty.Shared.Contracts;

public static class TunnelSerializer
{
    // MEDIUM-04: Maximum message size to prevent memory exhaustion attacks
    public const int MaxMessageSize = 16 * 1024 * 1024; // 16MB max per message

    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithCompression(MessagePackCompression.Lz4BlockArray)
        .WithSecurity(MessagePackSecurity.UntrustedData); // Additional safety for untrusted data

    public static byte[] Serialize(TunnelMessage message)
    {
        return MessagePackSerializer.Serialize(message, Options);
    }

    public static TunnelMessage Deserialize(ReadOnlyMemory<byte> data)
    {
        if (data.Length > MaxMessageSize)
        {
            throw new InvalidOperationException(
                $"Message size {data.Length} exceeds maximum allowed size of {MaxMessageSize} bytes");
        }

        return MessagePackSerializer.Deserialize<TunnelMessage>(data, Options);
    }

    public static TunnelMessage Deserialize(byte[] data)
    {
        if (data.Length > MaxMessageSize)
        {
            throw new InvalidOperationException(
                $"Message size {data.Length} exceeds maximum allowed size of {MaxMessageSize} bytes");
        }

        return MessagePackSerializer.Deserialize<TunnelMessage>(data, Options);
    }
}
