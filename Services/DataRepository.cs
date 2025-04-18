using Microsoft.EntityFrameworkCore;
using SignalRChat.Models;
using SignalRChat.Data;

namespace SignalRChat.Services;

public class DataRepository : IDataRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DataRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    private ChatDbContext GetContext()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    }

    public bool AddAgentToTeam(string teamId, Agent agent)
    {
        using var context = GetContext();
        var team = context.Teams
            .Include(t => t.Agents)
            .FirstOrDefault(t => t.TeamId == teamId);
        if (team != null && !team.Agents.Any(a => a.AgentId == agent.AgentId))
        {
            agent.TeamId = teamId;
            team.Agents.Add(agent);
            UpdateTeamActiveStatus(team);
            context.SaveChanges();
            return true;
        }
        return false;
    }

    public bool UpdateAgentAvailability(string agentId, bool isAvailable)
    {
        using var context = GetContext();
        var agent = context.Agents
            .Include(a => a.Team)
            .FirstOrDefault(a => a.AgentId == agentId);

        if (agent != null)
        {
            var oldStatus = agent.IsAvailable;
            agent.IsAvailable = isAvailable;

            var team = context.Teams
                .Include(t => t.Agents)
                .FirstOrDefault(t => t.TeamId == agent.TeamId);

            if (team != null)
            {
                var oldTeamStatus = team.IsActive;
                UpdateTeamActiveStatus(team);
                UpdateAllTeamsActiveStatus(context);
                context.SaveChanges();

                return oldStatus != isAvailable || oldTeamStatus != team.IsActive;
            }
            context.SaveChanges();
            return oldStatus != isAvailable;
        }
        return false;
    }

    private void UpdateTeamActiveStatus(Team team)
    {
        team.IsActive = team.Agents.Any(a => a.IsAvailable);
    }

    private void UpdateAllTeamsActiveStatus(ChatDbContext context)
    {
        var teams = context.Teams.Include(t => t.Agents).ToList();
        foreach (var team in teams)
        {
            UpdateTeamActiveStatus(team);
        }
    }

    public void UpdateAgentConnection(string agentId, string connectionId)
    {
        using var context = GetContext();

        // Remove existing connections for this agent
        var existingConnections = context.AgentConnections
            .Where(c => c.AgentId == agentId)
            .ToList();
        context.AgentConnections.RemoveRange(existingConnections);

        // Add new connection
        context.AgentConnections.Add(new AgentConnection
        {
            ConnectionId = connectionId,
            AgentId = agentId
        });

        // Update agent availability
        UpdateAgentAvailability(agentId, true);

        context.SaveChanges();
    }

    public string? GetAgentConnectionId(string agentId)
    {
        using var context = GetContext();
        return context.AgentConnections
            .FirstOrDefault(c => c.AgentId == agentId)
            ?.ConnectionId;
    }

    private Team? GetCurrentShiftTeam()
    {
        using var context = GetContext();
        var currentTime = DateTime.Now.TimeOfDay;

        // First try to find a team whose shift exactly matches the current time
        var currentTeam = context.Teams
            .Include(t => t.Agents)
            .Where(t => t.IsActive && IsTimeInShift(currentTime, t.ShiftStartTime, t.ShiftEndTime))
            .OrderByDescending(t => t.Agents.Count(a => a.IsAvailable))
            .FirstOrDefault();

        if (currentTeam != null)
            return currentTeam;

        // If no team is currently on shift, return the overflow team
        return context.Teams
            .Include(t => t.Agents)
            .FirstOrDefault(t => t.Shift == ShiftType.Overflow && t.IsActive);
    }

    private bool IsTimeInShift(TimeSpan currentTime, TimeSpan shiftStart, TimeSpan shiftEnd)
    {
        if (shiftStart < shiftEnd)
        {
            return currentTime >= shiftStart && currentTime < shiftEnd;
        }
        else // Handle overnight shifts (e.g., 22:00 - 06:00)
        {
            return currentTime >= shiftStart || currentTime < shiftEnd;
        }
    }

    public Agent? GetAvailableAgent(string? teamId = null)
    {
        using var context = GetContext();
        Team? team;

        if (string.IsNullOrEmpty(teamId))
        {
            team = GetCurrentShiftTeam();
            if (team == null) return null;
        }
        else
        {
            team = context.Teams
                .Include(t => t.Agents)
                .FirstOrDefault(t => t.TeamId == teamId);
            if (team == null) return null;
        }

        // Get available agents from the team sorted by seniority and chat count
        var agent = team.Agents
            .Where(a => a.IsAvailable)
            .OrderByDescending(a => a.Seniority)
            .ThenBy(a => context.Chats.Count(c => c.AgentId == a.AgentId && c.IsActive))
            .FirstOrDefault();

        // If no agents available in the primary team and it's not the overflow team,
        // try the overflow team
        if (agent == null && team.Shift != ShiftType.Overflow)
        {
            var overflowTeam = context.Teams
                .Include(t => t.Agents)
                .FirstOrDefault(t => t.Shift == ShiftType.Overflow && t.IsActive);

            if (overflowTeam != null)
            {
                agent = overflowTeam.Agents
                    .Where(a => a.IsAvailable)
                    .OrderByDescending(a => a.Seniority)
                    .ThenBy(a => context.Chats.Count(c => c.AgentId == a.AgentId && c.IsActive))
                    .FirstOrDefault();
            }
        }

        return agent;
    }

    public bool EndChat(string chatId)
    {
        using var context = GetContext();
        var chat = context.Chats.FirstOrDefault(c => c.ChatId == chatId);
        if (chat != null)
        {
            chat.IsActive = false;
            context.SaveChanges();
            return true;
        }
        return false;
    }

    public IEnumerable<AssigningChat> GetAgentActiveChats(string agentId)
    {
        using var context = GetContext();
        var chats = context.Chats
            .Where(c => c.AgentId == agentId && c.IsActive)
            .ToList();

        Console.WriteLine($"Found {chats.Count} active chats for agent {agentId}");
        foreach (var chat in chats)
        {
            Console.WriteLine($"Chat {chat.ChatId} - IsActive: {chat.IsActive}");
        }

        return chats;
    }

    public IEnumerable<AssigningChat> GetTeamActiveChats(string teamId)
    {
        using var context = GetContext();
        return context.Chats
            .Where(c => c.TeamId == teamId && c.IsActive)
            .ToList();
    }

    public Agent? GetAgentById(string agentId)
    {
        if (string.IsNullOrEmpty(agentId)) return null;
        using var context = GetContext();
        return context.Agents
            .Include(a => a.Team)
            .FirstOrDefault(a => a.AgentId.ToLower() == agentId.ToLower());
    }

    public Team? GetTeamById(string teamId)
    {
        using var context = GetContext();
        return context.Teams
            .Include(t => t.Agents)
            .FirstOrDefault(t => t.TeamId == teamId);
    }

    public IEnumerable<Team> GetAllTeams()
    {
        using var context = GetContext();
        UpdateAllTeamsActiveStatus(context);
        context.SaveChanges();

        // Get data first, then sort in memory
        var teams = context.Teams
            .Include(t => t.Agents)
            .ToList(); // Execute query and bring data to memory

        // Sort in memory (LINQ to Objects)
        return teams
            .OrderBy(t => t.Shift == ShiftType.Overflow)
            .ThenBy(t => t.ShiftStartTime.TotalMinutes);
    }

    public AssigningChat? GetActiveChatByConnectionId(string connectionId)
    {
        using var context = GetContext();
        return context.Chats
            .Include(c => c.Agent)
            .FirstOrDefault(c => (c.UserConnectionId == connectionId || c.AgentConnectionId == connectionId)
            && c.IsActive);
    }

    public void HandleAgentDisconnection(string agentId)
    {
        using var context = GetContext();
        var agent = context.Agents
            .Include(a => a.Team)
            .FirstOrDefault(a => a.AgentId == agentId);

        if (agent != null)
        {
            // Just mark agent as unavailable
            agent.IsAvailable = false;

            // Remove the connection
            var connections = context.AgentConnections
                .Where(c => c.AgentId == agentId)
                .ToList();
            context.AgentConnections.RemoveRange(connections);

            // Update team status
            var team = context.Teams
                .Include(t => t.Agents)
                .FirstOrDefault(t => t.TeamId == agent.TeamId);
            if (team != null)
            {
                UpdateTeamActiveStatus(team);
            }

            context.SaveChanges();
        }
    }

    // This method should only be used when an agent is actually being removed from the system
    // Not for temporary disconnections
    [Obsolete("This method should not be used for temporary disconnections. Use HandleAgentDisconnection instead.")]
    public void RemoveAgent(string agentId)
    {
        using var context = GetContext();
        var agent = context.Agents.FirstOrDefault(a => a.AgentId == agentId);
        if (agent != null)
        {
            // End all active chats for this agent
            var agentChats = context.Chats
                .Where(c => c.AgentId == agentId && c.IsActive)
                .ToList();
            Console.WriteLine($"[RemoveAgent] Found {agentChats.Count} active chats for agent {agentId}");
            foreach (var chat in agentChats)
            {
                Console.WriteLine($"[RemoveAgent] Setting chat {chat.ChatId} IsActive from {chat.IsActive} to false");
                chat.IsActive = false;
            }

            // Remove all connections for this agent
            var connections = context.AgentConnections
                .Where(c => c.AgentId == agentId)
                .ToList();
            context.AgentConnections.RemoveRange(connections);

            // Update team status
            var team = context.Teams
                .Include(t => t.Agents)
                .FirstOrDefault(t => t.TeamId == agent.TeamId);
            if (team != null)
            {
                UpdateTeamActiveStatus(team);
            }

            context.SaveChanges();
        }
    }

    public string? GetAgentIdByConnectionId(string connectionId)
    {
        using var context = GetContext();
        return context.AgentConnections
            .FirstOrDefault(c => c.ConnectionId == connectionId)
            ?.AgentId;
    }

    public bool AddActiveChat(AssigningChat chat)
    {
        using var context = GetContext();
        Console.WriteLine($"[AddActiveChat] Adding new chat {chat.ChatId} with IsActive={chat.IsActive}");

        if (context.Chats.Any(c => c.ChatId == chat.ChatId))
        {
            Console.WriteLine($"[AddActiveChat] Chat {chat.ChatId} already exists");
            return false;
        }

        // Ensure IsActive is set to true
        chat.IsActive = true;
        Console.WriteLine($"[AddActiveChat] Ensured IsActive is true for chat {chat.ChatId}");

        context.Chats.Add(chat);
        try
        {
            context.SaveChanges();

            // Verify the chat was saved correctly by loading it fresh from the database
            context.Entry(chat).Reload();
            Console.WriteLine($"[AddActiveChat] After save - Chat {chat.ChatId} IsActive: {chat.IsActive}");

            // Double check by querying
            var savedChat = context.Chats
                .AsNoTracking()
                .FirstOrDefault(c => c.ChatId == chat.ChatId);
            Console.WriteLine($"[AddActiveChat] Queried from db - Chat {chat.ChatId} IsActive: {savedChat?.IsActive}");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AddActiveChat] Error saving chat: {ex.Message}");
            return false;
        }
    }

    public AssigningChat? GetActiveChat(string chatId)
    {
        using var context = GetContext();
        return context.Chats
            .Include(c => c.Agent)
            .FirstOrDefault(c => c.ChatId == chatId && c.IsActive);
    }

    public IEnumerable<AssigningChat> GetAllActiveChats()
    {
        using var context = GetContext();
        var chats = context.Chats
            .Include(c => c.Agent)
            //.Where(c => c.IsActive)
            .ToList();

        Console.WriteLine($"Found {chats.Count} active chats");
        foreach (var chat in chats)
        {
            Console.WriteLine($"Chat {chat.ChatId} - IsActive: {chat.IsActive}");
        }

        return chats;
    }

    public Dictionary<string, string> GetAllAgentConnections()
    {
        using var context = GetContext();
        return context.AgentConnections
            .ToDictionary(c => c.ConnectionId, c => c.AgentId);
    }

    public void UpdateChatAgentConnection(string chatId, string agentConnectionId)
    {
        using var context = GetContext();
        var chat = context.Chats.FirstOrDefault(c => c.ChatId == chatId);
        if (chat != null)
        {
            chat.AgentConnectionId = agentConnectionId;
            context.SaveChanges();
        }
    }

    public async Task SaveChatMessage(ChatMessage message)
    {
        using var context = GetContext();
        using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            // Verify the chat exists and is active
            var chat = await context.Chats
                .FirstOrDefaultAsync(c => c.ChatId == message.ChatId && c.IsActive);

            if (chat == null)
            {
                throw new InvalidOperationException($"Chat {message.ChatId} not found or is not active");
            }

            // Add and save the message
            await context.ChatMessages.AddAsync(message);
            await context.SaveChangesAsync();

            // Commit the transaction
            await transaction.CommitAsync();

            Console.WriteLine($"Message saved successfully in chat {message.ChatId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SaveChatMessage: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            // Rollback the transaction
            await transaction.RollbackAsync();
            throw; // Re-throw the exception to be handled by the caller
        }
    }

    public async Task<IEnumerable<ChatMessage>> GetChatHistory(string chatId)
    {
        using var context = GetContext();
        var messages = await context.ChatMessages
            .Where(m => m.ChatId == chatId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
        return messages;
    }

    public AssigningChat? GetActiveChatByDisplayName(string displayName)
    {
        using var context = GetContext();
        return context.Chats
            .Include(c => c.Agent)
            .FirstOrDefault(c => c.DisplayName == displayName && c.IsActive);
    }

    public async Task UpdateUserConnection(string chatId, string connectionId)
    {
        using var context = GetContext();
        var chat = await context.Chats.FirstOrDefaultAsync(c => c.ChatId == chatId);
        if (chat != null)
        {
            chat.UserConnectionId = connectionId;
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateChatDisplayName(string chatId, string displayName)
    {
        using var context = GetContext();
        var chat = await context.Chats.FirstOrDefaultAsync(c => c.ChatId == chatId);
        if (chat != null)
        {
            chat.DisplayName = displayName;
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> SaveConnectionAttempt(string connectionId, string displayName)
    {
        using var context = GetContext();

        // Find existing attempt for this connection
        var existingAttempt = await context.ConnectionAttempts
            .FirstOrDefaultAsync(ca => ca.ConnectionId == connectionId);

        if (existingAttempt != null)
        {
            // Update existing attempt
            existingAttempt.DisplayName = displayName;
            existingAttempt.AttemptNumber++;
            existingAttempt.AttemptTime = DateTime.UtcNow;
            existingAttempt.IsSuccessful = true;
        }
        else
        {
            // Create new attempt
            var attempt = new ConnectionAttempt
            {
                ConnectionId = connectionId,
                DisplayName = displayName,
                AttemptNumber = 1,
                AttemptTime = DateTime.UtcNow,
                IsSuccessful = true
            };
            context.ConnectionAttempts.Add(attempt);
        }

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetConnectionAttemptsCount(string connectionId)
    {
        using var context = GetContext();
        return await context.ConnectionAttempts
            .Where(ca => ca.ConnectionId == connectionId)
            .CountAsync();
    }

    public async Task<IEnumerable<ConnectionAttempt>> GetAllConnectionAttempts()
    {
        using var context = GetContext();
        return await context.ConnectionAttempts
            .OrderByDescending(ca => ca.AttemptTime)
            .ToListAsync();
    }

    public async Task<bool> DeleteConnectionAttempt(int id)
    {
        using var context = GetContext();
        var attempt = await context.ConnectionAttempts.FindAsync(id);
        if (attempt != null)
        {
            context.ConnectionAttempts.Remove(attempt);
            await context.SaveChangesAsync();
            return true;
        }
        return false;
    }
}