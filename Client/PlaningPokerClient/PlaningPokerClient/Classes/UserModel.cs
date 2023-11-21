using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlaningPokerClient.Classes
{
    internal class UserModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("isMod")]
        public bool IsMod { get; set; }
    }
}
