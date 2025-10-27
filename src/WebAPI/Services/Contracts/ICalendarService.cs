using WebAPI.Models;

namespace WebAPI.Services.Contracts;

public interface ICalendarService
{
    Task<List<CalendarEvent>> GetCalendarEventsAsync(string id);
}
