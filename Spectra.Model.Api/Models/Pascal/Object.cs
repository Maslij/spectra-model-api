using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Spectra.Model.Api.Models.Pascal
{
    public class Object
    {
        [JsonProperty("name")]
        [Required]
        public string Name { get; set; }

        [JsonProperty("pose")]
        [Required]
        public string Pose { get; set; } = "Unspecified";

        [JsonProperty("truncated")]
        [Required]
        public int Truncated { get; set; } = 0;

        [JsonProperty("difficult")]
        [Required]
        public int Difficult { get; set; } = 0;

        [JsonProperty("bndbox")]
        [Required]
        public BoundBox Bndbox { get; set; }
    }
}
