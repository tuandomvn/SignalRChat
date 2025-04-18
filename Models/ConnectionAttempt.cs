using System;

namespace SignalRChat.Models;

public class ConnectionAttempt
{
    public int Id { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public DateTime AttemptTime { get; set; }
    public bool IsSuccessful { get; set; }
} 