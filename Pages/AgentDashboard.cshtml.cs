using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SignalRChat.Models;
using SignalRChat.Services;
using System.Linq;

namespace SignalRChat.Pages;

[AllowAnonymous]
public class AgentDashboardModel : PageModel
{
    private readonly ChatAPIService _chatAssignment;

    public AgentDashboardModel(ChatAPIService chatAssignment)
    {
        _chatAssignment = chatAssignment;
    }

    [BindProperty(SupportsGet = true)]
    public string? AgentId { get; set; }

    public Agent? CurrentAgent { get; private set; }
    public Team? CurrentTeam { get; private set; }
    public IEnumerable<AssigningChat> ActiveChats { get; private set; } = new List<AssigningChat>();
    public string? ErrorMessage { get; private set; }
    public IEnumerable<Team> AllTeams { get; private set; } = new List<Team>();

    public void OnGet()
    {
        // Get all teams with updated active status
        AllTeams = _chatAssignment.GetAllTeams();

        // If AgentId is provided, load specific agent info
        if (!string.IsNullOrEmpty(AgentId))
        {
            CurrentAgent = _chatAssignment.GetAgentById(AgentId);
            if (CurrentAgent != null)
            {
                CurrentTeam = _chatAssignment.GetTeamById(CurrentAgent.TeamId);
                ActiveChats = _chatAssignment.GetAgentActiveChats(AgentId);
            }
            else
            {
                // If agent not found, redirect to login
                Response.Redirect("/AgentLogin");
            }
        }
    }

    public IActionResult OnPostToggleAvailability(bool isAvailable)
    {
        if (string.IsNullOrEmpty(AgentId))
        {
            return RedirectToPage("/AgentLogin");
        }

        _chatAssignment.UpdateAgentAvailability(AgentId, isAvailable);
        return RedirectToPage(new { agentId = AgentId });
    }

    public IActionResult OnPostEndChat(string chatId)
    {
        if (string.IsNullOrEmpty(AgentId))
        {
            return RedirectToPage("/AgentLogin");
        }

        _chatAssignment.EndChat(chatId);
        return RedirectToPage(new { agentId = AgentId });
    }

    public IActionResult OnPostLogout()
    {
        if (!string.IsNullOrEmpty(AgentId))
        {
            _chatAssignment.UpdateAgentAvailability(AgentId, false);
        }
        return RedirectToPage("/AgentLogin");
    }

    public string GetAgentStatusText()
    {
        return CurrentAgent?.IsAvailable == true ? "Available" : "Unavailable";
    }

    public string GetAgentStatusBadgeClass()
    {
        return CurrentAgent?.IsAvailable == true ? "bg-success" : "bg-danger";
    }

    public string GetSeniorityBadgeClass()
    {
        return CurrentAgent?.Seniority switch
        {
            AgentSeniority.TeamLead => "bg-danger",
            AgentSeniority.Senior => "bg-warning",
            AgentSeniority.MidLevel => "bg-info",
            _ => "bg-secondary"
        };
    }

    public string GetTeamShiftTime()
    {
        if (CurrentTeam == null) return "";
        if (CurrentTeam.Shift == ShiftType.Overflow) return "Flexible Hours";
        return $"{CurrentTeam.ShiftStartTime:hh\\:mm} - {CurrentTeam.ShiftEndTime:hh\\:mm}";
    }

    public string GetUserConnectionStatus(AssigningChat chat)
    {
        return !string.IsNullOrEmpty(chat.UserConnectionId) ? "Connected" : "Disconnected";
    }

    public string GetChatDuration(AssigningChat chat)
    {
        var duration = DateTime.UtcNow - chat.AssignedTime;
        return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
    }

    public IEnumerable<AssigningChat> GetTeamActiveChats(string teamId)
    {
        return _chatAssignment.GetTeamActiveChats(teamId);
    }

    public IActionResult OnGetActiveChatsPartial(string agentId)
    {
        if (string.IsNullOrEmpty(agentId))
        {
            return new EmptyResult();
        }

        ActiveChats = _chatAssignment.GetAgentActiveChats(agentId);
        return new PartialViewResult
        {
            ViewName = "_ActiveChatsTable",
            ViewData = new ViewDataDictionary<IEnumerable<AssigningChat>>(ViewData, ActiveChats)
        };
    }

    public IActionResult OnGetRefreshActiveChats()
    {
        if (string.IsNullOrEmpty(AgentId))
        {
            return new JsonResult(new { error = "Agent ID is required" });
        }

        try
        {
            CurrentAgent = _chatAssignment.GetAgentById(AgentId);
            if (CurrentAgent != null)
            {
                ActiveChats = _chatAssignment.GetAgentActiveChats(AgentId);
                
                // Return the partial view with the current model
                var viewData = new ViewDataDictionary<AgentDashboardModel>(ViewData, this)
                {
                    { "Layout", "_PartialLayout" }
                };
                
                return new PartialViewResult
                {
                    ViewName = "_ActiveChatsTable",
                    ViewData = viewData
                };
            }
            
            return new JsonResult(new { error = "Agent not found" });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message });
        }
    }
}