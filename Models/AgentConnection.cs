using System.ComponentModel.DataAnnotations;

namespace SignalRChat.Models;

public class AgentConnection
{
    [Key]
    public string ConnectionId { get; set; } = "";
    public string AgentId { get; set; } = "";
    public Agent? Agent { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
} 