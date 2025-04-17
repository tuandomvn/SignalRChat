using System;
using System.ComponentModel.DataAnnotations;

namespace SignalRChat.Models;

public class ChatMessage
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string ChatId { get; set; }
    
    [Required]
    public string SenderName { get; set; }
    
    [Required]
    public string Content { get; set; }
    
    public bool IsFromAgent { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
} 