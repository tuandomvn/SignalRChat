using SignalRChat.Models;

namespace SignalRChat.Services;

public class AgentChatCoordinatorService : IAgentChatCoordinatorService
{
    private readonly IDataRepository _chatAssignment;

    public AgentChatCoordinatorService(IDataRepository chatAssignment)
    {
        _chatAssignment = chatAssignment;
    }

    private int GetMaxChatsForSeniority(AgentSeniority seniority)
    {
        return seniority switch
        {
            AgentSeniority.Junior => 4,
            AgentSeniority.MidLevel => 6,
            AgentSeniority.Senior => 8,
            AgentSeniority.TeamLead => 5,
            _ => 4 // Default to Junior limit
        };
    }

    public int CalculateTeamCapacity(Team team)
    {
        if (team == null || !team.Agents.Any())
            return 0;

        var baseCapacity = team.Agents
            .Where(a => a.IsAvailable)
            .Sum(a => GetMaxChatsForSeniority(a.Seniority));

        return (int)Math.Floor(baseCapacity * 1.5);
    }

    private IEnumerable<Agent> GetAvailableAgentsByLevel(Team team, AgentSeniority level)
    {
        return team.Agents
            .Where(a => a.IsAvailable && a.Seniority == level)
            .Select(a => new
            {
                Agent = a,
                ChatCount = _chatAssignment.GetAgentActiveChats(a.AgentId).Count()
            })
            .OrderBy(x => x.ChatCount) // Sort by current chat count
            .Select(x => x.Agent);
    }

    public AssigningChat? AssignUserToAgent(string connectionId, string displayName, Team currentTeam)
    {
        // Try assigning to each seniority level in order
        var seniorityLevels = Enum.GetValues(typeof(AgentSeniority))
            .Cast<AgentSeniority>()
            .OrderBy(s => (int)s)
            .ToArray();

        foreach (var level in seniorityLevels)
        {
            //For ex: Junior1: 3 chats, Junior2: 4 chats, Junior3: 5 chats
            var agentsAtLevel = GetAvailableAgentsByLevel(currentTeam, level).ToList();

            if (agentsAtLevel.Any())
            {
                Console.WriteLine($"Found {agentsAtLevel.Count} available {level} agents in team {currentTeam.Name}");

                foreach (var agent in agentsAtLevel)
                {
                    var activeChats = _chatAssignment.GetAgentActiveChats(agent.AgentId).Count();
                    var maxChats = GetMaxChatsForSeniority(level);

                    if (activeChats < maxChats)
                    {
                        Console.WriteLine($"Assigning to {level} agent: {agent.Name} " +
                                        $"(current chats: {activeChats}, max: {maxChats})");

                        var chat = new AssigningChat
                        {
                            AgentId = agent.AgentId,
                            UserId = connectionId,
                            UserConnectionId = connectionId,
                            DisplayName = displayName,
                            TeamId = currentTeam.TeamId,
                            AgentConnectionId = null, // Will be set when agent joins the chat
                            IsActive = true
                        };

                        if (_chatAssignment.AddActiveChat(chat))
                        {
                            return chat;
                        }
                    }
                }
            }
        }

        return null;
    }

    public Team? GetAvailableTeam(TimeSpan currentTime)
    {
        var teams = _chatAssignment.GetAllTeams();

        // Find current active team based on time
        var currentTeam = GetCurrentPrimaryTeam(currentTime, teams);
        var activeChatCount = GetCurrentActiveChatsByTeam(currentTeam);

        // If no current team is inactive, try overflow team
        if (activeChatCount == -1 || activeChatCount >= CalculateTeamCapacity(currentTeam))
        {
            currentTeam = teams.FirstOrDefault(t => t.IsActive && t.Shift == ShiftType.Overflow);
        }

        return currentTeam;
    }

    private Team? GetCurrentPrimaryTeam(TimeSpan currentTime, IEnumerable<Team> teams)
    {
        return teams.FirstOrDefault(t =>
            t.IsActive && t.Shift != ShiftType.Overflow &&
            t.ShiftStartTime <= currentTime && currentTime <= t.ShiftEndTime);
    }

    public int GetCurrentActiveChatsByTeam(Team team)
    {
        if (team == null)
            return -1;

        return _chatAssignment.GetTeamActiveChats(team.TeamId).Count();
    }
}