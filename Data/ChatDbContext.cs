using Microsoft.EntityFrameworkCore;
using SignalRChat.Models;

namespace SignalRChat.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
    }

    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<AssigningChat> Chats => Set<AssigningChat>();
    public DbSet<AgentConnection> AgentConnections => Set<AgentConnection>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure boolean fields for SQLite
        modelBuilder.Entity<AssigningChat>(entity =>
        {
            entity.Property(e => e.IsActive)
                .IsRequired()  // NOT NULL
                .HasDefaultValue(1)  // Default value in SQLite (1 for true)
                .ValueGeneratedNever();  // Value is provided by the application
        });

        // Configure relationships
        modelBuilder.Entity<Agent>()
            .HasOne(a => a.Team)
            .WithMany(t => t.Agents)
            .HasForeignKey(a => a.TeamId);

        modelBuilder.Entity<AssigningChat>()
            .HasOne(c => c.Agent)
            .WithMany(a => a.ActiveChats)
            .HasForeignKey(c => c.AgentId);

        modelBuilder.Entity<AssigningChat>()
            .HasOne(c => c.Team)
            .WithMany(t => t.ActiveChats)
            .HasForeignKey(c => c.TeamId);

        modelBuilder.Entity<AgentConnection>()
            .HasOne(c => c.Agent)
            .WithMany()
            .HasForeignKey(c => c.AgentId);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        
        optionsBuilder
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging();
    }
} 