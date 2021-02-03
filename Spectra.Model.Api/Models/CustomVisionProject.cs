using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Spectra.Model.Api.Models
{
    public class CustomVisionProject
    {
        [JsonProperty("Endpoint")]
        [Required]
        public string Endpoint { get; set; }

        [JsonProperty("TrainingKey")]
        [Required]
        public string TrainingKey { get; set; }

        [JsonProperty("PredictionKey")]
        [Required]
        public string PredictionKey { get; set; } = null;
    }
}
