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

using System.Text.Json;
using Cinegy.TsDecoder.Buffers;
using Cinegy.TsDecoder.Descriptors;
using Cinegy.TsDecoder.Tables;
using Cinegy.TsDecoder.TransportStream;
using Cinegy.VisionKlvDecoder;

namespace VisionKlvTsFileMuxer
{
    internal class KlvPidMuxer
    {
        private readonly RingBuffer _asyncKlvPktBuffer = new(1000, TsPacketSize, true);

        private readonly string _sourceVideoPath;
        private readonly string _sourceKlvJsonPath;
        private readonly ushort _asyncKlvPid;
        private byte _klvPidCc = 0;

        private ProgramMapTable _videoStreamPmt;
        private ulong _referencePcr;
        private readonly DateTime _startTime = DateTime.UtcNow;

        private ulong _lastSourceVideoPcr;
        private List<VisionDatalinkMetadata> _klvSource;
        private int _klvInjectionCount;

        private readonly TsDecoder _videoStreamDecoder = new();
        private static readonly TsPacketFactory Factory = new();

        // ReSharper disable once InconsistentNaming
        private const uint CRC32_POLYNOMIAL = (0x02608EDB << 1) | 1;
        private const int TsPacketSize = 188;

        public bool PrintErrorsToConsole { get; set; }

        public KlvPidMuxer(string videoSourcePath, string klvJsonSourcePath, ushort asyncKlvPid)
        {
            _asyncKlvPid = asyncKlvPid;
            _sourceVideoPath = videoSourcePath;
            _sourceKlvJsonPath = klvJsonSourcePath;
        }

        public void Start()
        {
            using var stream = File.OpenRead(_sourceVideoPath);
            Console.WriteLine($"Reading video source file: {_sourceVideoPath}");

            _klvSource = LoadKlvJson();

            var buffer = new byte[TsPacketSize];

            var readCount = stream.Read(buffer, 0, TsPacketSize);

            while (readCount > 0)
            {
                var packets = Factory.GetTsPacketsFromData(buffer, readCount);

                //use decoder to register default program (muxing always happens on default program)
                if (_videoStreamDecoder.GetSelectedPmt() == null)
                {
                    _videoStreamDecoder.AddPackets(packets);
                }
                else
                {
                    if (_videoStreamPmt == null)
                    {
                        _videoStreamPmt = _videoStreamDecoder.GetSelectedPmt();

                        var pmtSpaceNeeded = GetKlvEsInfo().GetData().Length;

                        if (_videoStreamPmt.SectionLength + pmtSpaceNeeded > TsPacketSize - 12)
                        {
                            throw new InvalidDataException(
                                "Cannot add to PMT - no room (packet spanned PMT not supported)");
                        }
                    }
                }

                if (_videoStreamPmt != null)
                {
                    //if we made it here, we've now managed to scan a PMT - so we can quit this loop (the TS decoder is now pre-warmed)
                    break;
                }

                //still not yet fully-warmed; read some more and keep trying
                readCount = stream.Read(buffer, 0, TsPacketSize);
            }

            //if we made it out of the loop, but without a PMT - we probably had a file input problem...
            if (_videoStreamPmt == null)
            {
                throw new InvalidDataException("Finished scanning whole TS input, and did not find any PMT tables - corrupted TS?");
            }

            //now we know the PAT / PMT layout, we can reset the stream to the start (ready to patch from the very first PMT)
            stream.Seek(0, SeekOrigin.Begin);
            readCount = stream.Read(buffer, 0, TsPacketSize);

            while (readCount > 0)
            {
                var packets = Factory.GetTsPacketsFromData(buffer, readCount);

                //check for any PMT packets, and adjust them to reflect the new muxed reality...
                foreach (var packet in packets)
                {
                    if (packet.Pid != _videoStreamPmt?.Pid) continue;

                    //this is the PMT for the target program on the target stream - patch in the substream PID entries                 
                    var sourceData = GetKlvEsInfo().GetData();

                    //locate current SectionLength bytes in databuffer
                    var pos = packet.SourceBufferIndex + 4; //advance to start of PMT data structure (past TS header)
                    var pointerField = buffer[pos];
                    pos += pointerField; //advance by pointer field
                    var sectionLen = (short)(((buffer[pos + 2] & 0x3) << 8) + buffer[pos + 3]); //get current length

                    //increase length value by esinfo length
                    var extSectionLen = (short)(sectionLen + (short)sourceData.Length);

                    //set back new length into databuffer                                        
                    var bytes = BitConverter.GetBytes(extSectionLen);
                    buffer[pos + 2] = (byte)((buffer[pos + 2] & 0xFC) + (byte)(bytes[1] & 0x3));
                    buffer[pos + 3] = bytes[0];

                    //copy esinfo source data to end of program block in pmt
                    Buffer.BlockCopy(sourceData, 0, buffer,
                        packet.SourceBufferIndex + 4 + pointerField + sectionLen,
                        sourceData.Length);

                    //correct CRC after each extension
                    var crcBytes = BitConverter.GetBytes(GenerateCrc(ref buffer, pos + 1, extSectionLen - 1));

                    buffer[packet.SourceBufferIndex + 4 + pointerField + extSectionLen] = crcBytes[3];
                    buffer[packet.SourceBufferIndex + 4 + pointerField + extSectionLen + 1] = crcBytes[2];
                    buffer[packet.SourceBufferIndex + 4 + pointerField + extSectionLen + 2] = crcBytes[1];
                    buffer[packet.SourceBufferIndex + 4 + pointerField + extSectionLen + 3] = crcBytes[0];

                }

                //check for any PCR packets, to track where we are in source and when we need to add some KLV
                foreach (var packet in packets)
                {
                    CheckPcr(packet);

                    if (packet is { PayloadUnitStartIndicator: true, PesHeader.StartCode: 0x1 })
                    {
                        //check against new frames to see if we are due to insert a new async KLV
                        var pcrSinceStart = _lastSourceVideoPcr - _referencePcr;
                        var sinceStart = new TimeSpan((long)(pcrSinceStart / 2.7));
                        if ((_klvInjectionCount * 100) < sinceStart.TotalMilliseconds)
                        {
                            Console.WriteLine($"Time for a KLV at {sinceStart} (KLV number: {_klvInjectionCount})");
                            
                            if (_klvInjectionCount >= _klvSource.Count) break;

                            var resultingKlv = _klvSource[_klvInjectionCount++].Encode();

                            //create a PES object from the sample
                            var pesSample = new Pes(PesStreamTypes.PrivateStream1, resultingKlv.SourceData, new OptionalPes {DataAlignmentIndicator = true});

                            //define a TS packet that may contain this PES (limited to a PES that will fit in a single TS Packet currently)
                            var tsPacket = new TsPacket
                            {
                                Pid = _asyncKlvPid,
                                ContinuityCounter = _klvPidCc++,
                                ContainsPayload = true,
                                PayloadUnitStartIndicator = true,
                                AdaptationFieldExists = true
                            };

                            if (_klvPidCc > 15) _klvPidCc = 0;

                            //get the byte payload of the KLV sample, now inside a PES structure
                            var pesPayload = pesSample.GetDataFromPes();

                            //set up the TS packet adaptation field parameters, and set the payload data object reference
                            tsPacket.AdaptationField.FieldSize = (byte)(TsPacketFactory.MaxAdaptationFieldSize - pesPayload.Length);
                            //tsPacket.AdaptationField.PcrFlag = true;
                            tsPacket.Payload = pesPayload;
                            tsPacket.PayloadLen = pesPayload.Length;

                            //use the TS packet factory ability to serialize a TS packet back into a byte array
                            var data = TsPacketFactory.GetDataFromTsPacket(tsPacket);

                            _asyncKlvPktBuffer.Add(data);
                        }
                    }

                    //if packet is null packet, swap out for anything waiting in output queue
                    if (packet.Pid == 0x1FFF)
                    {
                        if (_asyncKlvPktBuffer.BufferFullness > 0)
                        {
                            Console.WriteLine($"Injecting KLV {_klvInjectionCount}");
                            _asyncKlvPktBuffer.Remove(buffer, out _, out _);
                        }
                    }
                }

                var packetReadyEventArgs = new PacketReadyEventArgs
                {
                    PacketData = new byte[readCount]
                };

                Buffer.BlockCopy(buffer, 0, packetReadyEventArgs.PacketData, 0, readCount);
                OnPacketReady(packetReadyEventArgs);

                readCount = stream.Read(buffer, 0, TsPacketSize);
            }
        }

        private List<VisionDatalinkMetadata> LoadKlvJson()
        {
            using var klvStream = File.OpenRead(_sourceKlvJsonPath);
            Console.WriteLine($"Reading KLV source file: {_sourceKlvJsonPath}");
            var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            
            var klvLists = JsonSerializer.Deserialize<List<VisionDatalinkMetadata>>(klvStream,jsonOpts);
                        
            if (klvLists.Count < 1)
            {
                throw new InvalidDataException("Provided KLV JSON resulted in no KLV entries - check JSON format");
            }
            Console.WriteLine($"Decoded {klvLists.Count} KLV items from source KLV JSON");

            foreach (var visionDatalinkMetadata in klvLists)
            {
                visionDatalinkMetadata.VisionVerNum = "17";
                visionDatalinkMetadata.EventStartTime = DateTimeOffset.UtcNow;   
            }


            return klvLists;
        }

        private EsInfo GetKlvEsInfo()
        {
            var klvEsInfo = new EsInfo()
            {
                ElementaryPid = _asyncKlvPid,
                Descriptors = new List<Descriptor>(),
                StreamType = 0x6
            };

            var klvDesc = new RegistrationDescriptor()
            {
                FormatIdentifier = "KLVA"u8.ToArray()
            };

            klvEsInfo.Descriptors.Add(klvDesc);

            return klvEsInfo;
        }

        protected virtual void OnPacketReady(PacketReadyEventArgs e)
        {
            var handler = PacketReady;
            handler?.Invoke(this, e);
        }

        public event EventHandler<PacketReadyEventArgs> PacketReady;

        public class PacketReadyEventArgs : EventArgs
        {
            public byte[] PacketData { get; set; }
        }

        private void CheckPcr(TsPacket tsPacket)
        {
            if (!tsPacket.AdaptationFieldExists) return;
            if (!tsPacket.AdaptationField.PcrFlag) return;
            if (tsPacket.AdaptationField.FieldSize < 1) return;

            if (tsPacket.AdaptationField.DiscontinuityIndicator)
            {
                Console.WriteLine("Adaptation field discontinuity indicator");
                return;
            }

            if (_lastSourceVideoPcr == 0)
            {
                _referencePcr = tsPacket.AdaptationField.Pcr;
            }

            _lastSourceVideoPcr = tsPacket.AdaptationField.Pcr;
        }

        private static uint GenerateCrc(ref byte[] dataBuffer, int position, int length)
        {
            var endPos = position + length;
            var crc = uint.MaxValue;

            for (var i = position; i < endPos; i++)
            {
                for (var masking = 0x80; masking != 0; masking >>= 1)
                {
                    var carry = crc & 0x80000000;
                    crc <<= 1;
                    if (carry != 0 ^ (dataBuffer[i] & masking) != 0)
                        crc ^= CRC32_POLYNOMIAL;
                }
            }

            return crc;
        }
    }
}

