using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using PlaningPokerClient.Classes;

var ws = new ClientWebSocket();
string name;
bool isMod = false;
bool voteMode = false;

while (true)
{
    Console.Write("Enter your name: ");
    name = Console.ReadLine();
    Console.Write("Are you a moderator? (y/n): ");
    var mod = Console.ReadLine().ToLower();
    if (mod == "y")
    {
        isMod = true;
    }
    else if (mod == "n")
    {
        isMod = false;
    }
    else
    {
        Console.WriteLine("Invalid input! mod is not set");
    }
    break;
}

Console.WriteLine("Connecting to server...");
await ws.ConnectAsync(new Uri($"ws://localhost:6969/ws?name={name}&mod={isMod}"), CancellationToken.None);
Console.WriteLine("Connected!");

var receiveTask = Task.Run(async () =>
{
    var buffer = new byte[1024];
    while (ws.State == WebSocketState.Open)
    {
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            break;
        }
        var rawData = Encoding.UTF8.GetString(buffer, 0, result.Count);
        dynamic dynamic = (dynamic)rawData;
        try
        {
            var userMsg = JsonSerializer.Deserialize<Dictionary<string, dynamic>>(dynamic);
            MessageModel msg = JsonSerializer.Deserialize<MessageModel>(userMsg["Message"]);
            UserModel user = JsonSerializer.Deserialize<UserModel>(userMsg["Connection"]);
            if (msg.Type != null)
                RecieveAction(msg.Type);
            RecieveMessage(msg, user);
        }
        catch (Exception x)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(rawData);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
});

var sendTask = Task.Run(async () =>
{
    while (ws.State == WebSocketState.Open)
    {
        var message = Console.ReadLine();
        if (message == "exit")
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            break;
        }
        MessageModel msg = new MessageModel { Message = message, Type = voteMode ? "vote" : "" };

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }
});



async void RecieveMessage(MessageModel msg, UserModel user)
{
    if (user.IsMod == true)
    {
        Console.ForegroundColor = ConsoleColor.Red;
    }
    if (name == user.Name)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(new String(' ', Console.BufferWidth));
        //Console.SetCursorPosition(0, Console.CursorTop - 1);
    }
    Console.WriteLine(user.IsMod ? $"{msg.Time} [MOD]{user.Name}:\n{msg.Message}" : $"{msg.Time} {user.Name}:\n{msg.Message}");
    Console.ForegroundColor = ConsoleColor.White;
}

async void RecieveAction(string type)
{

    switch (type)
    {
        case "setVote":
            voteMode = true;
            Console.WriteLine("Vote with '#_' !");
            break;
        case "endVote":
            voteMode = false;
            break;
    }
}

await Task.WhenAny(sendTask, receiveTask);
if (ws.State != WebSocketState.Closed)
{
    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
}
await Task.WhenAll(sendTask, receiveTask);
