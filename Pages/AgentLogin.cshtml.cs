using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SignalRChat.Services;

namespace SignalRChat.Pages;

public class AgentLoginModel : PageModel
{
    private readonly IChatAPIService _chatAssignment;

    public AgentLoginModel(IChatAPIService chatAssignment)
    {
        _chatAssignment = chatAssignment;
    }

    [BindProperty]
    public string AgentId { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        var agent = _chatAssignment.GetAgentById(AgentId);
        if (agent == null)
        {
            ErrorMessage = "Agent not found. Please check your credentials.";
            return Page();
        }

        // Update agent availability
        _chatAssignment.UpdateAgentAvailability(AgentId, true);
        
        return RedirectToPage("/AgentDashboard", new { agentId = AgentId });
    }
} 