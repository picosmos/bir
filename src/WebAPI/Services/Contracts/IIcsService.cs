using WebAPI.Models;

namespace WebAPI.Services.Contracts;

public interface IIcsService
{
    string GenerateIcs(List<CalendarEvent> events, string location = "");
}