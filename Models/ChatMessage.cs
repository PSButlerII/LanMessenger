namespace LanMessenger.Models;

    public record ChatMessage(    
        DateTimeOffset Timestamp,
        string Sender,
        string Text
    );

