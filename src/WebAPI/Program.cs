using WebAPI.Services;
using WebAPI.Services.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add memory cache
builder.Services.AddMemoryCache();

// Add HTTP client with a reasonable timeout
builder.Services.AddHttpClient<ICalendarService, CalendarService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "BIR-Calendar-API/1.0");
});

// Register services
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<IIcsService, IcsService>();
builder.Services.AddScoped<ICacheService, CacheService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Calendar endpoint that returns ICS feed
app.MapGet("/calendar/{id}", async (string id, ICalendarService calendarService, IIcsService icsService, ICacheService cacheService) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Results.BadRequest("ID parameter is required");
        }

        // Get cached calendar events
        var events = await cacheService.GetCachedCalendarEventsAsync(id, calendarService.GetCalendarEventsAsync);

        if (events.Count == 0)
        {
            return Results.NotFound("No calendar events found for the specified ID");
        }

        // Generate ICS content
        var icsContent = icsService.GenerateIcs(events, $"Tømmekalender - {id}");

        // Return as ICS file
        return Results.Text(icsContent, "text/calendar", null, null);
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
    operation.Summary = "Get calendar as ICS feed";
    operation.Description = "Returns calendar events for the specified ID as an ICS (iCalendar) feed";
    return operation;
});

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