/* Copyright 2022-2023 Cinegy GmbH.

  Licensed under the Apache License, Version 2.0 (the "License");
  you may not use this file except in compliance with the License.
  You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.
*/

using System;
using System.IO;
using Cinegy.TsDecoder.Video;

namespace Cinegy.VisionKlvDecoder;

public unsafe class CinegyTimestamp
{
    //format is:
    //KEY (16-bytes), LOCK Byte (with some reserved bits to make sure there are no phantom start codes - not actually used / tested), VALUE (11 bytes, with some skipped bytes again to stop phantom start codes)
    //VALUE is microseconds (or nanoseconds) in UNIX time, once it is bit-shifted and the emulation prevention codes skipped

    private static readonly byte[] KeyCngyMicro = "CNGYmicrosectime"u8.ToArray();
    private static readonly byte[] KeyCngyNano  = "CNGYnanosecstime"u8.ToArray();
    
    private DateTime _timeStamp = DateTime.MinValue;

    private CinegyTimestamp(){}

    public CinegyTimestamp(CinegyTimestampType type, ulong timestamp, byte timeLock = 0x1F)
    {
        Timestamp = timestamp;
        TimestampType = type;
        TimeLock = timeLock;
    }

    public CinegyTimestamp(CinegyTimestampType type, DateTime? dateTime = null, byte timeLock = 0x1F)
    {
        var dto = new DateTimeOffset(dateTime ??= DateTime.UtcNow);
        if (type == CinegyTimestampType.Nanosecond)
        {
            Timestamp = (ulong)(dto.ToUnixTimeMilliseconds() * 1000 * 1000);
        }
        else
        {
            Timestamp = (ulong)(dto.ToUnixTimeMilliseconds() * 1000);
        }
        
        TimestampType = type;
        TimeLock = timeLock;
    }

    public static CinegyTimestamp Create(SeiMessage message)
    {
        if (message.Type != 5) return null;
        
        if (message.PayloadSize != 28)
        {
            return null;
        }

        return Create(message.Payload);
    }

    public static CinegyTimestamp Create(byte[] seiPayload)
    {
        var payloadKeySpan = new Span<byte>(seiPayload[..16]);
        var cinegyMicroKeySpan = new Span<byte>(KeyCngyMicro);
        var cinegyNanoKeySpan = new Span<byte>(KeyCngyNano);

        var timestampType = CinegyTimestampType.Unspecified;
            
        if(payloadKeySpan.SequenceEqual(cinegyMicroKeySpan))
        {
            timestampType = CinegyTimestampType.Microsecond;
        }

        if(payloadKeySpan.SequenceEqual(cinegyNanoKeySpan))
        {
            //TODO: Complete testing with a sample of Nano timestamping
#if RELEASE
                throw new NotSupportedException("Nano-precision timestamps are untested, and are therefore set to throw this exception in Release builds");
#endif
            timestampType = CinegyTimestampType.Nanosecond;
        }

        if (timestampType == CinegyTimestampType.Unspecified) return null;

        var stampSpan = new ReadOnlySpan<byte>(seiPayload[17..28]);
        var stampLong = (ulong)stampSpan[0] << 56;
        stampLong += (ulong)stampSpan[1] << 48;
        stampLong += (ulong)stampSpan[3] << 40;
        stampLong += (ulong)stampSpan[4] << 32;
        stampLong += (ulong)stampSpan[6] << 24;
        stampLong += (ulong)stampSpan[7] << 16;
        stampLong += (ulong)stampSpan[9] << 8;
        stampLong += stampSpan[10];

        var newObj = new CinegyTimestamp
        {
            TimeLock = seiPayload[16],
            Timestamp = stampLong,
            TimestampType = timestampType
        };
            
        return newObj;
    }
        
    public byte TimeLock { get; set; }

    public ulong Timestamp { get; set; }

    public CinegyTimestampType TimestampType { get; private init; }

    public DateTime DateTimeStamp
    {
        get
        {
            if (TimestampType == CinegyTimestampType.Nanosecond)
            {
                return DateTime.UnixEpoch.AddMicroseconds((int)(Timestamp/1000));
            }

            return DateTime.UnixEpoch.AddMicroseconds(Timestamp);
        }
        set
        {
            var dto = new DateTimeOffset(value);
            if (TimestampType == CinegyTimestampType.Nanosecond)
            {
                Timestamp = (ulong)(dto.ToUnixTimeMilliseconds() * 1000 * 1000);
            }
            else
            {
                Timestamp = (ulong)(dto.ToUnixTimeMilliseconds() * 1000);
            }
        }
    }

    public byte[] GetBytes()
    {
        Span<byte> payloadSpan = stackalloc byte[28];
        
        payloadSpan[16] = TimeLock;
        var timeBytes = BitConverter.GetBytes(Timestamp);

        if (BitConverter.IsLittleEndian)
        {
            payloadSpan[17] = timeBytes[7];
            payloadSpan[18] = timeBytes[6];
            payloadSpan[19] = 0xFF;
            payloadSpan[20] = timeBytes[5];
            payloadSpan[21] = timeBytes[4];
            payloadSpan[22] = 0xFF;
            payloadSpan[23] = timeBytes[3];
            payloadSpan[24] = timeBytes[2];
            payloadSpan[25] = 0xFF;
            payloadSpan[26] = timeBytes[1];
            payloadSpan[27] = timeBytes[0];
        }
        else
        {
            payloadSpan[17] = timeBytes[0];
            payloadSpan[18] = timeBytes[1];
            payloadSpan[19] = 0xFF;
            payloadSpan[20] = timeBytes[2];
            payloadSpan[21] = timeBytes[3];
            payloadSpan[22] = 0xFF;
            payloadSpan[23] = timeBytes[4];
            payloadSpan[24] = timeBytes[5];
            payloadSpan[25] = 0xFF;
            payloadSpan[26] = timeBytes[6];
            payloadSpan[27] = timeBytes[7];

        }
        
        switch (TimestampType)
        {
            case CinegyTimestampType.Microsecond:
                KeyCngyMicro.CopyTo(payloadSpan);
                break;
            case CinegyTimestampType.Nanosecond:
                KeyCngyNano.CopyTo(payloadSpan);
                break;
            default:
                throw new InvalidDataException("Unknown Cinegy timestamp type - cannot be serialized");
        }
        
        return payloadSpan.ToArray();
    }

    public byte[] GetBytesAs(CinegyTimestampType type)
    {
        if (type == CinegyTimestampType.Nanosecond || TimestampType == CinegyTimestampType.Nanosecond)
        {
            throw new NotImplementedException("Never got around to this format (maybe not needed?)");
        }

        var data = GetBytes();

        if (type == TimestampType) return data;

        Span<byte> dataSpan = data;
        //simple conversion while nano-conversion is not sorted
        switch (type)
        {
            case CinegyTimestampType.Microsecond:
                KeyCngyMicro.CopyTo(dataSpan);
                break;
            default:
                throw new InvalidDataException("Unknown Cinegy timestamp type - cannot be converted");
        }
        
        return data;
    }
}