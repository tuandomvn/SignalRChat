using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SignalRChat.Models;

public class AssigningChat
{
    [Key]
    public string ChatId { get; set; } = Guid.NewGuid().ToString();
    public string AgentId { get; set; } = "";
    public Agent? Agent { get; set; }
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? UserConnectionId { get; set; }
    public string? AgentConnectionId { get; set; }
    public string TeamId { get; set; } = "";
    public Team? Team { get; set; }
    public DateTime AssignedTime { get; set; } = DateTime.UtcNow;
    
    [Required]
    [Column(TypeName = "INTEGER")]
    public bool IsActive { get; set; } = true;
} 