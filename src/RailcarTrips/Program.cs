using Microsoft.EntityFrameworkCore;

using RailcarTrips.Components;
using RailcarTrips.Database;
using RailcarTrips.Trips;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<RailcarTripsContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<TripsService>();
builder.Services.AddScoped<TripProcessor>();
builder.Services.AddScoped<TripsImportService>();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
var dbContext = scope.ServiceProvider.GetRequiredService<RailcarTripsContext>();

logger.LogInformation("Starting database migration");
await dbContext.Database.MigrateAsync().ConfigureAwait(false);
logger.LogInformation("Database migration completed successfully");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
