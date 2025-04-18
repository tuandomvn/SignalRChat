using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using SignalRChat.Models;
using SignalRChat.Services;

namespace SignalRChat.Pages;

[AllowAnonymous]
public class DebugChatsModel : PageModel
{
    private readonly IDataRepository _dataRepository;

    public DebugChatsModel(IDataRepository dataRepo)
    {
        _dataRepository = dataRepo;
    }

    public IEnumerable<AssigningChat> ActiveChats { get; private set; } = new List<AssigningChat>();
    public Dictionary<string, string> AgentConnections { get; private set; } = new();
    public IEnumerable<Team> Teams { get; private set; } = new List<Team>();
    public IEnumerable<ConnectionAttempt> ConnectionAttempts { get; private set; } = new List<ConnectionAttempt>();

    public async Task OnGetAsync()
    {
        // Prevent caching
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        ActiveChats = _dataRepository.GetAllActiveChats();
        AgentConnections = _dataRepository.GetAllAgentConnections();
        Teams = _dataRepository.GetAllTeams();
        ConnectionAttempts = await _dataRepository.GetAllConnectionAttempts();
    }

    public async Task<IActionResult> OnPostDeleteAttemptAsync(int id)
    {
        if (await _dataRepository.DeleteConnectionAttempt(id))
        {
            return RedirectToPage();
        }
        return NotFound();
    }

    public string GetChatDuration(AssigningChat chat)
    {
        var duration = DateTime.UtcNow - chat.AssignedTime;
        return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
    }

    public string? GetAgentName(string agentId)
    {
        var agent = _dataRepository.GetAgentById(agentId);
        return agent?.Name ?? agentId;
    }
} 