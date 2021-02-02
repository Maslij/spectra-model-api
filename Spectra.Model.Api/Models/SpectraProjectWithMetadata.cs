using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spectra.Model.Api.Models
{
    public class SpectraProjectWithMetadata
    {
        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("demo_urls")]
        public string[] DemoUrls { get; set; }

        [JsonProperty("created")]
        public DateTime Created { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("drModeEnabled")]
        public bool? DrModeEnabled { get; set; }

        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("lastModified")]
        public DateTime LastModified { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("Settings")]
        public Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models.ProjectSettings Settings { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("thumbnailUri")]
        public string ThumbnailUri { get; set; }
    }
}
