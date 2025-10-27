using Microsoft.Extensions.Caching.Memory;
using WebAPI.Models;
using WebAPI.Services.Contracts;

namespace WebAPI.Services;

public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CacheService> _logger;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromDays(1);

    public CacheService(IMemoryCache memoryCache, ILogger<CacheService> logger)
    {
        this._memoryCache = memoryCache;
        this._logger = logger;
    }

    public async Task<List<CalendarEvent>> GetCachedCalendarEventsAsync(string id, Func<string, Task<List<CalendarEvent>>> fetchFunction)
    {
        ArgumentNullException.ThrowIfNull(fetchFunction);

        var cacheKey = $"calendar_events_{id}";

        if (this._memoryCache.TryGetValue(cacheKey, out List<CalendarEvent>? cachedEvents))
        {
            this._logger.LogInformation("Returning cached calendar events for ID: {Id}", id);
            return cachedEvents!;
        }

        this._logger.LogInformation("Cache miss for ID: {Id}, fetching fresh data", id);

        try
        {
            var events = await fetchFunction(id);

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = this._cacheDuration,
                SlidingExpiration = TimeSpan.FromHours(12),
                Priority = CacheItemPriority.Normal
            };

            this._memoryCache.Set(cacheKey, events, cacheOptions);
            this._logger.LogInformation("Cached {Count} calendar events for ID: {Id} with expiration: {Expiration}",
                events.Count, id, DateTime.Now.Add(this._cacheDuration));

            return events;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error fetching calendar events for ID: {Id}", id);

            // Try to return stale cache if available
            if (this._memoryCache.TryGetValue($"{cacheKey}_stale", out List<CalendarEvent>? staleEvents))
            {
                this._logger.LogWarning("Returning stale cache data for ID: {Id}", id);
                return staleEvents!;
            }

            throw;
        }
    }

    public void ClearCache()
    {
        if (this._memoryCache is MemoryCache memoryCache)
        {
            memoryCache.Clear();
            this._logger.LogInformation("Cache cleared");
        }
    }
}