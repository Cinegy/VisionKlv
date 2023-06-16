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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinegy.KlvDecoder.Entities;

namespace Cinegy.VisionKlvDecoder
{
    public class VisionDatalinkMetadata
    {
        private readonly Dictionary<string,LocalSetKlvEntity> _entities = new(128);

        //TODO: Replace this key                         06-0E-2B-34-02-0B-01-01-0E-01-03-01-01-00-00-00
        public const string VisionDatalinkMetadataKey = "06-0E-2B-34-04-01-01-0E-0E-21-03-01-01-00-00-00";

        #region Properties

        public int Checksum { get; set; } = -1;

        public DateTimeOffset? Timestamp { 
            get => _entities.TryGetValue("Timestamp", out var value) ? ((VisionTimestampKlvEntity)value)?.Timestamp : null;
            set
            {
                if(value == null)
                {
                    _entities.Remove("Timestamp"); 
                    return;
                }
                _entities.Add("Timestamp", new VisionTimestampKlvEntity((byte)VisionTags.Timestamp, (DateTimeOffset)value));
            }

        }
        
        public string EventId
        {
            get => _entities.TryGetValue("EventId", out var value) ? ((VisionUtf8KlvEntity)value)?.String : null;
            set
            {
                if(value == null)
                {
                    _entities.Remove("EventId"); 
                    return;
                }
                _entities.Add("EventId", new VisionUtf8KlvEntity((byte)VisionTags.EventId, value));
            }
        }

        public DateTimeOffset? EventStartTime
        {
            get => _entities.TryGetValue("EventStartTime", out var value) ? ((VisionTimestampKlvEntity)value)?.Timestamp : null;
            set
            {
                if (value == null)
                {
                    _entities.Remove("EventStartTime");
                    return;
                }
                _entities.Add("EventStartTime", new VisionTimestampKlvEntity((byte)VisionTags.EventStartTime, (DateTimeOffset)value));
            }

        }

        public string CameraName
        {
            get => _entities.TryGetValue("CameraName", out var value)
                ? ((VisionUtf8KlvEntity)value)?.String
                : null;
            set
            {
                if (value == null)
                {
                    _entities.Remove("CameraName");
                    return;
                }

                _entities.Add("CameraName", new VisionUtf8KlvEntity((byte)VisionTags.CameraName, value));
            }
        }

        public float? CameraDirection { get; }
        
        public double? CameraLat
        {
            get => _entities.TryGetValue("CameraLat", out var value) ? ((VisionAngleKlvEntity)value).Angle : null;
            set
            {
                if (value == null)
                {
                    _entities.Remove("CameraLat");
                    return;
                }

                _entities.Add("CameraLat", new VisionAngleKlvEntity((byte)VisionTags.CameraLat, (double)value, 180));
            }
        }

        public double? CameraLong
        {
            get => _entities.TryGetValue("CameraLong", out var value) ? ((VisionAngleKlvEntity)value).Angle : null;
            set
            {
                if (value == null)
                {
                    _entities.Remove("CameraLong");
                    return;
                }

                _entities.Add("CameraLong", new VisionAngleKlvEntity((byte)VisionTags.CameraLong, (double)value));
            }
        }
        
        public double? CameraHeight
        {
            get => _entities.TryGetValue("CameraHeight", out var value) ? ((VisionAltKlvEntity)value).Altitude : null;
            set
            {
                if (value == null)
                {
                    _entities.Remove("CameraHeight");
                    return;
                }

                _entities.Add("CameraHeight", new VisionAltKlvEntity((byte)VisionTags.CameraHeight, (double)value));
            }
        }


        public byte? BatteryRemaining
        {
            get => _entities.TryGetValue("BatteryRemaining", out var value) ? ((VisionByteKlvEntity)value).ByteValue : null;
            set
            {
                if (value == null)
                {
                    _entities.Remove("BatteryRemaining");
                    return;
                }

                _entities.Add("BatteryRemaining", new VisionByteKlvEntity((byte)VisionTags.BatteryRemaining, (byte)value));
            }
        }

        public byte? StorageRemaining
        {
            get => _entities.TryGetValue("StorageRemaining", out var value) ? ((VisionByteKlvEntity)value).ByteValue : null;
            set
            {
                if (value == null)
                {
                    _entities.Remove("StorageRemaining");
                    return;
                }

                _entities.Add("StorageRemaining", new VisionByteKlvEntity((byte)VisionTags.StorageRemaining, (byte)value));
            }
        }

        public string VisionVerNum
        {
            get => _entities.TryGetValue("VisionVerNum", out var value)
                ? ((VisionVersionKlvEntity)value).VisionVersion
                : null;
            set
            {
                if (value == null)
                {
                    _entities.Remove("VisionVerNum");
                    return;
                }
                _entities.Add("VisionVerNum", new VisionVersionKlvEntity(value));
            }
        }
        
        public Dictionary<string,LocalSetKlvEntity> KnownTags { get; }

        public List<LocalSetKlvEntity> UnknownTags { get; }

        #endregion

        public VisionDatalinkMetadata()
        {
            UnknownTags = new List<LocalSetKlvEntity>(128);
            KnownTags = new Dictionary<string, LocalSetKlvEntity>(128);
        }

        public VisionDatalinkMetadata(UniversalLabelKlvEntity klvEntity, bool retainUnknownEntities = true, bool retainKnownEntities = false)
        {
            var entities = KlvEntityFactory.GetLocalSetEntitiesFromData(klvEntity.Value);
            UnknownTags = new List<LocalSetKlvEntity>(entities.Count);
            KnownTags = new Dictionary<string, LocalSetKlvEntity>(entities.Count);

            foreach (var lsMetadata in entities)
            {
                if (Enum.IsDefined((VisionTags)lsMetadata.TagId))
                {
                    switch (lsMetadata.TagId)
                    {
                        case (int)VisionTags.Checksum:
                            Checksum = BitConverter.ToUInt16(lsMetadata.ValueNative, 0);
                            var calculatedChecksum = CalculateChecksum(klvEntity.SourceData);
                            if (Checksum != calculatedChecksum)
                            {
                                //negative checksum means a failure, with calculated checksum negated to assist any debugging
                                Checksum = calculatedChecksum * -1;
                                //throw new InvalidDataException("Checksum mismatch! Please investigate (exception should be killed when telemetry metrics introduced");
                            }
                            break;
                        case (int)VisionTags.Timestamp:
                            _entities.Add("Timestamp", new VisionTimestampKlvEntity(lsMetadata));
                            break;
                        case (int)VisionTags.EventId:
                            _entities.Add("EventId",new VisionUtf8KlvEntity(lsMetadata));
                            break;
                        case (int)VisionTags.EventStartTime:
                            _entities.Add("EventStartTime", new VisionTimestampKlvEntity(lsMetadata));
                            break;
                        case (int)VisionTags.CameraName:
                            _entities.Add("CameraName",new VisionUtf8KlvEntity(lsMetadata));
                            break;
                        case (int)VisionTags.CameraDirection:
                            var rawDirVal = BitConverter.ToUInt16(lsMetadata.ValueNative, 0);
                            CameraDirection = rawDirVal / (ushort.MaxValue / 360f);
                            break;
                       case (int)VisionTags.CameraLat:
                            _entities.Add("CameraLat",new VisionAngleKlvEntity(lsMetadata,180));
                            break;
                        case (int)VisionTags.CameraLong:
                            _entities.Add("CameraLong",new VisionAngleKlvEntity(lsMetadata));
                            break;
                        case (int)VisionTags.CameraHeight:
                            _entities.Add("CameraHeight",new VisionAltKlvEntity(lsMetadata));
                            break;
                        case (int)VisionTags.BatteryRemaining:
                            _entities.Add("BatteryRemaining", new VisionByteKlvEntity(lsMetadata));
                            break;
                        case (int)VisionTags.StorageRemaining:
                            _entities.Add("StorageRemaining", new VisionByteKlvEntity(lsMetadata));
                            break;
                        case (int)VisionTags.VisionVerNum:
                            _entities.Add("VisionVerNum", new VisionVersionKlvEntity(lsMetadata));
                            break;
                    }

                    if (retainKnownEntities)
                    {
                        KnownTags.Add(Enum.GetName((VisionTags)lsMetadata.TagId), lsMetadata);                        
                    }
                }
                else
                {
                    if (retainUnknownEntities)
                    {
                        UnknownTags.Add(lsMetadata);
                    }
                }
            }
        }

        public UniversalLabelKlvEntity Encode()
        {
            var checksumKlv = new LocalSetKlvEntity
            {
                Key = new[] { (byte)0x1 },
                Value = new[] { (byte)0x0, (byte)0x0 }
            };

            _entities.Add("Checksum", checksumKlv);

            var sourceData = Array.Empty<byte>();

            foreach(var entity in _entities.Values)
            {
                sourceData = sourceData.Concat(entity.Encode()).ToArray();
            }

            var keyBuffer = VisionDatalinkMetadataKey.Split('-').Select(x => Convert.ToByte(x, 16)).ToArray();
            var len = new[] { (byte)sourceData.Length };
            var universalLabelKlvData = keyBuffer.Concat(len).ToArray().Concat(sourceData).ToArray();

            var calculatedChecksum = CalculateChecksum(universalLabelKlvData);
            universalLabelKlvData[^2] = BitConverter.GetBytes(calculatedChecksum)[1];
            universalLabelKlvData[^1] = BitConverter.GetBytes(calculatedChecksum)[0];

            var klvEntity = new UniversalLabelKlvEntity(universalLabelKlvData, 0, universalLabelKlvData.Length, true);
                        
            return klvEntity;
        }

        private static int CalculateChecksum(byte[] sourceData)
        {
            if(sourceData == null)
            {
                throw new InvalidDataException("Checksum calculation was attempted, but source data for KLV was not preserved");
            }

            ushort bcc = 0;

            for(ushort i = 0; i < sourceData.Length - 2; i++)
            {
                bcc += (ushort)(sourceData[i] << (8 * ((i + 1) % 2)));
            }

            return bcc;
        }
    }
}
