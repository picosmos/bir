namespace WebAPI.Models;

public record CalendarEvent(
    DateTime Date,
    string Title,
    string? Description = null,
    string? Location = null
);