using System.Text.Json.Serialization;

namespace PlanningPokerServer.Classes;

public class MessageModel
{
    [JsonPropertyName("message")]
    public string Message { get; set; }
    [JsonPropertyName("time")]
    public string Time { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

}
