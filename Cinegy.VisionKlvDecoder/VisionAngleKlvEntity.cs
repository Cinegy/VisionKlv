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

public class VisionAngleKlvEntity : LocalSetKlvEntity
{
    //if you have a camera that can only ever look in a restricted field (e.g. onto a sports field), you can increase resolution in 4 bytes by reducing encoded range
    private double? _angle = null;

    public int Range { get; set; }
    
    public VisionAngleKlvEntity(byte key, double angle, int range = 360)
    {
        Key = new[] { key };
        Range = range;
        
        var incrementPerDegree = (uint.MaxValue - 1d) / Range;
        var intAngle = (long)(angle * incrementPerDegree);
        Value = new byte[4];
        Value[0] = (byte)((intAngle >> 24) & 0xFF);
        Value[1] = (byte)((intAngle >> 16) & 0xFF);
        Value[2] = (byte)((intAngle >> 8) & 0xFF);
        Value[3] = (byte)(intAngle & 0xFF);
    }
        
    public VisionAngleKlvEntity(IKlvEntity localSetKlvEntity, int range = 360)
    {
        Key = localSetKlvEntity.Key;
        Value = localSetKlvEntity.Value;
        Range = range;
    }

    public double? Angle
    {
        get
        {
            //return the cached angle if it's not been reset to null
            if (_angle != null) return _angle;
            if (Value == null || Value.Length < 1) return null;
            
            var klvValue = BitConverter.ToInt32(ValueNative, 0);
            
            var incrementPerKlv = Range / (uint.MaxValue - 1d);
            _angle = klvValue * incrementPerKlv;
            return _angle;
        }
        set
        {
            if (value is null)
            {
                Value = null;
                _angle = null;
                return;
            }

            var incrementPerDegree = (uint.MaxValue - 1d) / Range;
            var intAngle = (long)(value * incrementPerDegree);
            Value = new byte[4];
            Value[0] = (byte)((intAngle >> 24) & 0xFF);
            Value[1] = (byte)((intAngle >> 16) & 0xFF);
            Value[2] = (byte)((intAngle >> 8) & 0xFF);
            Value[3] = (byte)(intAngle & 0xFF);
            
            //clear the cached encoded angle - if it is retrieved again, it will regenerate then
            _angle = null;
        }
    }
}