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

using Cinegy.VisionKlvDecoder;
using CommandLine;

namespace VisionKlvTsFileMuxer
{
    public class Program
    {
        private static bool _pendingExit;
        private static bool _suppressOutput;

        private static MuxOptions _options;

        private static KlvPidMuxer _muxer;
        private static readonly VisionDatalinkMetadataFactory VisionFactory = new();

        private static FileStream _outputFileStream;
                
        private enum ExitCodes
        {
            SubPidError = 102,
            UnknownError = 2000
        }

        private static int Main(string[] args)
        {
            try
            {
                var result = Parser.Default.ParseArguments<MuxOptions>(args);

                return result.MapResult(
                    Run,
                    _ => CheckArgumentErrors());
            }
            catch (Exception ex)
            {
                Environment.ExitCode = (int)ExitCodes.UnknownError;
                Console.WriteLine("Unknown error: " + ex.Message);
                throw;
            }
        }

        private static int CheckArgumentErrors()
        {
            //will print using library the appropriate help - now pause the console for the viewer
            Console.WriteLine("Hit enter to quit");
            Console.ReadLine();
            return -1;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.CursorVisible = true;
            if (_pendingExit) return; //already trying to exit - allow normal behaviour on subsequent presses
            _pendingExit = true;
            e.Cancel = true;
        }

        private static int Run(MuxOptions options)
        {
            Console.Clear();
            Console.CancelKeyPress += Console_CancelKeyPress;
            
            Console.WriteLine(
               // ReSharper disable once AssignNullToNotNullAttribute
               $"Cinegy Vision KLV TS File Muxing tool v23.01.1 (Built: {File.GetCreationTime(AppContext.BaseDirectory)})\n");

            _options = options;
                        
            _suppressOutput = _options.SuppressOutput; //only suppresses extra logging to screen, not dynamic output

            _muxer = new KlvPidMuxer(_options.SourceVideoFile, _options.KlvJsonFile, _options.AsyncKlvPid) 
            {
                PrintErrorsToConsole = !_suppressOutput
            };

            _muxer.PacketReady += _muxer_PacketReady;

            _outputFileStream = new FileStream(_options.OutputVideoFile,FileMode.Create, FileAccess.Write);

            _muxer.Start();

            return 0;

        }

        private static async void _muxer_PacketReady(object sender, KlvPidMuxer.PacketReadyEventArgs e)
        {
            await _outputFileStream.WriteAsync(e.PacketData);
        }
    }

}
