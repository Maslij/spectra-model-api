using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Spectra.Model.Api.Models.Pascal
{
    public class BoundBox
    {
        [JsonProperty("xmin")]
        [Required]
        public double Xmin { get; set; }

        [JsonProperty("ymin")]
        [Required]
        public double Ymin { get; set; }

        [JsonProperty("xmax")]
        [Required]
        public double Xmax { get; set; }

        [JsonProperty("ymax")]
        [Required]
        public double Ymax { get; set; }
    }
}
