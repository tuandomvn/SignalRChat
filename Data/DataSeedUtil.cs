using SignalRChat.Models;
using Microsoft.EntityFrameworkCore;

namespace SignalRChat.Data;

public class DataSeedUtil
{
    private readonly ChatDbContext _context;

    public DataSeedUtil(ChatDbContext context)
    {
        _context = context;
    }

    public void CleanupActiveChats()
    {
        // Get all chats
        var chats = _context.AssigningChats.Where(c => c.IsActive).ToList();
        if (chats.Any())
        {
            // Remove all chats
            _context.AssigningChats.RemoveRange(chats);
            _context.SaveChanges();
        }

        var agentConnections = _context.AgentConnections.ToList();
        if (agentConnections.Any())
        {
            _context.AgentConnections.RemoveRange(agentConnections);
            _context.SaveChanges();
        }

        var agents = _context.Agents.ToList();
        if (agents.Any())
        {
            foreach (var item in agents)
            {
                item.IsAvailable = false;
            }
            _context.Agents.UpdateRange(agents);
            _context.SaveChanges();
        }

        var teams = _context.Teams.ToList();
        if (teams.Any())
        {
            foreach (var item in teams)
            {
                item.IsActive = false;
            }
            _context.Teams.UpdateRange(teams);
            _context.SaveChanges();
        }
    }

    public void SeedData()
    {
        // First, cleanup any active chats
        CleanupActiveChats();

        if (_context.Teams.Any()) return; // Data already seeded

        // Team A: Morning Shift (6:00 - 14:00)
        var teamA = new Team
        {
            TeamId = "team-a",
            Name = "Team A",
            Description = "Main Support Team - Morning Shift",
            Shift = ShiftType.Morning,
            ShiftStartTime = new TimeSpan(6, 0, 0),
            ShiftEndTime = new TimeSpan(14, 0, 0)
        };
        _context.Teams.Add(teamA);

        var agentsA = new[]
        {
            new Agent
            {
                AgentId = "a-lead",
                Name = "Alice Johnson",
                Seniority = AgentSeniority.TeamLead,
                TeamId = teamA.TeamId
            },
            new Agent
            {
                AgentId = "a-mid1",
                Name = "Bob Smith",
                Seniority = AgentSeniority.MidLevel,
                TeamId = teamA.TeamId
            },
            new Agent
            {
                AgentId = "a-mid2",
                Name = "Carol White",
                Seniority = AgentSeniority.MidLevel,
                TeamId = teamA.TeamId
            },
            new Agent
            {
                AgentId = "a-junior",
                Name = "David Brown",
                Seniority = AgentSeniority.Junior,
                TeamId = teamA.TeamId
            }
        };
        _context.Agents.AddRange(agentsA);

        // Team B: Afternoon Shift (14:00 - 22:00)
        var teamB = new Team
        {
            TeamId = "team-b",
            Name = "Team B",
            Description = "Secondary Support Team - Afternoon Shift",
            Shift = ShiftType.Afternoon,
            ShiftStartTime = new TimeSpan(14, 0, 0),
            ShiftEndTime = new TimeSpan(22, 0, 0)
        };
        _context.Teams.Add(teamB);

        var agentsB = new[]
        {
            new Agent
            {
                AgentId = "b-senior",
                Name = "Emma Davis",
                Seniority = AgentSeniority.Senior,
                TeamId = teamB.TeamId
            },
            new Agent
            {
                AgentId = "b-mid",
                Name = "Frank Miller",
                Seniority = AgentSeniority.MidLevel,
                TeamId = teamB.TeamId
            },
            new Agent
            {
                AgentId = "b-junior1",
                Name = "Grace Wilson",
                Seniority = AgentSeniority.Junior,
                TeamId = teamB.TeamId
            },
            new Agent
            {
                AgentId = "b-junior2",
                Name = "Henry Taylor",
                Seniority = AgentSeniority.Junior,
                TeamId = teamB.TeamId
            }
        };
        _context.Agents.AddRange(agentsB);

        // Team C: Night Shift (22:00 - 6:00)
        var teamC = new Team
        {
            TeamId = "team-c",
            Name = "Team C",
            Description = "Night Shift Support Team",
            Shift = ShiftType.Night,
            ShiftStartTime = new TimeSpan(22, 0, 0),
            ShiftEndTime = new TimeSpan(6, 0, 0)
        };
        _context.Teams.Add(teamC);

        var agentsC = new[]
        {
            new Agent
            {
                AgentId = "c-mid1",
                Name = "Isabel Martinez",
                Seniority = AgentSeniority.MidLevel,
                TeamId = teamC.TeamId
            },
            new Agent
            {
                AgentId = "c-mid2",
                Name = "Jack Thompson",
                Seniority = AgentSeniority.MidLevel,
                TeamId = teamC.TeamId
            }
        };
        _context.Agents.AddRange(agentsC);

        // Overflow team: Flexible Hours
        var overflowTeam = new Team
        {
            TeamId = "overflow",
            Name = "Overflow Team",
            Description = "Overflow Support Team - Flexible Hours",
            Shift = ShiftType.Overflow
        };
        _context.Teams.Add(overflowTeam);

        var overflowAgents = new[]
        {
            ("of-1", "Kelly Anderson"),
            ("of-2", "Liam Parker"),
            ("of-3", "Mia Garcia"),
            ("of-4", "Noah Lee"),
            ("of-5", "Olivia King"),
            ("of-6", "Peter Wright")
        };

        foreach (var (id, name) in overflowAgents)
        {
            _context.Agents.Add(new Agent
            {
                AgentId = id,
                Name = name,
                Seniority = AgentSeniority.Junior,
                TeamId = overflowTeam.TeamId
            });
        }

        _context.SaveChanges();
    }
} 