using SignalRChat.Models;

namespace SignalRChat.Services;

public interface IChatAPIService
{
    Agent? GetAgentById(string agentId);
    string? GetAgentConnectionId(string agentId);
    void UpdateAgentConnection(string agentId, string connectionId);
    AssigningChat? GetActiveChatByConnectionId(string connectionId);
    AssigningChat? GetActiveChat(string chatId);
    bool EndChat(string chatId);
    bool UpdateAgentAvailability(string agentId, bool isAvailable);
    string? GetAgentIdByConnectionId(string connectionId);
    void HandleAgentDisconnection(string agentId);
    IEnumerable<AssigningChat> GetAllActiveChats();
    Task<IEnumerable<ChatMessage>> GetChatHistory(string chatId);
    Task SaveChatMessage(ChatMessage message);
    void UpdateChatAgentConnection(string chatId, string agentConnectionId);
    void DebugDatabaseContent();
    
    // Add missing methods
    IEnumerable<AssigningChat> GetAgentActiveChats(string agentId);
    IEnumerable<Team> GetAllTeams();
    bool AddActiveChat(AssigningChat chat);
    IEnumerable<AssigningChat> GetTeamActiveChats(string teamId);
    Team? GetTeamById(string teamId);
    bool RegisterTeam(Team team);
    bool AddAgentToTeam(string teamId, Agent agent);
    Agent? GetAvailableAgent(string? teamId = null);
    AssigningChat? GetActiveChatByDisplayName(string displayName);
    Task UpdateUserConnection(string chatId, string connectionId);
    Task UpdateChatDisplayName(string chatId, string displayName);
    Dictionary<string, string> GetAllAgentConnections();
} 