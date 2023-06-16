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

using System.Text;
using Cinegy.KlvDecoder.Entities;
using Cinegy.KlvDecoder.Interfaces;

namespace Cinegy.VisionKlvDecoder
{

    public class VisionUtf8KlvEntity : LocalSetKlvEntity
    {
        public VisionUtf8KlvEntity(byte key, string stringValue)
        {
            Key = new byte[] { key };
            String = stringValue;
        }

        public VisionUtf8KlvEntity(IKlvEntity localSetKlvEntity)
        {
            Key = localSetKlvEntity.Key;
            Value = localSetKlvEntity.Value;
        }

        public string String
        {
            get => Value == null ? null : Encoding.UTF8.GetString(Value);
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Value = null;
                    return;
                }

                Value = Encoding.UTF8.GetBytes(value);
            }
        }
    }
}
