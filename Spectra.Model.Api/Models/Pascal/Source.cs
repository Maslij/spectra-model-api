using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Spectra.Model.Api.Models.Pascal
{
    public class Source
    {

        [JsonProperty("database")]
        [Required]
        public string Database { get; set; } = "";
    }
}
