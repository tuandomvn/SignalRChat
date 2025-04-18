using Moq;
using SignalRChat.Models;
using SignalRChat.Services;
using Xunit;

namespace SignalRChat.Tests;

public class GetAvailableTeamTests
{
    private readonly Mock<IDataRepository> _mockChatAPIService;
    private readonly AgentChatCoordinatorService _service;

    public GetAvailableTeamTests()
    {
        _mockChatAPIService = new Mock<IDataRepository>();
        _service = new AgentChatCoordinatorService(_mockChatAPIService.Object);
    }

    [Fact]
    public void GetAvailableTeam_WhenNoTeams_ReturnsNull()
    {
        // Arrange
        _mockChatAPIService.Setup(x => x.GetAllTeams())
            .Returns(new List<Team>());

        // Act
        var result = _service.GetAvailableTeam(TimeSpan.FromHours(12));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetAvailableTeam_WhenPrimaryTeamAvailable_ReturnsPrimaryTeam()
    {
        // Arrange
        var primaryTeam = new Team
        {
            TeamId = "team1",
            Name = "Primary Team",
            IsActive = true,
            Shift = ShiftType.Morning,
            ShiftStartTime = TimeSpan.FromHours(9),
            ShiftEndTime = TimeSpan.FromHours(17),
            Agents = new List<Agent>
            {
                new Agent { AgentId = "agent1", IsAvailable = true, Seniority = AgentSeniority.Junior },
                new Agent { AgentId = "agent2", IsAvailable = true, Seniority = AgentSeniority.MidLevel }
            }
        };

        var overflowTeam = new Team
        {
            TeamId = "overflow",
            Name = "Overflow Team",
            IsActive = true,
            Shift = ShiftType.Overflow,
            Agents = new List<Agent>()
        };

        _mockChatAPIService.Setup(x => x.GetAllTeams())
            .Returns(new List<Team> { primaryTeam, overflowTeam });

        _mockChatAPIService.Setup(x => x.GetTeamActiveChats(primaryTeam.TeamId))
            .Returns(new List<AssigningChat>());

        // Act
        var result = _service.GetAvailableTeam(TimeSpan.FromHours(12));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(primaryTeam.TeamId, result.TeamId);
    }

    [Fact]
    public void GetAvailableTeam_WhenPrimaryTeamAtCapacity_ReturnsOverflowTeam()
    {
        // Arrange
        var primaryTeam = new Team
        {
            TeamId = "team1",
            Name = "Primary Team",
            IsActive = true,
            Shift = ShiftType.Morning,
            ShiftStartTime = TimeSpan.FromHours(9),
            ShiftEndTime = TimeSpan.FromHours(17),
            Agents = new List<Agent>
            {
                new Agent { AgentId = "agent1", IsAvailable = true, Seniority = AgentSeniority.Junior }
            }
        };

        var overflowTeam = new Team
        {
            TeamId = "overflow",
            Name = "Overflow Team",
            IsActive = true,
            Shift = ShiftType.Overflow,
            Agents = new List<Agent>
            {
                new Agent { AgentId = "agent2", IsAvailable = true, Seniority = AgentSeniority.Junior }
            }
        };

        _mockChatAPIService.Setup(x => x.GetAllTeams())
            .Returns(new List<Team> { primaryTeam, overflowTeam });

        // Set active chats to exceed capacity (4 chats for Junior * 1.5 = 6)
        _mockChatAPIService.Setup(x => x.GetTeamActiveChats(primaryTeam.TeamId))
            .Returns(new List<AssigningChat> 
            { 
                new AssigningChat(), new AssigningChat(), new AssigningChat(),
                new AssigningChat(), new AssigningChat(), new AssigningChat(),
                new AssigningChat() // 7 chats > 6 capacity
            });

        // Act
        var result = _service.GetAvailableTeam(TimeSpan.FromHours(12));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(overflowTeam.TeamId, result.TeamId);
    }

    [Fact]
    public void GetAvailableTeam_WhenPrimaryTeamInactive_ReturnsOverflowTeam()
    {
        // Arrange
        var primaryTeam = new Team
        {
            TeamId = "team1",
            Name = "Primary Team",
            IsActive = false,
            Shift = ShiftType.Morning,
            ShiftStartTime = TimeSpan.FromHours(9),
            ShiftEndTime = TimeSpan.FromHours(17),
            Agents = new List<Agent>()
        };

        var overflowTeam = new Team
        {
            TeamId = "overflow",
            Name = "Overflow Team",
            IsActive = true,
            Shift = ShiftType.Overflow,
            Agents = new List<Agent>
            {
                new Agent { AgentId = "agent1", IsAvailable = true, Seniority = AgentSeniority.Junior }
            }
        };

        _mockChatAPIService.Setup(x => x.GetAllTeams())
            .Returns(new List<Team> { primaryTeam, overflowTeam });

        // Act
        var result = _service.GetAvailableTeam(TimeSpan.FromHours(12));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(overflowTeam.TeamId, result.TeamId);
    }

    [Fact]
    public void GetAvailableTeam_WhenOutsidePrimaryTeamShift_ReturnsOverflowTeam()
    {
        // Arrange
        var primaryTeam = new Team
        {
            TeamId = "team1",
            Name = "Primary Team",
            IsActive = true,
            Shift = ShiftType.Morning,
            ShiftStartTime = TimeSpan.FromHours(9),
            ShiftEndTime = TimeSpan.FromHours(17),
            Agents = new List<Agent>()
        };

        var overflowTeam = new Team
        {
            TeamId = "overflow",
            Name = "Overflow Team",
            IsActive = true,
            Shift = ShiftType.Overflow,
            Agents = new List<Agent>
            {
                new Agent { AgentId = "agent1", IsAvailable = true, Seniority = AgentSeniority.Junior }
            }
        };

        _mockChatAPIService.Setup(x => x.GetAllTeams())
            .Returns(new List<Team> { primaryTeam, overflowTeam });

        // Act - Try to get team at 8 AM (outside primary team's shift)
        var result = _service.GetAvailableTeam(TimeSpan.FromHours(8));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(overflowTeam.TeamId, result.TeamId);
    }
} 