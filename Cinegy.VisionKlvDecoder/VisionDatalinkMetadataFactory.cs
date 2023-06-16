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
using Cinegy.KlvDecoder.Entities;

namespace Cinegy.VisionKlvDecoder
{
    public class VisionDatalinkMetadataFactory
    {
        public bool RetainKnownEntities { get; set; } = false;

        public bool RetainUnknownEntities { get; set; } = true;

        public void AddEntities(IEnumerable<KlvEntity> klvEntities)
        {
            foreach (var klvEntity in klvEntities)
            {
                AddEntity(klvEntity);
            }
        }

        public void AddEntity(KlvEntity klvEntity)
        {
            if (klvEntity.KeyHexString.Equals(VisionDatalinkMetadata.VisionDatalinkMetadataKey, StringComparison.InvariantCultureIgnoreCase))
            {
                ExtractVisionDatalinkMetadata((UniversalLabelKlvEntity)klvEntity);
            }
        }

        public void ExtractVisionDatalinkMetadata(UniversalLabelKlvEntity klvEntity)
        {
            var visionData = new VisionDatalinkMetadata(klvEntity,RetainUnknownEntities, RetainKnownEntities);

            if (visionData.Checksum < 0)
            {
                Console.WriteLine($"Metadata set has invalid checksum - calculated value: {visionData.Checksum * -1}");
            }

            OnMetadataReady(visionData);
        }

        public delegate void VisionMetadataEventHandler(object sender, VisionMetadataEventArgs args);

        // some metadata is available
        public event VisionMetadataEventHandler MetadataReady;

        protected void OnMetadataReady(VisionDatalinkMetadata metadata)
        {
            var handler = MetadataReady;
            if (handler == null) return;

            var args = new VisionMetadataEventArgs { TsPid = 1, Metadata = metadata};
            handler(this, args);
        }
    }
}