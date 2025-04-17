using SignalRChat.Hubs;
using SignalRChat.Services;
using SignalRChat.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSignalR();

// Add DbContext
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<ChatAPIService>();
builder.Services.AddSingleton<AgentChatCoordinatorService>();
builder.Services.AddScoped<DataSeedService>();

var app = builder.Build();

// Create database and seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ChatDbContext>();
    context.Database.EnsureCreated();
    
    // Seed initial data
    var seedService = services.GetRequiredService<DataSeedService>();
    seedService.SeedData();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapHub<ChatHub>("/chatHub");

app.Run();