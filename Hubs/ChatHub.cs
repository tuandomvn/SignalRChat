using Microsoft.AspNetCore.SignalR;
using SignalRChat.Models;
using SignalRChat.Services;

namespace SignalRChat.Hubs;

public class ChatHub : Hub
{
    private readonly IDataRepository _chatAPIService;
    private readonly IAgentChatCoordinatorService _coordinator;

    public ChatHub(IDataRepository chatAssignment, IAgentChatCoordinatorService coordinator)
    {
        _chatAPIService = chatAssignment;
        _coordinator = coordinator;
    }

    //When Agent login
    public async Task RegisterConnection(string agentId)
    {
        // Check if agent exists
        var agent = _chatAPIService.GetAgentById(agentId);
        if (agent == null)
        {
            throw new HubException("Agent not found.");
        }

        // Remove old connection if exists
        var existingConnectionId = _chatAPIService.GetAgentConnectionId(agentId);
        if (!string.IsNullOrEmpty(existingConnectionId))
        {
            await Groups.RemoveFromGroupAsync(existingConnectionId, "Agents");
        }

        // Update connection and add to group
        _chatAPIService.UpdateAgentConnection(agentId, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "Agents");
    }

    //When user want to starts a chat
    public async Task StartChat(string displayName)
    {
        // Check if user already has an active chat
        var existingChat = _chatAPIService.GetActiveChatByConnectionId(Context.ConnectionId);
        if (existingChat != null)
        {
            var existingAgent = _chatAPIService.GetAgentById(existingChat.AgentId);
            if (existingAgent != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, existingChat.ChatId);
                var agentConnectionId = _chatAPIService.GetAgentConnectionId(existingAgent.AgentId);
                if (!string.IsNullOrEmpty(agentConnectionId))
                {
                    await Groups.AddToGroupAsync(agentConnectionId, existingChat.ChatId);
                    await Clients.Caller.SendAsync("ChatAssigned", "You are connected to " + existingAgent.Name);
                }
                return;
            }
        }

        // Get available team based on current time
        Team? currentTeam = _coordinator.GetAvailableTeam(TimeSpan.FromHours(DateTime.Now.Hour));
        if (currentTeam == null)
        {
            await Clients.Caller.SendAsync("NoChatAssigned", "No agents available at the moment. Please try again later.");
            return;
        }

        var chat = _coordinator.AssignUserToAgent(Context.ConnectionId, displayName, currentTeam);
        if (chat != null)
        {
            var agent = _chatAPIService.GetAgentById(chat.AgentId);
            if (agent != null)
            {
                // Add both user and agent to the chat group
                await Groups.AddToGroupAsync(Context.ConnectionId, chat.ChatId);

                // Get agent's current connection and add to group
                var agentConnectionId = _chatAPIService.GetAgentConnectionId(agent.AgentId);
                if (!string.IsNullOrEmpty(agentConnectionId))
                {
                    await Groups.AddToGroupAsync(agentConnectionId, chat.ChatId);

                    // Update the chat's AgentConnectionId
                    _chatAPIService.UpdateChatAgentConnection(chat.ChatId, agentConnectionId);

                    // Notify both parties
                    await Clients.Caller.SendAsync("ChatAssigned", "You are connected to " + agent.Name);
                    await Clients.Client(agentConnectionId).SendAsync("NewChat",
                        new { ChatId = chat.ChatId, UserId = displayName });

                    // Send a system message to the chat group about the connection
                    await Clients.Group(chat.ChatId).SendAsync("ReceiveMessage",
                        "System",
                        $"Chat started. {displayName} is now connected with {agent.Name}.",
                        chat.ChatId);
                }
            }
        }
        else
        {
            await Clients.Caller.SendAsync("NoChatAssigned", "No agents available at the moment. Please try again later.");
        }
    }

    //When both sides send messages
    public async Task SendMessage(string user, string message)
    {
        var chat = _chatAPIService.GetActiveChatByConnectionId(Context.ConnectionId);

        if (chat != null)
        {
            // Create and store the message
            var chatMessage = new ChatMessage
            {
                ChatId = chat.ChatId,
                SenderName = user,
                Content = message,
                IsFromAgent = Context.ConnectionId == chat.AgentConnectionId
            };

            await _chatAPIService.SaveChatMessage(chatMessage);

            // Send to all clients in the group
            await Clients.Group(chat.ChatId).SendAsync("ReceiveMessage", user, message, chat.ChatId);
        }
        else
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "System", "You are not connected to an agent. Please connect first.", null);
        }
    }

    public async Task EndChat(string chatId)
    {
        var chat = _chatAPIService.GetActiveChat(chatId);
        if (chat != null && _chatAPIService.EndChat(chatId))
        {
            // Get who ended the chat (user or agent)
            string endedBy = Context.ConnectionId == chat.UserConnectionId ? chat.DisplayName : "Agent";

            // Send end notification to the chat group
            await Clients.Group(chatId).SendAsync("ChatEnded", $"Chat ended by {endedBy}");

            // Remove both user and agent from the chat group
            if (!string.IsNullOrEmpty(chat.UserConnectionId))
                await Groups.RemoveFromGroupAsync(chat.UserConnectionId, chatId);

            if (!string.IsNullOrEmpty(chat.AgentConnectionId))
                await Groups.RemoveFromGroupAsync(chat.AgentConnectionId, chatId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var agentId = _chatAPIService.GetAgentIdByConnectionId(Context.ConnectionId);
        if (agentId != null)
        {
            var agent = _chatAPIService.GetAgentById(agentId);
            if (agent != null)
            {
                // Handle temporary disconnection instead of removing the agent
                _chatAPIService.HandleAgentDisconnection(agent.AgentId);
                await Clients.Others.SendAsync("AgentDisconnected", $"Agent {agent.Name} has disconnected");
                // Broadcast status change when agent disconnects
                await Clients.All.SendAsync("AgentStatusChanged");
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Debug endpoint to get all active chats
    public IEnumerable<AssigningChat> GetAllActiveChats()
    {
        return _chatAPIService.GetAllActiveChats();
    }

    //Agent join chat
    public async Task JoinChat(string chatId)
    {
        var chat = _chatAPIService.GetActiveChat(chatId);
        if (chat != null)
        {
            var agentId = _chatAPIService.GetAgentIdByConnectionId(Context.ConnectionId);
            if (agentId != null && agentId == chat.AgentId)
            {
                // Load and send chat history to the agent
                var chatHistory = await _chatAPIService.GetChatHistory(chatId);
                if (chatHistory.Any())
                {
                    foreach (var message in chatHistory)
                    {
                        await Clients.Caller.SendAsync("ReceiveMessage",
                            message.SenderName,
                            message.Content,
                            chatId);
                    }
                }

                // Send a notification that the agent has opened the chat window
                await Clients.Group(chatId).SendAsync("ReceiveMessage",
                    "System",
                    $"Agent has opened the chat window.",
                    chatId);
            }
            else
            {
                throw new HubException("Not authorized to join this chat.");
            }
        }
        else
        {
            throw new HubException("Chat not found or not active.");
        }
    }

    public async Task LeaveChat(string chatId)
    {
        var chat = _chatAPIService.GetActiveChat(chatId);
        if (chat != null)
        {
            var agentId = _chatAPIService.GetAgentIdByConnectionId(Context.ConnectionId);
            if (agentId != null && agentId == chat.AgentId)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);
                Console.WriteLine($"Agent {agentId} left chat group {chatId}");
            }
        }
    }
}