using System;

namespace PlatformAI.Infrastructure.Application;

public class Message:Entity
{
        public Guid ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;
        public string Content { get; set; } = string.Empty;
        public bool IsAnswer { get; set; } = false;
        public string? ChartsJson { get; set; }
}
