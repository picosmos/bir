namespace WebAPI.Models;

public record CalendarEvent(
    DateOnly Date,
    string WasteType
)
{
    public DateTime DateAsDateTime => this.Date.ToDateTime(new TimeOnly(0, 0));
};