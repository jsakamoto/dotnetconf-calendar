using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using DotNetConfCalendar.Models;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using TimeZoneNames;

namespace DotNetConfCalendar.Services;

internal class Agenda
{
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly IReadOnlyDictionary<string, string> _abbreviationToTimeZoneId;

    public Agenda(IHttpClientFactory httpClientFactory)
    {
        this._httpClientFactory = httpClientFactory;
        this._abbreviationToTimeZoneId = this.CreateAbbreviationToTimeZoneIdMap();
    }

    private IReadOnlyDictionary<string, string> CreateAbbreviationToTimeZoneIdMap()
    {
        return TimeZoneInfo.GetSystemTimeZones(skipSorting: true)
            .SelectMany(tz =>
            {
                var anames = TZNames.GetAbbreviationsForTimeZone(tz.Id, "en_us");
                return new[] { anames.Generic, anames.Standard, anames.Daylight }
                    .Where(aname => !string.IsNullOrEmpty(aname))
                    .Select(aname => (AbbreviationName: aname!, TimeZoneId: tz.Id));
            })
            .OrderBy(set => set.AbbreviationName)
            .DistinctBy(set => set.AbbreviationName)
            .ToDictionary(set => set.AbbreviationName, set => set.TimeZoneId);
    }

    internal async ValueTask<IEnumerable<Session>> GetSessionsAsync()
    {
        // Fetch the Agenda page of .NET Conf.
        var htpClient = this._httpClientFactory.CreateClient();
        var response = await htpClient.GetStringAsync("https://www.dotnetconf.net/agenda");

        // Parse the HTML string by AngleSharp's HtmlParser
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(response);
        var agendaContainer = document.QuerySelector(".agenda-container");

        var sessionList = new List<Session>();

        var day = DateOnly.MinValue;
        foreach (var section in agendaContainer?.Children ?? Enumerable.Empty<IElement>())
        {
            if (section.ClassList.Contains("agenda-group"))
            {
                foreach (var agenda in section.Children.Chunk(3))
                {
                    var schedule = agenda[0].QuerySelector("span")?.TextContent;
                    var title = agenda[2].QuerySelector(".agenda-title")?.TextContent;
                    var speakers = agenda[2].QuerySelector(".agenda-speaker-name")?.TextContent;
                    var description = agenda[2].QuerySelector(".agenda-description")?.TextContent;
                    if (string.IsNullOrEmpty(schedule)) continue;

                    var timeParts = schedule.Split(' ');
                    var startTime = TimeOnly.Parse(timeParts[0]);
                    var endTime = TimeOnly.Parse(timeParts[2]);
                    var timeZoneText = timeParts[3];

                    var timeZoneId = this._abbreviationToTimeZoneId.TryGetValue(timeZoneText, out var tzid) ? tzid : timeZoneText;
                    var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

                    var startDateTime = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(startTime), timeZoneInfo);
                    var endDateTime = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(endTime).AddDays(endTime < startTime ? +1 : 0), timeZoneInfo);

                    sessionList.Add(new Session
                    {
                        StartTime = startDateTime,
                        EndTime = endDateTime,
                        Title = title,
                        Speakers = speakers,
                        Description = description
                    });
                }
            }
            else
            {
                day = DateOnly.TryParse(section.QuerySelector("p")?.TextContent, out var d) ? d : day;
            }
        }

        return sessionList;
    }

    internal async ValueTask<string> GetSessionsAsICalAsync()
    {
        var sessionList = await this.GetSessionsAsync();
        var calendar = new Calendar();
        calendar.AddProperty("X-WR-CALNAME", ".NET Conf");
        calendar.AddProperty("X-WR-CALDESC", "Join the .NET Conf 2023 free virtual event November 14-16 to learn about the newest developments across the .NET platform, open source, and dev tools. Mark your calendar!");
        foreach (var session in sessionList)
        {
            var icalEvent = new CalendarEvent
            {
                IsAllDay = false,
                Uid = session.GetHashForUID(),
                DtStart = new CalDateTime(session.StartTime) { HasTime = true },
                DtEnd = new CalDateTime(session.EndTime) { HasTime = true },
                Summary = session.Title,
                Description = $"<b>Speaker(s):</b>\r\n{session.Speakers}\r\n\r\n<b>Description:</b>\r\n{session.Description}"
            };
            calendar.Events.Add(icalEvent);
        }

        var serializer = new CalendarSerializer(new SerializationContext());
        return serializer.SerializeToString(calendar);
    }
}