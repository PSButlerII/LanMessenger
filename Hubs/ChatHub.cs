using LanMessenger.Models;
using LanMessenger.Services;
using Microsoft.AspNetCore.SignalR;

namespace LanMessenger.Hubs;

public class ChatHub : Hub
{
    private readonly MessageStore _store;

    public ChatHub(MessageStore store) => _store = store;

    public async Task SendMessage(string sender, string text)
    {
        sender = (sender ?? "").Trim();
        text = (text ?? "").Trim();

        if (string.IsNullOrWhiteSpace(sender)) sender = "Unknown";
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > 500) text = text[..500];

        var msg = new ChatMessage(DateTimeOffset.Now, sender, text);
        _store.Add(msg);

        await Clients.All.SendAsync("messageReceived", new
        {
            timestamp = msg.Timestamp.ToString("HH:mm:ss"),
            sender = msg.Sender,
            text = msg.Text
        });
    }
}
