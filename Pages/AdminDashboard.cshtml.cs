using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using SignalRChat.Models;
using SignalRChat.Services;

namespace SignalRChat.Pages;

[AllowAnonymous]
public class AdminDashboardModel : PageModel
{
    private readonly DataRepository _chatAssignment;

    public AdminDashboardModel(DataRepository chatAssignment)
    {
        _chatAssignment = chatAssignment;
    }

    public IEnumerable<Team> Teams { get; private set; } = new List<Team>();
    public int TotalAgents { get; private set; }
    public int AvailableAgents { get; private set; }
    public int ActiveChats { get; private set; }

    public void OnGet()
    {
        // Get all teams
        Teams = _chatAssignment.GetAllTeams();

        // Calculate statistics
        TotalAgents = Teams.Sum(t => t.Agents.Count);
        AvailableAgents = Teams.Sum(t => t.Agents.Count(a => a.IsAvailable));
        ActiveChats = Teams.Sum(t => _chatAssignment.GetTeamActiveChats(t.TeamId).Count());
    }

    public IEnumerable<AssigningChat> GetAgentActiveChats(string agentId)
    {
        return _chatAssignment.GetAgentActiveChats(agentId);
    }
} 