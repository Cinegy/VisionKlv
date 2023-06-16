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
using Cinegy.KlvDecoder.Entities;
using Cinegy.KlvDecoder.Interfaces;

namespace Cinegy.VisionKlvDecoder;

public class VisionAltKlvEntity : LocalSetKlvEntity
{
    //picked some default height ranges deeper than you'd expect to find anyone filming (of course, someone will send some metadata to the Titanic just to annoy me)
    //and double height of Everest - so someone filming looking down on a big mountain should have room to fly - but there is only so much resolution / range
    //you can fit into two bytes after all :)
    private double? _alt;

    public int Range { get; set; }

    public int Offset { get; set; }

    public VisionAltKlvEntity(byte key, double altitude, int range = 19900, int offset = -900)
    {
        Key = new[] { key };
        Range = range;
        Offset = offset;

        var incrementPerMeter = 65535d / Range;
        var intAlt = (uint)((altitude - Offset) * incrementPerMeter);
        Value = new byte[2];
        Value[0] = (byte)((intAlt >> 8) & 0xFF);
        Value[1] = (byte)(intAlt & 0xFF);
    }

    public VisionAltKlvEntity(IKlvEntity localSetKlvEntity, int range = 19900, int offset = -900)
    {
        Key = localSetKlvEntity.Key;
        Value = localSetKlvEntity.Value;
        Range = range;
        Offset = offset;
    }

    public double? Altitude
    {
        get
        {
            //return the cached altitude if it's not been reset to null
            if (_alt != null) return _alt;
            if (Value == null || Value.Length < 1) return null;

            var klvValue = BitConverter.ToUInt16(ValueNative, 0);

            var incrementPerKlv = Range / 65535d;
            _alt = Math.Round(klvValue * incrementPerKlv + Offset,4);
            return _alt;
        }
        set
        {
            if (value is null)
            {
                Value = null;
                _alt = null;
                return;
            }
            
            var incrementPerMeter = 65535d / Range;
            var intAlt = (uint)((value - Offset) * incrementPerMeter);
            Value = new byte[2];
            Value[0] = (byte)((intAlt >> 8) & 0xFF);
            Value[1] = (byte)(intAlt & 0xFF);

            //clear the cached encoded altitude - if it is retrieved again, it will regenerate then
            _alt = null;
        }
    }
}