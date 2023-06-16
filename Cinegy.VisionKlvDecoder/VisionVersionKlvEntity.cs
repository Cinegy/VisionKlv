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
using Cinegy.KlvDecoder.Interfaces;

namespace Cinegy.VisionKlvDecoder;

public class VisionVersionKlvEntity : VisionByteKlvEntity
{
    public VisionVersionKlvEntity(byte value) : base((byte)VisionTags.VisionVerNum, value)
    {
    }

    public VisionVersionKlvEntity(IKlvEntity localSetKlvEntity) : base(localSetKlvEntity)
    {
    }

    //version string is in simple 'IDENT.REVISION' format (e.g. CINVIS.17 means 17th revision of Cinegy Vision KLV format)
    public VisionVersionKlvEntity(string visionVersionString) : base((byte)VisionTags.VisionVerNum, 0)
    {
        ReadOnlySpan<char> spanVal = visionVersionString;
        var verChars = spanVal;

        for(var i = 0; i<spanVal.Length;i++)
        {
            //look for the dot, and then use a subset if found
            if (spanVal[i] != '.') continue;
            verChars = spanVal[++i..];
            break;
        }

        //check all remaining chars are numeric
        foreach (var verChar in verChars)
        {
            if (!char.IsNumber(verChar))
            {
                throw new InvalidDataException(
                    $"Invalid {visionVersionString} Vision version string - expected a number <255, optionally after a '.' prefix (e.g. 'CINVIS.17' or '17')");
            }
        }

        var verInt = verChars.Length switch
        {
            1 => char.GetNumericValue(verChars[0]),
            2 => char.GetNumericValue(verChars[0]) * 10
                 + char.GetNumericValue(verChars[1]),
            3 => char.GetNumericValue(verChars[0]) * 100
                 + char.GetNumericValue(verChars[1]) * 10
                 + char.GetNumericValue(verChars[2]),
            _ => 0
        };

        var tooLongString =
            $"Invalid string provided for Vision version - revision value {visionVersionString} (max version = CINVIS.255)";

        if (verChars.Length > 3 || verInt > 255)
        {
            throw new InvalidDataException(tooLongString);
        }

        ByteValue = (byte)verInt;
    }

    public string VisionVersion =>  $"CINVIS.{ByteValue}";
    

}