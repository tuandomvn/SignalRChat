using SignalRChat.Models;

namespace SignalRChat.Services;

public class AgentChatCoordinatorService
{
    private readonly ChatAPIService _chatAssignment;

    public AgentChatCoordinatorService(ChatAPIService chatAssignment)
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

    public AssigningChat? AssignUserToAgent(string connectionId, string displayName)
    {
        var currentTime = TimeSpan.FromHours(DateTime.Now.Hour);
        
        var teams = _chatAssignment.GetAllTeams();
        
        // Find current active team based on time
        var currentTeam = teams.FirstOrDefault(t => 
            t.IsActive && t.Shift != ShiftType.Overflow &&
            t.ShiftStartTime <= currentTime && currentTime <= t.ShiftEndTime);

        // If no current team is active, try overflow team
        if (currentTeam == null)
        {
            currentTeam = teams.FirstOrDefault(t => t.IsActive && t.Shift == ShiftType.Overflow);
        }

        // If still no team available, return null
        if (currentTeam == null)
        {
            Console.WriteLine("No active teams available at current time");
            return null;
        }

        // Try assigning to each seniority level in order
        var seniorityLevels = new[]
        {
            AgentSeniority.Junior,
            AgentSeniority.MidLevel,
            AgentSeniority.Senior,
            AgentSeniority.TeamLead
        };

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
                            Console.WriteLine($"Successfully added chat {chat.ChatId} to database");
                            return chat;
                        }
                        else
                        {
                            Console.WriteLine($"Failed to add chat {chat.ChatId} to database");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Agent {agent.Name} has reached max chats: {activeChats}/{maxChats}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"No available {level} agents in team {currentTeam.Name}");
            }
        }

        Console.WriteLine($"No agents with available capacity in team {currentTeam.Name}");
        return null;
    }
} 