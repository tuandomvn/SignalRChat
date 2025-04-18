using SignalRChat.Models;

namespace SignalRChat.Services;

public interface IAgentChatCoordinatorService
{
    Team? GetAvailableTeam(TimeSpan currentTime);
    AssigningChat? AssignUserToAgent(string connectionId, string displayName, Team currentTeam);
} 