using System;
using PlatformAI.Infrastructure.VM;

namespace PlatformAI.Analytics.DTO;

  /// <summary>
    /// Response per la chat completa (non streaming)
    /// </summary>
    public class ConversationResponse
    {
        public string ConversationId { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public ConversationVM? Conversation { get; set; }
    }

    /// <summary>
    /// Request per lo streaming
    /// </summary>
    public class StreamingRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }