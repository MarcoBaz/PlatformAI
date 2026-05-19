using System;

namespace PlatformAI.Infrastructure.Application ;

public class Conversation:Entity
{
     public string Title { get; set; } = string.Empty;
     public Guid UserId { get; set; }
     public List<Message> Messages { get; set; } = new List<Message>();
}
