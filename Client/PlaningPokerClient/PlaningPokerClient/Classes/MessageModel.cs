using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlaningPokerClient.Classes
{
    internal class MessageModel
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }
        [JsonPropertyName("time")]
        public string Time { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; set; }

    }
}
 