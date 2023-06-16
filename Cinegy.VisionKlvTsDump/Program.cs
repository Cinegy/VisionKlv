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

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cinegy.KlvDecoder.Entities;
using Cinegy.KlvDecoder.TransportStream;
using Cinegy.TsDecoder.TransportStream;
using Cinegy.TsDecoder.Video;
using Cinegy.VisionKlvDecoder;

namespace VisionKlvTsDump
{
    internal class Program
    {
        private static readonly JsonSerializerOptions JsonOpts;
        private static readonly VisionDatalinkMetadataFactory VisionFactory = new() { RetainKnownEntities = true };

        private static bool _summaryOnly;
        private static StreamWriter _outputFileStream;
        private static int _seiCount;

        static Program()
        {
            JsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = {
                    new JsonStringEnumConverter()
                }
            };
            
            Console.WriteLine("WARNING - this is pre-release software and output is NOT TO BE TRUSTED IN THIS RELEASE");
        }

        private static async Task Main(string[] args)
        {
            if (args == null || args.Length is < 1 or > 2)
            {
                Console.WriteLine("Please pass the name of the file to decode in as a command-line parameter, and optionally an output directory for dumping to JSON");
                return;
            }

            switch (args.Length)
            {
                case 1:
                {
                    var jsonFilename = Path.ChangeExtension(Path.Combine(Path.GetDirectoryName(args[0])!, Path.GetFileNameWithoutExtension(args[0])), "json");
                    Console.WriteLine($"Saving JSON version of Vision metadata to {jsonFilename}");
                    _summaryOnly = false;

                    if (File.Exists(jsonFilename))
                    {
                        File.Delete(jsonFilename);
                    }

                    _outputFileStream = new StreamWriter(File.OpenWrite(jsonFilename));
                    await _outputFileStream.WriteLineAsync("[");

                    var stopWatch = Stopwatch.StartNew();
                    await DecodeDataFromTsFile(args[0]);
                    stopWatch.Stop();
                    Console.WriteLine($"Decoded file in {stopWatch.Elapsed}");

                    await _outputFileStream.WriteLineAsync("]");
                    await _outputFileStream.DisposeAsync();
                    break;
                }
                case 2:
                {
                    var jsonFilename = Path.ChangeExtension(Path.Combine(args[1], Path.GetFileNameWithoutExtension(args[0])),"json");
                    Console.WriteLine($"Saving JSON version of Vision metadata to {jsonFilename}");
                    _summaryOnly = false;

                    if (File.Exists(jsonFilename))
                    {
                        File.Delete(jsonFilename);
                    }

                    _outputFileStream = new StreamWriter(File.OpenWrite(jsonFilename));
                    await _outputFileStream.WriteLineAsync("[");

                    var stopWatch = Stopwatch.StartNew();
                    await DecodeDataFromTsFile(args[0]);
                    stopWatch.Stop();
                    Console.WriteLine($"Decoded file in {stopWatch.Elapsed}");

                    await _outputFileStream.WriteLineAsync("]");
                    await _outputFileStream.DisposeAsync();
                    break;
                }
            }
        }

        private static async Task DecodeDataFromTsFile(string filePath)
        {
            var tsDecoder = new TsDecoder();
            var factory = new TsPacketFactory();
            var videoAsyncDecoder = new VideoTsDecoder();
            var klvAsyncDecoder = new KlvTsDecoder(0x6, 0x5, true);
            videoAsyncDecoder.TsService.OnVideoNalUnitsReady += TsService_OnVideoNalUnitsReady;
            klvAsyncDecoder.TsService.KlvEntitiesReady += KlvAsyncEntitiesReady;
            VisionFactory.MetadataReady += VisionMetadataReady;

            const int readFragmentSize = 1316*10;

            await using var stream = File.Open(filePath,FileMode.Open,FileAccess.Read);
            Console.WriteLine($"Reading test file: {filePath}");
            var data = new byte[readFragmentSize];
            var readCount = await stream.ReadAsync(data.AsMemory(0, readFragmentSize));

            while (readCount > 0)
            {
                var tsPackets = factory.GetTsPacketsFromData(data);

                if (tsPackets == null) break;
                
                foreach (var tsPacket in tsPackets)
                {
                    tsDecoder.AddPacket(tsPacket);
                    if (tsDecoder.ProgramMapTables.Count <= 0) continue;
                    videoAsyncDecoder.AddPacket(tsPacket, tsDecoder);
                    klvAsyncDecoder.AddPacket(tsPacket, tsDecoder);
                }
                
                if (stream.Position < stream.Length)
                {
                    readCount = stream.Read(data, 0, readFragmentSize);
                }
                else
                {
                    break;
                }
            }
        }
        
        private static void TsService_OnVideoNalUnitsReady(object sender, NalUnitReadyEventArgs args)
        {
            foreach (var nalUnit in args.NalUnits)
            {
                if (nalUnit is H264NalUnit { UnitType: H264NalUnitType.SupplementalEnhancementInfo } h264Nal)
                {
                    _seiCount++;
                    var br = new RbspBitReader(h264Nal.RbspData);
                    while (br.More_RBSP_Data())
                    {
                        var sei = new SeiMessage();
                        sei.Init(br);
                        if (sei.Type != 5) continue;
                        var timeStamp = CinegyTimestamp.Create(sei);
                        if (timeStamp == null) continue;
                        var timestampJson = JsonSerializer.Serialize(timeStamp, JsonOpts);
                        _outputFileStream?.Write(timestampJson);
                        _outputFileStream?.WriteLine(",");
                    }
                }
                
                if (nalUnit is H265NalUnit { UnitType: 39 } h265Nal)
                {
                    _seiCount++;

                    var br = new RbspBitReader(h265Nal.RbspData);
                    while (br.More_RBSP_Data())
                    {
                        var sei = new SeiMessage();
                        sei.Init(br);
                        if (sei.Type != 5) continue;
                        var timeStamp = CinegyTimestamp.Create(sei);
                        if (timeStamp == null) continue;
                        var timestampJson = JsonSerializer.Serialize(timeStamp, JsonOpts);
                        _outputFileStream?.Write(timestampJson);
                        _outputFileStream?.WriteLine(",");
                    }
                }
            }
        }

        private static void KlvSyncEntitiesReady(object sender, KlvEntityReadyEventArgs args)
        {
            throw new NotImplementedException();
        }

        private static void KlvAsyncEntitiesReady(object sender, KlvEntityReadyEventArgs args)
        {
            VisionFactory.AddEntities(args.EntityList);
        }

        private static void VisionMetadataReady(object sender, VisionMetadataEventArgs args)
        {
            if (_summaryOnly)
            {
                Console.WriteLine($"{args.Metadata.EventId}, {args.Metadata.EventStartTime} - GPS: {args.Metadata.CameraLat},{args.Metadata.CameraLong} -  Alt: {args.Metadata.CameraHeight}");
            }
            else
            {
                var json = JsonSerializer.Serialize(args.Metadata, JsonOpts);
                _outputFileStream?.Write(json);
                _outputFileStream?.WriteLine(",");
            }
        }


    }
}