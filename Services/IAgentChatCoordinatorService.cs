using SignalRChat.Models;

namespace SignalRChat.Services;

public interface IAgentChatCoordinatorService
{
    AssigningChat? AssignUserToAgent(string connectionId, string displayName, TimeSpan currentTime);
} 