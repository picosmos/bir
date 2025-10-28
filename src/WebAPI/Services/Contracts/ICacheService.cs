using WebAPI.Models;

namespace WebAPI.Services.Contracts;

public interface ICacheService
{
    Task<List<CalendarEvent>> GetCachedCalendarEventsAsync(string id, Func<string, Task<List<CalendarEvent>>> fetchFunction);
    void ClearCache();
}