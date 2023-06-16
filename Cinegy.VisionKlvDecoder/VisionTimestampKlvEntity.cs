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
using System.Linq;
using Cinegy.KlvDecoder.Entities;
using Cinegy.KlvDecoder.Interfaces;

namespace Cinegy.VisionKlvDecoder
{
    public class VisionTimestampKlvEntity : LocalSetKlvEntity
    {      
        public VisionTimestampKlvEntity(byte key, DateTimeOffset dateTimeOffset)
        {
            Key = new[] { key };
            var microseconds = (ulong)(dateTimeOffset.ToUnixTimeMilliseconds() * 1000);
            var tsData = BitConverter.GetBytes(microseconds);
            
            Value = BitConverter.IsLittleEndian ? tsData.Reverse().ToArray() : tsData;
        }
        
        public VisionTimestampKlvEntity(IKlvEntity localSetKlvEntity)
        {
            Key = localSetKlvEntity.Key;
            Value = localSetKlvEntity.Value;
        }
        
        public DateTimeOffset? Timestamp
        {
            get
            {
                if (Value == null || Value.Length < 8) return null;
                var microsecondsTimestamp = BitConverter.ToUInt64(ValueNative, 0);
                var dto = DateTimeOffset.FromUnixTimeMilliseconds((long)microsecondsTimestamp / 1000);
                return dto;
            }
            set
            {
                if (value is null)
                {
                    Value = null;
                    return;
                }

                var dto = (DateTimeOffset)value;
                var microseconds = (ulong)(dto.ToUnixTimeMilliseconds() * 1000);
                var valueBytes = BitConverter.GetBytes(microseconds);
                Value = BitConverter.IsLittleEndian ? valueBytes.Reverse().ToArray() : valueBytes;
            }
        }

    }
}
