using System;
using System.Dynamic;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks.Dataflow;
using System.Xml.Linq;
using PlanningPokerServer.Classes;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:6969");
var app = builder.Build();
app.UseWebSockets();

var connections = new List<WebSocket>();
List<ConnectionModel> connectionsModel = new List<ConnectionModel>();
List<KeyValuePair<ConnectionModel, int>> votes = new List<KeyValuePair<ConnectionModel, int>>();
bool voteMode = false;

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        string user = context.Request.Query["name"];
        bool isMod = Convert.ToBoolean(context.Request.Query["mod"]);
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        connections.Add(ws);
        ConnectionModel currentConnection = new ConnectionModel()
        {
            Name = user,
            IsMod = isMod,
            Socket = ws
        };
        connectionsModel.Add(currentConnection);
        if (currentConnection.IsMod)
        {
            var ModUserExists = connectionsModel.Any(x => x.IsMod == true && x != currentConnection);
            if (ModUserExists)
            {
                var modUser = connectionsModel.FirstOrDefault(x => x.IsMod == true);
                await MessageToUser(connectionsModel.Last(), $"{modUser.Name} is already Mod");
                currentConnection.IsMod = false;
            }
            if (currentConnection.IsMod)
            {
                await MessageToUser(currentConnection, "You are now Mod! \nu have special Functions! \n#1. Start Vote(@1)");
            }
        }
        await Broadcast($"{user} joined the room");
        if (voteMode)
            await MessageToUser(currentConnection, "", "setVote");
        await Broadcast($"{connections.Count} users connected");
        await RecieveMessage(ws,
                  async (result, buffer) =>
                  {
                      if (result.MessageType == WebSocketMessageType.Text)
                      {
                          var rawData = Encoding.UTF8.GetString(buffer, 0, result.Count);
                          MessageModel msg = JsonSerializer.Deserialize<MessageModel>(rawData);
                          msg.Time = DateTime.Now.ToString("h:mm:ss");
                          if (currentConnection.IsMod)
                              ModFunction(msg, currentConnection);
                          else
                          {
                              if (msg.Type != null)
                                  UserFunction(msg, currentConnection);
                          }
                      }
                      else if (result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted)
                      {
                          connections.Remove(ws);
                          await Broadcast($"{user} left the room");
                          await Broadcast($"{connections.Count} users connected");
                          await ws.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                      }
                  });
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

async Task RecieveMessage(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
{
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        handleMessage(result, buffer);
    }
}

async Task Broadcast(string message)
{
    byte[] bytes = Encoding.UTF8.GetBytes(message);
    foreach (var socket in connections)
    {
        if (socket.State == WebSocketState.Open)
        {
            var arraySegement = new ArraySegment<byte>(bytes, 0, bytes.Length);
            await socket.SendAsync(arraySegement, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}

async Task BroadcastMessage(MessageModel msg, ConnectionModel connection)
{
    byte[] bytes;
    dynamic obj = new ExpandoObject();
    obj.Message = msg;
    obj.Connection = new ConnectionModel { Name = connection.Name, IsMod = connection.IsMod };
    var json = JsonSerializer.Serialize(obj);
    bytes = Encoding.UTF8.GetBytes(json);
    foreach (var socket in connections)
    {
        if (socket.State == WebSocketState.Open)
        {
            var arraySegement = new ArraySegment<byte>(bytes, 0, bytes.Length);
            await socket.SendAsync(arraySegement, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}

async Task MessageToUser(ConnectionModel connection, string message, string type = null)
{
    byte[] bytes;
    var msg = new MessageModel()
    {
        Message = message,
        Time = DateTime.Now.ToString("h:mm:ss"),
        Type = type
    };

    var json = JsonSerializer.Serialize(msg);
    bytes = Encoding.UTF8.GetBytes(json);
    if (connection.Socket.State == WebSocketState.Open)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        var arraySegment = new ArraySegment<byte>(buffer);
        await connection.Socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}

#region mod functions

void ModFunction(MessageModel messageModel, ConnectionModel connection)
{
    switch (messageModel.Message)
    {
        case "@1":
            StartVote(messageModel, connection);
            break;
        case "@2":
            ListVotes(messageModel, connection);
            break;
        case "@3":
            EndVote(messageModel, connection);
            break;
        default:
            BroadcastMessage(messageModel, connection);
            break;
    }
}

async Task EndVote(MessageModel messageModel, ConnectionModel connection)
{
    voteMode = false;
    MessageModel msg = new MessageModel()
    {
        Message = "Votemode deactivated!",
        Type = "setCloseVote",
    };
    await BroadcastMessage(msg, connection);
}

async Task StartVote(MessageModel messageModel, ConnectionModel connection)
{
    voteMode = true;
    MessageModel msg = new MessageModel()
    {
        Message = "Votemode activated!",
        Type = "setVote",
    };
    await BroadcastMessage(msg, connection);
}

#endregion
#region User Functions
void UserFunction(MessageModel messageModel, ConnectionModel connection)
{
    switch (messageModel.Type)
    {

        case "vote":
            Vote(messageModel, connection);
            break;
        default:
            BroadcastMessage(messageModel, connection);
            break;
    }
}
async Task ListVotes(MessageModel messageModel, ConnectionModel connection)
{
    //List all votes and highlight the lowest and highes
    var modUser = connectionsModel.FirstOrDefault(x => x.IsMod == true);
    var lowest = votes.OrderBy(x => x.Value).FirstOrDefault();
    var highest = votes.OrderByDescending(x => x.Value).FirstOrDefault();
    MessageModel msg = new MessageModel();
    string message = "";
    foreach (var vote in votes)
    {

        if (vote.Value == lowest.Value)
        {
            message = message + $"{vote.Key.Name} voted {vote.Value} (lowest)\n";
        }
        else if (vote.Value == highest.Value)
        {
            message = message + $"{vote.Key.Name} voted {vote.Value} (highest)\n";
        }
        else
        {
            message = message + $"{vote.Key.Name} voted {vote.Value}\n";
        }
    }
    msg.Message = message;
    await BroadcastMessage(msg, modUser);

}

async Task Vote(MessageModel messageModel, ConnectionModel connection)
{
    if (messageModel.Message.StartsWith("#"))
    {
        //only accept fibonaaci numbers
        var vote = messageModel.Message.Split("#")[1];
        if (votes.Any(x => x.Key == connection))
        {
            var oldVote = votes.FirstOrDefault(x => x.Key == connection);
            votes.Remove(oldVote);
        }
        votes.Add(new KeyValuePair<ConnectionModel, int>(connection, Convert.ToInt32(vote)));

        await MessageToUser(connection, "Voted");
        var modUser = connectionsModel.FirstOrDefault(x => x.IsMod == true);
        await MessageToUser(modUser, $"{connection.Name} has Voted!");
    }
    else
    {
        BroadcastMessage(messageModel, connection);
    }
}

#endregion


await app.RunAsync();
