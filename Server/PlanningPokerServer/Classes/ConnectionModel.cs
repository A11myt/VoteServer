namespace PlanningPokerServer.Classes;
using System.Net.WebSockets;
using System.Text.Json.Serialization;

public class ConnectionModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("isMod")]
    public bool IsMod { get; set; }
    [JsonPropertyName("socket")]
    public WebSocket Socket { get; set; }
}
