using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Spectra.Model.Api.Models
{
    public class CustomVisionPrediction
    {
        [JsonProperty("Endpoint")]
        [Required]
        public string Endpoint { get; set; }

        [JsonProperty("PredictionKey")]
        [Required]
        public string PredictionKey { get; set; } = null;

        [JsonProperty("ImageUrl")]
        [Required]
        public string ImageUrl { get; set; }
    }
}
