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

using CommandLine;

namespace VisionKlvTsFileMuxer
{
    internal class Options
    {
        [Option("silent", Required = false,
        HelpText = "Silence console output")]
        public bool SuppressOutput { get; set; }

    }

    // Define a class to receive parsed values
    [Verb("mux", HelpText = "Mux from the network.")]
    internal class MuxOptions : Options
    {
        [Option('f', "outputfile", Required = false,
        HelpText = "Path of target resulting newly-muxed TS file")]
        public string OutputVideoFile { get; set; } = string.Empty;
        
        [Option('v', "videofile", Required = false,
        HelpText = "Path to the source video TS file")]
        public string SourceVideoFile { get; set; } = string.Empty;

        [Option('k', "klvfile", Required = true,
        HelpText = "Path to the source KLV JSON file")]
        public string KlvJsonFile { get; set; } = string.Empty;

        [Option('a', "asyncklvpid", Required = true,
        HelpText = "Target PID for Async format KLV metadata")]
        public ushort AsyncKlvPid { get; set; }


    }
}
