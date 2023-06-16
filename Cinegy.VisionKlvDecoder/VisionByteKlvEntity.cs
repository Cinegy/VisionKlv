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

using Cinegy.KlvDecoder.Entities;
using Cinegy.KlvDecoder.Interfaces;

namespace Cinegy.VisionKlvDecoder;

public class VisionByteKlvEntity : LocalSetKlvEntity
{
    private byte? _byte;
    
    public VisionByteKlvEntity(byte key, byte value)
    {
        Key = new[] { key };
        Value = new[] { value };
    }

    public VisionByteKlvEntity(IKlvEntity localSetKlvEntity)
    {
        Key = localSetKlvEntity.Key;
        Value = localSetKlvEntity.Value;
    }

    public byte? ByteValue
    {
        get
        {
            if (_byte != null) return _byte;
            if (Value == null || Value.Length < 1) return null;

            _byte = Value[0];
            
            return _byte;
        }
        set
        {
            if (value is null)
            {
                Value = null;
                _byte = null;
                return;
            }
            
            Value = new byte[1];
            Value[0] = (byte)value;

            //clear the cached encoded value (null is helpful to easily skip serialization)
            _byte = null;
        }
    }
}