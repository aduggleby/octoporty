// MessageType.cs
// Defines the byte-sized enum for all tunnel protocol message types.
// Used as discriminator in MessagePack serialization to identify message kind.

namespace Octoporty.Shared.Contracts;

public enum MessageType : byte
{
    Auth = 1,
    AuthResult = 2,
    ConfigSync = 3,
    ConfigAck = 4,
    Heartbeat = 5,
    HeartbeatAck = 6,
    Request = 7,
    Response = 8,
    RequestBodyChunk = 9,
    ResponseBodyChunk = 10,
    Disconnect = 11,
    UpdateRequest = 12,
    UpdateResponse = 13,
    Error = 255
}
