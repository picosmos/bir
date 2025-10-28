using System.Globalization;
using System.Text;
using WebAPI.Models;
using WebAPI.Services.Contracts;

// cspell:words VCALENDAR PRODID CALDESC CALSCALE VEVENT DTSTAMP DTSTART DTEND yyyyMMddTHHmmssZ CALNAME VALARM VTODO

namespace WebAPI.Services;

public class IcsService : IIcsService
{
    public string GenerateIcs(List<CalendarEvent> events, string location = "")
    {
        ArgumentNullException.ThrowIfNull(events);

        var ics = new StringBuilder();

        // ICS header
        ics.AppendLine("BEGIN:VCALENDAR");
        ics.AppendLine("VERSION:2.0");
        ics.AppendLine("PRODID:-//BIR Calendar//BIR Tømmekalender//EN");
        ics.AppendLine(CultureInfo.InvariantCulture, $"X-WR-CALNAME:{EscapeIcsValue($"Tømmekalender {location}")}");
        ics.AppendLine("X-WR-CALDESC:Tømmekalender fra BIR");
        ics.AppendLine("CALSCALE:GREGORIAN");
        ics.AppendLine("METHOD:PUBLISH");

        var now = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

        foreach (var calendarEvent in events)
        {
            ics.AppendLine("BEGIN:VEVENT");

            // Generate unique ID for the event
            var uidEvent = GenerateUid(calendarEvent, "event");
            ics.AppendLine(CultureInfo.InvariantCulture, $"UID:{uidEvent}");

            // Date formatting for whole-day events (VALUE=DATE format)
            var dtStart = calendarEvent.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var dtEnd = calendarEvent.Date.AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture); // End date is exclusive for all-day events

            ics.AppendLine(CultureInfo.InvariantCulture, $"DTSTART;VALUE=DATE:{dtStart}");
            ics.AppendLine(CultureInfo.InvariantCulture, $"DTEND;VALUE=DATE:{dtEnd}");

            // Event details
            ics.AppendLine(CultureInfo.InvariantCulture, $"SUMMARY:{EscapeIcsValue($"Henting av {calendarEvent.WasteType}")}");

            if (!string.IsNullOrEmpty(location))
            {
                ics.AppendLine(CultureInfo.InvariantCulture, $"LOCATION:{EscapeIcsValue(location)}");
            }

            // Created and last modified timestamps
            ics.AppendLine(CultureInfo.InvariantCulture, $"DTSTAMP:{now}");
            ics.AppendLine(CultureInfo.InvariantCulture, $"CREATED:{now}");
            ics.AppendLine(CultureInfo.InvariantCulture, $"LAST-MODIFIED:{now}");

            // Event status and classification
            ics.AppendLine("STATUS:CONFIRMED");
            ics.AppendLine("CLASS:PUBLIC");
            ics.AppendLine("TRANSP:TRANSPARENT"); // Mark as "not busy" - informational only

            // Add reminder for 4pm the day before
            ics.AppendLine("BEGIN:VALARM");
            ics.AppendLine("TRIGGER;VALUE=DATE-TIME:" + calendarEvent.DateAsDateTime.AddDays(-1).AddHours(16).ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture));
            ics.AppendLine("ACTION:DISPLAY");
            ics.AppendLine("DESCRIPTION:Påminnelse: " + EscapeIcsValue($"Henting av {calendarEvent.WasteType}") + " i morgen");
            ics.AppendLine("END:VALARM");

            ics.AppendLine("END:VEVENT");

            ics.AppendLine("BEGIN:VTODO");
            // Generate unique ID for the reminder
            var uidReminder = GenerateUid(calendarEvent, "reminder");
            ics.AppendLine(CultureInfo.InvariantCulture, $"UID:{uidReminder}");

            // Due date for the reminder (day before the actual event at 4pm)
            var dueDate = calendarEvent.DateAsDateTime.AddDays(-1).Date.AddHours(16).ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            ics.AppendLine(CultureInfo.InvariantCulture, $"DUE:{dueDate}");

            // Reminder details
            ics.AppendLine(CultureInfo.InvariantCulture, $"SUMMARY:{EscapeIcsValue($"Henting av {calendarEvent.WasteType} i morgen")}");

            // Priority (1=high, 5=medium, 9=low)
            ics.AppendLine("PRIORITY:5");

            // Status and classification
            ics.AppendLine("STATUS:NEEDS-ACTION");
            ics.AppendLine("CLASS:PUBLIC");

            // Created and last modified timestamps
            ics.AppendLine(CultureInfo.InvariantCulture, $"DTSTAMP:{now}");
            ics.AppendLine(CultureInfo.InvariantCulture, $"CREATED:{now}");
            ics.AppendLine(CultureInfo.InvariantCulture, $"LAST-MODIFIED:{now}");

            ics.AppendLine("END:VTODO");
        }

        // ICS footer
        ics.AppendLine("END:VCALENDAR");

        return ics.ToString();
    }

    private static string GenerateUid(CalendarEvent calendarEvent, string icsType)
    {
        // Generate a unique ID based on the event details, type, and application version
        const string version = "2";
        var sanitizedWasteType = EscapeIcsValue(calendarEvent.WasteType).Replace(" ", "-");
        return $"{calendarEvent.Date:yyyyMMdd}-{sanitizedWasteType}@bir.{icsType}.v{version}";
    }

    private static string EscapeIcsValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Escape special characters according to RFC 5545
        return value
            .Replace("\\", "\\\\")  // Escape backslashes first
            .Replace(",", "\\,")    // Escape commas
            .Replace(";", "\\;")    // Escape semicolons
            .Replace("\n", "\\n")   // Escape newlines
            .Replace("\r", "")      // Remove carriage returns
            .Trim();
    }
}