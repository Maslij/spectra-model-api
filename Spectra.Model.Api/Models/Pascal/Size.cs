using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Spectra.Model.Api.Models.Pascal
{
    public class Size
    {
        [JsonProperty("width")]
        [Required]
        public int Width { get; set; }

        [JsonProperty("height")]
        [Required]
        public int Height { get; set; }

        [JsonProperty("depth")]
        [Required]
        public int Depth { get; set; }
    }
}
