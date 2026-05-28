using System;

namespace PlatformAI.Infrastructure.Application
{
    public class Log : Entity
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string LoggerName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
        public string? StackTrace { get; set; }
    }
}
