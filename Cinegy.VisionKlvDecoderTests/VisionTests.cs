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
using System.Text.Json;
using System.Text.Json.Serialization;
using Cinegy.KlvDecoder.Entities;
using Cinegy.KlvDecoder.TransportStream;
using Cinegy.TsDecoder.Descriptors;
using Cinegy.TsDecoder.TransportStream;
using Cinegy.VisionKlvDecoder;
using NUnit.Framework;

namespace Cinegy.VisionKlvDecoderTests
{
    public class Tests
    {
        private Dictionary<string, byte[]> VisionKlvDataSamples = new Dictionary<string, byte[]>();


        private Dictionary<string, byte[]> CinTimestampDataSamples = new Dictionary<string, byte[]>();

     
        private static readonly byte[] CinTimestampDataSample1 =
        {
            0x43, 0x4e, 0x47, 0x59, 0x6d, 0x69, 0x63, 0x72, 0x6f, 0x73, 0x65, 0x63, 0x74, 0x69, 0x6d, 0x65, 0x1f, 0x00,
            0x05, 0xff, 0xe6, 0x0f, 0xff, 0xd8, 0xb1, 0xff, 0x7b, 0x12
        };

        private static readonly byte[] VisionKlvDataSample1 = {
            0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x0E, 0x0E, 0x21, 0x03, 0x01, 0x01, 0x00, 0x00, 0x00, 0x1E, 0x02, 
            0x08, 0x00, 0x05, 0xF1, 0xD8, 0x05, 0x7F, 0x1D, 0xC0, 0x03, 0x0B, 0x55, 0x6E, 0x69, 0x74, 0x54, 0x65, 0x73, 
            0x74, 0x69, 0x6E, 0x67, 0x63, 0x01, 0x11, 0x01, 0x02, 0x37, 0xE2
        };

        private static JsonSerializerOptions _jsonOpts = null!;

        private static readonly VisionDatalinkMetadataFactory VisionFactory = new() { RetainKnownEntities = true };

        public Tests()
        {
            _jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            VisionKlvDataSamples.Add("VisionKlvDataSample1", VisionKlvDataSample1);
            CinTimestampDataSamples.Add("CinTimestampDataSample1", CinTimestampDataSample1);
        }

        [SetUp]
        public void Setup()
        {
            var ts = new CinegyTimestamp(CinegyTimestampType.Microsecond, DateTime.UtcNow);
            var tsbytes = ts.GetBytes();
        }

        [TestCase("CinTimestampDataSample1")]
        public void TestTimestampCreation(string sampleName)
        {
            Console.WriteLine($"Creating Cinegy timestamp from test input '{sampleName}'");
            CinTimestampDataSamples.TryGetValue(sampleName, out var testBytes);

            if (testBytes == null)
            {
                Assert.Fail($"Request for Cinegy timestamp sample named {sampleName} resulted in no input test data!");
            }

            var timestamp = CinegyTimestamp.Create(testBytes);

            Console.WriteLine($"Decoded {timestamp.TimestampType} timestamp (lock code: 0x{timestamp.TimeLock:X}) -- Time: {timestamp.DateTimeStamp}");

            var serializedBytes = timestamp.GetBytes();

            Console.WriteLine("Verifying that source data equals resulting serialized data");
            Assert.AreEqual(testBytes,serializedBytes);
            
            timestamp = CinegyTimestamp.Create(serializedBytes);
            Console.WriteLine($"Cycled results decoded {timestamp.TimestampType} timestamp (lock code: 0x{timestamp.TimeLock:X}) -- Time: {timestamp.DateTimeStamp}");
        }

        
        [TestCase("CinTimestampDataSample1", CinegyTimestampType.Microsecond)]
        public void TestTimestampConversion(string sampleName, CinegyTimestampType targetType)
        {
            Console.WriteLine($"Creating Cinegy timestamp from test input '{sampleName}'");
            CinTimestampDataSamples.TryGetValue(sampleName, out var testBytes);

            if (testBytes == null)
            {
                Assert.Fail($"Request for Cinegy timestamp sample named {sampleName} resulted in no input test data!");
            }

            var timestamp = CinegyTimestamp.Create(testBytes);

            Console.WriteLine($"Decoded {timestamp.TimestampType} timestamp (lock code: 0x{timestamp.TimeLock:X}) -- Time: {timestamp.DateTimeStamp}");

            var serializedBytes = timestamp.GetBytesAs(CinegyTimestampType.Microsecond);
            
            timestamp = CinegyTimestamp.Create(serializedBytes);
            Console.WriteLine($"Cycled results decoded {timestamp.TimestampType} timestamp (lock code: 0x{timestamp.TimeLock:X}) -- Time: {timestamp.DateTimeStamp}");

            Console.WriteLine($"Verifying that returned type {timestamp.TimestampType} is now requested type {targetType}");
            Assert.AreEqual(targetType,timestamp.TimestampType);

            var constructedTimestamp =
                new CinegyTimestamp(timestamp.TimestampType, timestamp.Timestamp, timestamp.TimeLock);
            
            Console.WriteLine($"Verifying that re-constructed timestamp matches input sample"); 
            Console.WriteLine($"Constructed results decoded {constructedTimestamp.TimestampType} timestamp (lock code: 0x{constructedTimestamp.TimeLock:X}) -- Time: {constructedTimestamp.DateTimeStamp}");

            Assert.AreEqual(constructedTimestamp.TimestampType,timestamp.TimestampType);
            Assert.AreEqual(constructedTimestamp.TimeLock,timestamp.TimeLock);
            Assert.AreEqual(constructedTimestamp.Timestamp,timestamp.Timestamp);
            Assert.AreEqual(constructedTimestamp.DateTimeStamp,timestamp.DateTimeStamp);

        }

        [TestCase(-900)]
        [TestCase(0)]
        [TestCase(14190.7195)]
        public void TestAltitudes(double testAltitude)
        {
            var entity = new VisionAltKlvEntity((byte)VisionTags.CameraHeight, testAltitude);
            
            Console.WriteLine($"Encoded input altitude {testAltitude} as {entity.Altitude}");
            Assert.AreEqual(Math.Round(testAltitude,0), Math.Round(entity.Altitude ?? 0,0));
            Console.WriteLine("Resetting altitude via 'setter' property again from decoded value");
            entity.Altitude = entity.Altitude;
            Assert.AreEqual(Math.Round(testAltitude,0), Math.Round(entity.Altitude ?? 0,0));

            var data = entity.Encode();
            var localSet = new LocalSetKlvEntity(data, 0);
            var visionEnt = new VisionAltKlvEntity(localSet);
            Assert.AreEqual(Math.Round(testAltitude,0), Math.Round(visionEnt.Altitude ?? 0,0));

        }

        [TestCase(0.000000042)]
        [TestCase(1)]
        [TestCase(0.99999998)]
        [TestCase(45)]
        [TestCase(60.176822966978335)]
        [TestCase(45.5f)]
        [TestCase(46f)]
        [TestCase(-45f)]
        [TestCase(90)]
        [TestCase(-90)]
        [TestCase(0)]
        public void Test180RangeAngleConversionCycles(double testAngle)
        {
            var entity = new VisionAngleKlvEntity((byte)VisionTags.CameraLat, testAngle,180);
            Console.WriteLine($"Encoded input angle {testAngle} as {entity.Angle}");
            Assert.AreEqual(Math.Round(testAngle,6), Math.Round(entity.Angle ?? 0,6));
            Console.WriteLine("Resetting angle via 'setter' property again from decoded value");
            entity.Angle = entity.Angle;
            Assert.AreEqual(Math.Round(testAngle,6), Math.Round(entity.Angle ?? 0,6));
        }
        
        [TestCase(0.000000042)]
        [TestCase(1)]
        [TestCase(0.99999998)]
        [TestCase(45)]
        [TestCase(60.176822966978335)]
        [TestCase(45.5f)]
        [TestCase(46f)]
        [TestCase(-45f)]
        [TestCase(90)]
        [TestCase(-90)]
        [TestCase(0)]
        [TestCase(179.9999999408610165)]
        [TestCase(180)]
        [TestCase(-180)]
        public void Test360RangeAngleConversionCycles(double testAngle)
        {
            var entity = new VisionAngleKlvEntity((byte)VisionTags.CameraLong, testAngle,360);
            Console.WriteLine($"Encoded input angle {testAngle} as {entity.Angle}");
            Assert.AreEqual(Math.Round(testAngle,6), Math.Round(entity.Angle ?? 0,6));

            Console.WriteLine("Resetting angle via 'setter' property again from decoded value");
            entity.Angle = entity.Angle;
            Assert.AreEqual(Math.Round(testAngle,6), Math.Round(entity.Angle ?? 0,6));
        }

        [TestCase("Hello")]
        [TestCase("")]
        [TestCase("Hello �����")]
        public void TestUtf8StringConversionCycles(string testString)
        {
            var entity = new VisionUtf8KlvEntity((byte)VisionTags.EventId, testString);
            Console.WriteLine($"Encoded input string {testString} as {entity.String}");
            if (string.IsNullOrEmpty(testString) && entity.String == null)
            {
                Console.WriteLine("Empty string correctly serialized as null");
                return;
            }
            Assert.IsTrue(testString.Equals(entity.String,StringComparison.CurrentCulture));

            Console.WriteLine("Resetting string via 'setter' property again from decoded value");
            entity.String = entity.String;
            Assert.IsTrue(testString.Equals(entity.String,StringComparison.CurrentCulture));
        }
        
        [TestCase("VisionKlvDataSample1")]
        public void DecodeKlvData(string sampleName)
        {
            var inputData = VisionKlvDataSamples[sampleName];
            var entities = KlvEntityFactory.GetEntitiesFromData(inputData, inputData.Length, true);
            var visionFactory = new VisionDatalinkMetadataFactory
            {
                RetainKnownEntities = true
            };

            visionFactory.MetadataReady += VisionMetadataReady;
            visionFactory.AddEntities(entities);
        }
        
        [Test]
        public void EncodeKlv()
        {
            var createdKlv = new VisionDatalinkMetadata
            {
                Timestamp = new DateTimeOffset(2023, 1, 9, 17, 23, 11, TimeSpan.Zero),
                EventId = "UnitTesting",
                VisionVerNum = "CINVIS.17"
            };

            var resultingKlv = createdKlv.Encode();

            var visionFactory = new VisionDatalinkMetadataFactory
            {
                RetainKnownEntities = true
            };

            var tsPacket = new TsPacket
            {
                Payload = resultingKlv.SourceData
            };

            visionFactory.MetadataReady += VisionMetadataReady;
            visionFactory.AddEntities(new List<KlvEntity>() { resultingKlv });
        }

        [TestCase("VisionKlvDataSample1")]
        public void EncodeKlvTsPackets(string sampleName)
        {
            ushort pmtPid = 256;
            ushort klvPid = 4096;
            var klvSample = VisionKlvDataSamples[sampleName];

            //create a PES object from the sample
            var pesSample = new Pes(PesStreamTypes.PrivateStream2, klvSample);

            //define a TS packet that may contain this PES (limited to a PES that will fit in a single TS Packet currently)
            var tsPacket = new TsPacket
            {
                Pid = klvPid,
                ContainsPayload = true,
                PayloadUnitStartIndicator = true,
                AdaptationFieldExists = true
            };

            //get the byte payload of the KLV sample, now inside a PES structure
            var pesPayload = pesSample.GetDataFromPes();

            //set up the TS packet adaptation field parameters, and set the payload data object reference
            tsPacket.AdaptationField.FieldSize = (byte)(TsPacketFactory.MaxAdaptationFieldSize - pesPayload.Length);
            tsPacket.AdaptationField.PcrFlag = true;
            tsPacket.Payload = pesPayload;

            //use the TS packet factory ability to serialize a TS packet back into a byte array
            var data = TsPacketFactory.GetDataFromTsPacket(tsPacket);

            //then prepare a new factory, which will eat the serialized output and decode it (verifying that all is working)
            var factory = new TsPacketFactory();
            var generatedPkts = factory.GetTsPacketsFromData(data);

            var decoder = new TsDecoder.TransportStream.TsDecoder();
            decoder.TableChangeDetected += Decoder_TableChangeDetected;
            var klvAsyncDecoder = new KlvTsDecoder(0x6, 0x5, true);

            klvAsyncDecoder.TsService.KlvEntitiesReady += KlvEntitiesReady;
            VisionFactory.MetadataReady += VisionMetadataReady;

            //the TS Decoder needs a PAT and PMT set to locate the KLV and then decode it
            decoder.AddPacket(GetTestPatTsPacket(pmtPid));
            decoder.AddPacket(GetTestPmtTsPacket(pmtPid, klvPid));

            //at the moment, there should only be a single packet - our whole TS stream is just 3 x 188 bytes big!
            foreach (var generatedPkt in generatedPkts)
            {
                decoder.AddPacket(generatedPkt);
                klvAsyncDecoder.AddPacket(generatedPkt, decoder);
            }

            //check callbacks - we should now have some simple KLV decoded
        }

        private void Decoder_TableChangeDetected(object sender, TableChangedEventArgs args)
        {
            Console.WriteLine($"Table {args.TableType} (PID: {args.TablePid}) changed: {args.Message}");
        }

        private TsPacket GetTestPatTsPacket(ushort programPmtPid = 256, ushort tsId = 9999, ushort programNumber = 1)
        {
            var pat = new TsDecoder.Tables.ProgramAssociationTable
            {
                TransportStreamId = tsId,
                CurrentNextIndicator = true,
                ProgramNumbers = new ushort[] { programNumber },
                Pids = new ushort[] { programPmtPid }
            };

            var patTsPacket = new TsPacket
            {
                Pid = 0,
                ContainsPayload = true,
                PayloadUnitStartIndicator = true,
                AdaptationFieldExists = false
            };

            var patData = pat.GetData();

            patTsPacket.Payload = new byte[184];
            for (var i = 0; i < 184; i++)
            {
                patTsPacket.Payload[i] = 0xFF;
            }

            patTsPacket.Payload[0] = 0x0; //set PAT pointer to zero
            Buffer.BlockCopy(patData, 0, patTsPacket.Payload, 1, patData.Length);
            return patTsPacket;
        }

        private TsPacket GetTestPmtTsPacket(ushort pmtPid = 256, ushort pcrPid = 4096, ushort programNumber = 1)
        {
            var pmt = new TsDecoder.Tables.ProgramMapTable
            {
                Pid = pmtPid,
                CurrentNextIndicator = true,
                ProgramNumber = programNumber,
                PcrPid = pcrPid
            };

            pmt.EsStreams = new List<EsInfo>();

            var klvEsInfo = new EsInfo()
            {
                Descriptors = new List<Descriptor>(),
                StreamType = 0x6,
                ElementaryPid = pcrPid
            };

            var klvDesc = new RegistrationDescriptor()
            {
                FormatIdentifier = "KLVA"u8.ToArray()
            };

            klvEsInfo.Descriptors.Add(klvDesc);

            pmt.EsStreams.Add(klvEsInfo);

            var pmtTsPacket = new TsPacket
            {
                Pid = pmt.Pid,
                ContainsPayload = true,
                PayloadUnitStartIndicator = true,
                AdaptationFieldExists = false
            };

            var pmtData = pmt.GetData();

            pmtTsPacket.Payload = new byte[184];
            for (var i = 0; i < 184; i++)
            {
                pmtTsPacket.Payload[i] = 0xFF;
            }

            pmtTsPacket.Payload[0] = 0x0; //set PAT pointer to zero
            Buffer.BlockCopy(pmtData, 0, pmtTsPacket.Payload, 1, pmtData.Length);
            return pmtTsPacket;
        }

        private void KlvEntitiesReady(object sender, KlvEntityReadyEventArgs args)
        {
            var visionDatalinkMetadataFactory = new VisionDatalinkMetadataFactory
            {
                RetainKnownEntities = true,
                RetainUnknownEntities = true
            };

            visionDatalinkMetadataFactory.MetadataReady += VisionMetadataReady;
            visionDatalinkMetadataFactory.AddEntities(args.EntityList);
        }
        
        private static void VisionMetadataReady(object sender, VisionMetadataEventArgs args)
        {
            var json = JsonSerializer.Serialize(args.Metadata, _jsonOpts);
            Console.WriteLine(json);
           // Console.WriteLine(args.Metadata.Timestamp);
        }
        
        private void TsDecoder_TableChangeDetected(object sender, TableChangedEventArgs args)
        {
            Console.WriteLine($"{args.Message}");
        }
    }
}