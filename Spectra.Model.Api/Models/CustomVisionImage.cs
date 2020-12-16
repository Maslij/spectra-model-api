using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Spectra.Model.Api.Models
{
    public class CustomVisionBatchImage: CustomVisionProject
    {
        [JsonProperty("BatchImage")]
        [Required]
        public CustomVisionImage[] BatchImage { get; set; }
    }

    public class CustomVisionImage
    {
        [JsonProperty("ImageUri")]
        [Required]
        public string ImageUri { get; set; }
    }
}
