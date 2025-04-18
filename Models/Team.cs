using System.ComponentModel.DataAnnotations;

namespace SignalRChat.Models;

public class Team
{
    [Key]
    public string TeamId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ShiftType Shift { get; set; }
    public TimeSpan ShiftStartTime { get; set; }
    public TimeSpan ShiftEndTime { get; set; }
    public bool IsActive { get; set; }
    public List<Agent> Agents { get; set; } = new();
} 