using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SignalRChat.Services;

namespace SignalRChat.Pages;

public class AgentLoginModel : PageModel
{
    private readonly IDataRepository _dataRepository;

    public AgentLoginModel(IDataRepository chatAssignment)
    {
        _dataRepository = chatAssignment;
    }

    [BindProperty]
    public string AgentId { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        var agent = _dataRepository.GetAgentById(AgentId);
        if (agent == null)
        {
            ErrorMessage = "Agent not found. Please check your credentials.";
            return Page();
        }

        // Update agent availability
        _dataRepository.UpdateAgentAvailability(AgentId, true);
        
        return RedirectToPage("/AgentDashboard", new { agentId = AgentId });
    }
} 