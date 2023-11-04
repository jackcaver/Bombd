using System.Buffers.Binary;
using System.Runtime.Serialization;
using System.Text;
using Bombd.Serialization;

namespace Bombd.Types.Authentication;

public class TicketBlob : INetworkReadable
{
    private readonly List<TicketParameter> _parameters = new();
    public ArraySegment<byte> BlobData { get; private set; }

    public void Read(NetworkReader reader)
    {
        if (reader.ReadInt16() >> 8 != 0x30) throw new SerializationException("Expected blob header");

        BlobData = reader.ReadSegment(reader.ReadUInt16());
        int offset = 0;
        while (offset != BlobData.Count)
        {
            var type = (TicketData)((BlobData[offset++] << 8) | BlobData[offset++]);
            int size = (BlobData[offset++] << 8) | BlobData[offset++];
            var data = new ArraySegment<byte>(BlobData.Array!, BlobData.Offset + offset, size);
            _parameters.Add(new TicketParameter
            {
                DataType = type,
                Data = data
            });
            offset += size;
        }
    }

    public T ReadParameter<T>(int index)
    {
        TicketParameter parameter = _parameters[index];
        object? value = null;
        switch (parameter.DataType)
        {
            case TicketData.U32:
                value = BinaryPrimitives.ReadUInt32BigEndian(parameter.Data);
                break;
            case TicketData.U64:
                value = BinaryPrimitives.ReadUInt64BigEndian(parameter.Data);
                break;
            case TicketData.String:
                value = Encoding.ASCII.GetString(parameter.Data).Trim('\0');
                break;
            case TicketData.Time:
                long milliseconds = BinaryPrimitives.ReadInt64BigEndian(parameter.Data);
                value = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).Date;
                break;
            case TicketData.Binary:
                if (typeof(T) == typeof(string))
                {
                    value = Encoding.ASCII.GetString(parameter.Data).Trim('\0');
                    break;
                }

                byte[] buffer = new byte[parameter.Data.Count];
                parameter.Data.CopyTo(buffer);

                break;
        }

        ;

        return (T)Convert.ChangeType(value, typeof(T));
    }
}