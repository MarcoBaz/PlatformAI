using System;

namespace PlatformAI.Infrastructure.Application;

 public class MachineEvent : Entity
    {
        public Guid MachineId { get; set; }
        public Machine Machine { get; set; } = null!;

        public string EventType { get; set; } = string.Empty;
        public DateTime EventTime { get; set; }
        public string? Message { get; set; }
    }
