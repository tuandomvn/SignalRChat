using Microsoft.AspNetCore.SignalR;
using SignalRChat.Models;
using SignalRChat.Services;
using System.Security.Claims;

namespace SignalRChat.Hubs;

public class ChatHub : Hub
{
    private readonly IChatAPIService _chatAssignment;
    private readonly IAgentChatCoordinatorService _coordinator;

    public ChatHub(IChatAPIService chatAssignment, IAgentChatCoordinatorService coordinator)
    {
        _chatAssignment = chatAssignment;
        _coordinator = coordinator;
    }

    public async Task RegisterConnection(string agentId)
    {
        // Check if agent exists
        var agent = _chatAssignment.GetAgentById(agentId);
        if (agent == null)
        {
            throw new HubException("Agent not found.");
        }

        // Remove old connection if exists
        var existingConnectionId = _chatAssignment.GetAgentConnectionId(agentId);
        if (!string.IsNullOrEmpty(existingConnectionId))
        {
            await Groups.RemoveFromGroupAsync(existingConnectionId, "Agents");
        }

        // Update connection and add to group
        _chatAssignment.UpdateAgentConnection(agentId, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "Agents");
    }

    public async Task StartChat(string displayName)
    {
        // Debug database content before starting
        _chatAssignment.DebugDatabaseContent();
        
        // Check if user already has an active chat
        var existingChat = _chatAssignment.GetActiveChatByConnectionId(Context.ConnectionId);
        if (existingChat != null)
        {
            var existingAgent = _chatAssignment.GetAgentById(existingChat.AgentId);
            if (existingAgent != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, existingChat.ChatId);
                var agentConnectionId = _chatAssignment.GetAgentConnectionId(existingAgent.AgentId);
                if (!string.IsNullOrEmpty(agentConnectionId))
                {
                    await Groups.AddToGroupAsync(agentConnectionId, existingChat.ChatId);
                    await Clients.Caller.SendAsync("ChatAssigned", "You are connected to " + existingAgent.Name);
                }
                return;
            }
        }

        // Try to assign a new chat using the coordinator
        var chat = _coordinator.AssignUserToAgent(Context.ConnectionId, displayName, TimeSpan.FromHours(DateTime.Now.Hour));
        if (chat != null)
        {
            var agent = _chatAssignment.GetAgentById(chat.AgentId);
            if (agent != null)
            {
                // Add both user and agent to the chat group
                await Groups.AddToGroupAsync(Context.ConnectionId, chat.ChatId);
                
                // Get agent's current connection and add to group
                var agentConnectionId = _chatAssignment.GetAgentConnectionId(agent.AgentId);
                if (!string.IsNullOrEmpty(agentConnectionId))
                {
                    await Groups.AddToGroupAsync(agentConnectionId, chat.ChatId);
                    
                    // Update the chat's AgentConnectionId
                    _chatAssignment.UpdateChatAgentConnection(chat.ChatId, agentConnectionId);
                    
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
            
            // Debug database content after chat creation
            Console.WriteLine("\n=== Database content after chat creation ===");
            _chatAssignment.DebugDatabaseContent();
        }
        else
        {
            await Clients.Caller.SendAsync("ChatAssigned", "No agents available at the moment. Please try again later.");
        }
    }

    public async Task SendMessage(string user, string message)
    {
        var chat = _chatAssignment.GetActiveChatByConnectionId(Context.ConnectionId);
        
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
            
            await _chatAssignment.SaveChatMessage(chatMessage);

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
        var chat = _chatAssignment.GetActiveChat(chatId);
        if (chat != null && _chatAssignment.EndChat(chatId))
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

    public async Task SetAgentAvailability(string agentId, bool isAvailable)
    {
        if (_chatAssignment.UpdateAgentAvailability(agentId, isAvailable))
        {
            await Clients.Caller.SendAsync("AvailabilityUpdated", 
                isAvailable ? "You are now available for chats" : "You are now unavailable for chats");
            // Broadcast status change to all clients
            await Clients.All.SendAsync("AgentStatusChanged");
        }
    }

    public async Task<object> GetChatStatus(string chatId)
    {
        var chat = _chatAssignment.GetActiveChat(chatId);
        return new { 
            isActive = chat?.IsActive ?? false,
            userId = chat?.DisplayName ?? "",
            chatId = chat?.ChatId ?? ""
        };
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var agentId = _chatAssignment.GetAgentIdByConnectionId(Context.ConnectionId);
        if (agentId != null)
        {
            var agent = _chatAssignment.GetAgentById(agentId);
            if (agent != null)
            {
                // Handle temporary disconnection instead of removing the agent
                _chatAssignment.HandleAgentDisconnection(agent.AgentId);
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
        return _chatAssignment.GetAllActiveChats();
    }

    //Agent join chat window
    public async Task JoinChat(string chatId)
    {
        var chat = _chatAssignment.GetActiveChat(chatId);
        if (chat != null)
        {
            var agentId = _chatAssignment.GetAgentIdByConnectionId(Context.ConnectionId);
            if (agentId != null && agentId == chat.AgentId)
            {
                // Load and send chat history to the agent
                // Note: You'll need to implement the GetChatHistory method in your ChatAPIService
                var chatHistory = await _chatAssignment.GetChatHistory(chatId);
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
        var chat = _chatAssignment.GetActiveChat(chatId);
        if (chat != null)
        {
            var agentId = _chatAssignment.GetAgentIdByConnectionId(Context.ConnectionId);
            if (agentId != null && agentId == chat.AgentId)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);
                Console.WriteLine($"Agent {agentId} left chat group {chatId}");
            }
        }
    }
}