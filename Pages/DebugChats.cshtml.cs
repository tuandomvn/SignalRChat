using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using SignalRChat.Models;
using SignalRChat.Services;

namespace SignalRChat.Pages;

[AllowAnonymous]
public class DebugChatsModel : PageModel
{
    private readonly IDataRepository _chatAssignment;

    public DebugChatsModel(IDataRepository chatAssignment)
    {
        _chatAssignment = chatAssignment;
    }

    public IEnumerable<AssigningChat> ActiveChats { get; private set; } = new List<AssigningChat>();
    public Dictionary<string, string> AgentConnections { get; private set; } = new();
    public IEnumerable<Team> Teams { get; private set; } = new List<Team>();

    public void OnGet()
    {
        // Prevent caching
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        ActiveChats = _chatAssignment.GetAllActiveChats();
        AgentConnections = _chatAssignment.GetAllAgentConnections();
        Teams = _chatAssignment.GetAllTeams();
    }

    public string GetChatDuration(AssigningChat chat)
    {
        var duration = DateTime.UtcNow - chat.AssignedTime;
        return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
    }

    public string? GetAgentName(string agentId)
    {
        var agent = _chatAssignment.GetAgentById(agentId);
        return agent?.Name ?? agentId;
    }
} 