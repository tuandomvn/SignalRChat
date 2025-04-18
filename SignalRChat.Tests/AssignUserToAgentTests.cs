using Moq;
using SignalRChat.Models;
using SignalRChat.Services;
using Xunit;

namespace SignalRChat.Tests;

public class AgentChatCoordinatorServiceTests
{
    private readonly Mock<IDataRepository> _mockChatAPIService;
    private readonly AgentChatCoordinatorService _service;

    public AgentChatCoordinatorServiceTests()
    {
        _mockChatAPIService = new Mock<IDataRepository>();
        _service = new AgentChatCoordinatorService(_mockChatAPIService.Object);
    }

    [Fact]
    public void AssignUserToAgent_WhenNoAgentsAvailable_ReturnsNull()
    {
        // Arrange
        var team = new Team
        {
            TeamId = "team1",
            Name = "Team 1",
            IsActive = true,
            Agents = new List<Agent>()
        };

        // Act
        var result = _service.AssignUserToAgent("connection1", "User1", team);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void AssignUserToAgent_WhenJuniorAgentAvailable_AssignsToJuniorAgent()
    {
        // Arrange
        var team = new Team
        {
            TeamId = "team1",
            Name = "Team 1",
            IsActive = true,
            Agents = new List<Agent>
            {
                new Agent
                {
                    AgentId = "junior1",
                    Name = "Junior Agent",
                    IsAvailable = true,
                    Seniority = AgentSeniority.Junior
                }
            }
        };

        _mockChatAPIService.Setup(x => x.GetAgentActiveChats("junior1"))
            .Returns(new List<AssigningChat>());

        _mockChatAPIService.Setup(x => x.AddActiveChat(It.IsAny<AssigningChat>()))
            .Returns(true);

        // Act
        var result = _service.AssignUserToAgent("connection1", "User1", team);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("junior1", result.AgentId);
        Assert.Equal("team1", result.TeamId);
        Assert.Equal("connection1", result.UserConnectionId);
        Assert.Equal("User1", result.DisplayName);
        Assert.True(result.IsActive);
    }

    [Fact]
    public void AssignUserToAgent_WhenJuniorAtCapacity_AssignsToMidLevel()
    {
        // Arrange
        var team = new Team
        {
            TeamId = "team1",
            Name = "Team 1",
            IsActive = true,
            Agents = new List<Agent>
            {
                new Agent
                {
                    AgentId = "junior1",
                    Name = "Junior Agent",
                    IsAvailable = true,
                    Seniority = AgentSeniority.Junior
                },
                new Agent
                {
                    AgentId = "mid1",
                    Name = "Mid Agent",
                    IsAvailable = true,
                    Seniority = AgentSeniority.MidLevel
                }
            }
        };

        // Junior agent at capacity (4 chats)
        _mockChatAPIService.Setup(x => x.GetAgentActiveChats("junior1"))
            .Returns(new List<AssigningChat>
            {
                new AssigningChat(), 
                new AssigningChat(),
                new AssigningChat(), 
                new AssigningChat()
            });

        _mockChatAPIService.Setup(x => x.GetAgentActiveChats("mid1"))
            .Returns(new List<AssigningChat>());

        _mockChatAPIService.Setup(x => x.AddActiveChat(It.IsAny<AssigningChat>()))
            .Returns(true);

        // Act
        var result = _service.AssignUserToAgent("connection1", "User1", team);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("mid1", result.AgentId);
        Assert.Equal("team1", result.TeamId);
    }

    [Fact]
    public void AssignUserToAgent_WhenAllAgentsAtCapacity_ReturnsNull()
    {
        // Arrange
        var team = new Team
        {
            TeamId = "team1",
            Name = "Team 1",
            IsActive = true,
            Agents = new List<Agent>
            {
                new Agent
                {
                    AgentId = "junior1",
                    Name = "Junior Agent",
                    IsAvailable = true,
                    Seniority = AgentSeniority.Junior
                },
                new Agent
                {
                    AgentId = "mid1",
                    Name = "Mid Agent",
                    IsAvailable = true,
                    Seniority = AgentSeniority.MidLevel
                }
            }
        };

        // Both agents at capacity
        _mockChatAPIService.Setup(x => x.GetAgentActiveChats("junior1"))
            .Returns(new List<AssigningChat>
            {
                new AssigningChat(), 
                new AssigningChat(),
                new AssigningChat(), 
                new AssigningChat()
            });

        _mockChatAPIService.Setup(x => x.GetAgentActiveChats("mid1"))
            .Returns(new List<AssigningChat>
            {
                new AssigningChat(), new AssigningChat(),
                new AssigningChat(), new AssigningChat(),
                new AssigningChat(), new AssigningChat()
            });

        // Act
        var result = _service.AssignUserToAgent("connection1", "User1", team);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void AssignUserToAgent_WhenAgentsUnavailable_ReturnsNull()
    {
        // Arrange
        var team = new Team
        {
            TeamId = "team1",
            Name = "Team 1",
            IsActive = true,
            Agents = new List<Agent>
            {
                new Agent
                {
                    AgentId = "junior1",
                    Name = "Junior Agent",
                    IsAvailable = false,
                    Seniority = AgentSeniority.Junior
                },
                new Agent
                {
                    AgentId = "mid1",
                    Name = "Mid Agent",
                    IsAvailable = false,
                    Seniority = AgentSeniority.MidLevel
                }
            }
        };

        // Act
        var result = _service.AssignUserToAgent("connection1", "User1", team);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void AssignUserToAgent_WhenAddActiveChatFails_ReturnsNull()
    {
        // Arrange
        var team = new Team
        {
            TeamId = "team1",
            Name = "Team 1",
            IsActive = true,
            Agents = new List<Agent>
            {
                new Agent
                {
                    AgentId = "junior1",
                    Name = "Junior Agent",
                    IsAvailable = true,
                    Seniority = AgentSeniority.Junior
                }
            }
        };

        _mockChatAPIService.Setup(x => x.GetAgentActiveChats("junior1"))
            .Returns(new List<AssigningChat>());

        _mockChatAPIService.Setup(x => x.AddActiveChat(It.IsAny<AssigningChat>()))
            .Returns(false);

        // Act
        var result = _service.AssignUserToAgent("connection1", "User1", team);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void AssignUserToAgent_WhenTeamInactive_ReturnsNull()
    {
        // Arrange
        var team = new Team
        {
            TeamId = "team1",
            Name = "Team 1",
            IsActive = false,
            Agents = new List<Agent>
            {
                new Agent
                {
                    AgentId = "junior1",
                    Name = "Junior Agent",
                    IsAvailable = true,
                    Seniority = AgentSeniority.Junior
                }
            }
        };

        // Act
        var result = _service.AssignUserToAgent("connection1", "User1", team);

        // Assert
        Assert.Null(result);
    }
}