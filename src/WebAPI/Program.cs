using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using WebAPI.Services;
using WebAPI.Services.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add memory cache
builder.Services.AddMemoryCache();

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("CalendarApi", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 10, // 10 requests per minute per IP
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 3
            }));
});

// Add HTTP client with a reasonable timeout
builder.Services.AddHttpClient<ICalendarService, CalendarService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "BIR-Calendar-API/1.0");
});

// Configure forwarded headers for reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register services
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<IIcsService, IcsService>();
builder.Services.AddScoped<ICacheService, CacheService>();

var app = builder.Build();

app.UseForwardedHeaders();

app.UseSwagger();
app.UseSwaggerUI();

app.UseRateLimiter();

app.UseCors();

// Calendar endpoint that returns ICS feed with events and reminders
app.MapGet("/calendar/{id}", async (string id, ICalendarService calendarService, IIcsService icsService, ICacheService cacheService, HttpContext context) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Results.BadRequest("ID parameter is required");
        }

        // Get cached calendar events
        var events = await cacheService.GetCachedCalendarEventsAsync(id, calendarService.GetCalendarEventsAsync);

        // Generate ICS content with both events and reminders
        var icsContent = icsService.GenerateIcs(events, $"Tømmekalender - {id}");

        // Add cache headers
        context.Response.Headers.CacheControl = "public, max-age=3600"; // Cache for 1 hour
        context.Response.Headers.ContentDisposition = $"attachment; filename=\"calendar-{id}.ics\"";

        // Return as ICS file
        return Results.Text(icsContent, "text/calendar");
    }
    catch (HttpRequestException)
    {
        return Results.Problem("Unable to fetch calendar data from external source", statusCode: 502);
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while processing the calendar request", statusCode: 500);
    }
})
.WithName("GetCalendar")
.WithOpenApi(operation =>
{
    // cspell:words VEVENT VTODO
    operation.Summary = "Get calendar as ICS feed with events and reminders";
    operation.Description = "Returns calendar events and reminder tasks for the specified ID as a unified ICS (iCalendar) feed. Includes both VEVENT entries for the collection dates and VTODO entries for reminders.";
    return operation;
})
.RequireRateLimiting("CalendarApi");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
.WithName("HealthCheck")
.WithOpenApi();

// Cache management endpoint (for development/admin use)
app.MapPost("/admin/clear-cache", (ICacheService cacheService) =>
{
    cacheService.ClearCache();
    return Results.Ok(new { Message = "Cache cleared successfully" });
})
.WithName("ClearCache")
.WithOpenApi();

app.Run();