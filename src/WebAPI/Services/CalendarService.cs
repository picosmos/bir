using System.Globalization;
using HtmlAgilityPack;
using WebAPI.Models;
using WebAPI.Services.Contracts;

namespace WebAPI.Services;

public partial class CalendarService : ICalendarService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(HttpClient httpClient, ILogger<CalendarService> logger)
    {
        this._httpClient = httpClient;
        this._logger = logger;
    }

    public async Task<List<CalendarEvent>> GetCalendarEventsAsync(string id)
    {
        try
        {
            var url = $"https://bir.no/adressesoek/toemmekalender/?rId={id}";
            this._logger.LogInformation("Fetching calendar data from: {Url}", url);

            var response = await this._httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            return this.ParseCalendarFromHtml(html);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error fetching calendar data for ID: {Id}", id);
            throw;
        }
    }

    private List<CalendarEvent> ParseCalendarFromHtml(string html)
    {
        var events = new List<CalendarEvent>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Find all month containers
        var monthContainers = doc.DocumentNode.SelectNodes("//div[@class='month-container']");
        if (monthContainers == null)
        {
            this._logger.LogWarning("No month containers found in HTML");
            return events;
        }

        foreach (var monthContainer in monthContainers)
        {
            // Get the month and year from the title
            var monthTitleNode = monthContainer.SelectSingleNode(".//h2[@class='month-title']");
            if (monthTitleNode == null)
            {
                continue;
            }

            var monthTitle = monthTitleNode.InnerText?.Trim();
            if (string.IsNullOrEmpty(monthTitle))
            {
                continue;
            }

            var (monthNumber, year) = ParseMonthAndYear(monthTitle);
            if (monthNumber == 0 || year == 0)
            {
                continue;
            }

            // Find all category rows within this month
            var categoryRows = monthContainer.SelectNodes(".//div[@class='category-row']");
            if (categoryRows == null)
            {
                continue;
            }

            foreach (var categoryRow in categoryRows)
            {
                // Get the waste type from the image
                var iconImg = categoryRow.SelectSingleNode(".//img");
                if (iconImg == null)
                {
                    continue;
                }

                var wasteType = GetWasteTypeFromIcon(iconImg.GetAttributeValue("src", ""));

                // Get all date items for this waste type
                var dateItems = categoryRow.SelectNodes(".//div[@class='date-item']");
                if (dateItems == null)
                {
                    continue;
                }

                foreach (var dateItem in dateItems)
                {
                    var dayNode = dateItem.SelectSingleNode(".//div[@class='date-item-day']");
                    var dateNode = dateItem.SelectSingleNode(".//div[@class='date-item-date']");

                    if (dayNode?.InnerText?.Trim() is string dayText &&
                        dateNode?.InnerText?.Trim() is string dateText &&
                        !string.IsNullOrEmpty(dayText) &&
                        !string.IsNullOrEmpty(dateText) &&
                        this.TryParseDate(dateText, monthNumber, year, out var parsedDate))
                    {
                        events.Add(new CalendarEvent(
                            Date: parsedDate, // Waste collection is typically a single day event
                            Title: $"{wasteType} - {dayText}",
                            Description: $"Henting av {wasteType.ToLower(CultureInfo.InvariantCulture)}"
                        ));
                    }
                }
            }
        }

        return [.. events.OrderBy(e => e.Date)];
    }

    private static (int Month, int Year) ParseMonthAndYear(string monthTitle)
    {
        // Expected format: "Oktober 2025", "November 2025", etc.
        var parts = monthTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return (0, 0);
        }

        if (!int.TryParse(parts[1], out var year))
        {
            return (0, 0);
        }

        var monthName = parts[0].ToLowerInvariant();
        var monthNumber = monthName switch
        {
            "januar" => 1,
            "februar" => 2,
            "mars" => 3,
            "april" => 4,
            "mai" => 5,
            "juni" => 6,
            "juli" => 7,
            "august" => 8,
            "september" => 9,
            "oktober" => 10,
            "november" => 11,
            "desember" => 12,
            _ => 0
        };

        return (monthNumber, year);
    }

    private static string GetWasteTypeFromIcon(string iconSrc)
    {
        return iconSrc switch
        {
            string s when s.Contains("glassOgMetall.svg") => "glass og metall",
            string s when s.Contains("matavfall.svg") => "matavfall",
            string s when s.Contains("papirOgPlast.svg") => "papir og plastemballasje",
            string s when s.Contains("restavfall.svg") => "restavfall",
            string s when s.Contains("plastemballasje.svg") => "plastemballasje",
            _ => "ukjent avfallstype"
        };
    }

    private bool TryParseDate(string dateText, int month, int year, out DateTime date)
    {
        date = default;

        // Expected formats: "23. okt", "7. okt", "1. des", etc.
        var parts = dateText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        // Extract day number
        var dayPart = parts[0].TrimEnd('.');
        if (!int.TryParse(dayPart, out var day))
        {
            return false;
        }

        // The month abbreviation should match the current month we're parsing
        // We already have the month number from the section header
        try
        {
            date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Local);
            return true;
        }
        catch (ArgumentException)
        {
            this._logger.LogWarning("Invalid date: {Day}/{Month}/{Year}", day, month, year);
            return false;
        }
    }
}