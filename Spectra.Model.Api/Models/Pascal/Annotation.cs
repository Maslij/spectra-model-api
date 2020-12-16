using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Spectra.Model.Api.Models.Pascal
{
    public class Annotation
    {

        [JsonProperty("folder")]
        [Required]
        public string Folder { get; set; } = "";

        [JsonProperty("filename")]
        [Required]
        public string FileName { get; set; }


        [JsonProperty("source")]
        [Required]
        public Source Source { get; set; }

        [JsonProperty("size")]
        [Required]
        public Size Size { get; set; }

        [JsonProperty("segmented")]
        [Required]
        public int Segmented { get; set; } = 0;

        [JsonProperty("objects")]
        [Required]
        public List<Object> Objects { get; set; }

    }
}
