namespace Bombd.Protocols;

public enum PacketType : byte
{
    // char Protocol
    // char Pad
    // short Pad
    // uint SequenceNumber
    // uint Pad
    // uint Secret
    // uint Pad
    Reset = 96,
    
    // char Protocol
    // char Pad
    // short Pad
    // uint SequenceNumber
    // uint LocalTime
    // uint Pad2
    KeepAlive = 97,
    
    // char Protocol
    // char Pad
    // ushort Checksum
    // uint SequenceNumber
    // uint SessionId
    // uint SecretNum
    // uint GameDataSequenceNumber
    Handshake = 98,
    
    // char Protocol
    // char AckingProtocol
    // short Pad
    // uint AckingSequenceNumber
    // uint LocalTime
    // uint Pad
    Ack = 99,

    // char Protocol
    // char GroupCompleteFlag
    // short GroupId
    // uint SequenceNumber
    // uint Checksum
    // uint Pad
    // byte Payload[1024]
    ReliableNetcodeData = 100,
    
    // char Protocol
    // char Source
    // short Destination
    // short PayloadBytes
    // ushort Checksum
    // byte Payload[1032]
    UnreliableGameData = 101,

    // char Protocol
    // char Source
    // char Destination
    // char GroupCompleteFlag
    // uint SequenceNumber
    // short GroupId
    // ushort GroupSizeBytes
    // ushort Checksum
    // short PayloadBytes
    // byte Payload[1024]
    ReliableGameData = 102,

    // char Protocol
    // char Source
    // short Destination
    // uint SequenceNumber
    // uint Pad
    // uint Pad
    // byte Payload[1024]
    VoipData = 103
}