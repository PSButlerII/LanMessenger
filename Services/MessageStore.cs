using System.Collections.Concurrent;
using LanMessenger.Models;

namespace LanMessenger.Services;

public class MessageStore
{
    private readonly ConcurrentQueue<ChatMessage> _messages = new();
    private const int MaxMessages = 200; // keep it light

    public IReadOnlyList<ChatMessage> GetLatest()
        => _messages.ToArray().OrderBy(m => m.Timestamp).ToList();

    public void Add(ChatMessage message)
    {
        _messages.Enqueue(message);

        while (_messages.Count > MaxMessages && _messages.TryDequeue(out _)) { }
    }
}
