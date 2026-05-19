namespace PlatformAI.Infrastructure.VM ;

public class ConversationVM:BaseVM
{
     public string Title { get; set; } = string.Empty;
     public string UserId { get; set; }
     public List<MessageVM> Messages { get; set; } = new List<MessageVM>();
}
public class MessageVM:BaseVM
{
        public string ConversationId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsAnswer { get; set; } = false;
        public bool IsLoading { get; set; } = false;
        public string? ChartsJson { get; set; }
}

public class ConversationRequest
{
    public string UserId { get; set; }
    public MessageVM Message { get; set; } = new MessageVM();
}
