using System.ComponentModel.DataAnnotations;

namespace SignalRChat.Models;

public class Agent
{
    [Key]
    public string AgentId { get; set; } = "";
    public string Name { get; set; } = "";
    public AgentSeniority Seniority { get; set; }
    public bool IsAvailable { get; set; }
    public string TeamId { get; set; } = "";
    public Team? Team { get; set; }
    public List<AssigningChat> ActiveChats { get; set; } = new();
} 