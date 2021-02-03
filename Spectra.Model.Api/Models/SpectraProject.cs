using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spectra.Model.Api.Models
{
    public class SpectraProject
    {
        [BsonElement("_id")]
        [JsonProperty("_id")]
        public ObjectId Id { get; set; }

        [BsonElement("project_id")]
        [JsonProperty("project_id")]
        public string ProjectId { get; set; }

        [BsonElement("category")]
        [JsonProperty("category")]
        public string Category { get; set; }

        [BsonElement("demo_urls")]
        [JsonProperty("demo_urls")]
        public string[] DemoUrls { get; set; }
    }
}
