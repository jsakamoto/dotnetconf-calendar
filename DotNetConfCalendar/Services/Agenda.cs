using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
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

    private TimeZoneInfo GetTimeZoneInfo(string timeZoneId)
    {
        return TimeZoneInfo.FindSystemTimeZoneById(this._abbreviationToTimeZoneId.TryGetValue(timeZoneId, out var standardTimeZoneId) ? standardTimeZoneId : timeZoneId);
    }

    internal async ValueTask<IEnumerable<Session>> GetSessionsAsync()
    {
        // Fetch the Agenda page of .NET Conf.
        var htpClient = this._httpClientFactory.CreateClient();
        var response = await htpClient.GetStringAsync("https://www.dotnetconf.net/agenda");

        // Parse the HTML string by AngleSharp's HtmlParser
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(response);

        return this.EnumerateSessions(document, "")
            .Concat(this.EnumerateSessions(document, "bonus"));
    }

    private IEnumerable<Session> EnumerateSessions(IHtmlDocument document, string prefix)
    {
        var agendaGroupTitles = document.QuerySelectorAll($".agenda-group-title[data-{prefix}sessionid]");

        foreach (var agendaGroupTitle in agendaGroupTitles)
        {
            var sessionId = agendaGroupTitle.GetAttribute($"data-{prefix}sessionid");
            var timeSpan = agendaGroupTitle.QuerySelector("span")!;
            var startTimeGroups = Regex.Match(timeSpan.GetAttribute($"data-{prefix}start")!, "^(?<datetime>.+)[ ]+(?<timeZone>[A-Z]+)$").Groups;
            var endTimeGroups = Regex.Match(timeSpan.GetAttribute($"data-{prefix}end")!, "^(?<datetime>.+)[ ]+(?<timeZone>[A-Z]+)$").Groups;
            var agendaSession = document.QuerySelector($".agenda-group-sessions-container[data-{prefix}sessionid='{sessionId}'] .agenda-session")!;
            var title = agendaSession.QuerySelector(".agenda-title")?.TextContent.Trim();
            var speakers = agendaSession.QuerySelector(".agenda-speaker-name")?.TextContent.Trim();
            var description = agendaSession.QuerySelector(".agenda-description")?.TextContent.Trim();

            var startTime = TimeZoneInfo.ConvertTimeToUtc(DateTime.Parse(startTimeGroups["datetime"].Value), this.GetTimeZoneInfo(startTimeGroups["timeZone"].Value));
            var endTime = TimeZoneInfo.ConvertTimeToUtc(DateTime.Parse(endTimeGroups["datetime"].Value), this.GetTimeZoneInfo(endTimeGroups["timeZone"].Value));

            yield return new Session()
            {
                StartTime = startTime,
                EndTime = endTime,
                Title = title,
                Speakers = speakers,
                Description = description
            };
        }
    }

    internal async ValueTask<string> GetSessionsAsICalAsync()
    {
        var sessionList = await this.GetSessionsAsync();
        var calendar = new Calendar();
        calendar.AddProperty("X-WR-CALNAME", ".NET Conf");
        calendar.AddProperty("X-WR-CALDESC", "Join the .NET Conf 2024 free virtual event November 12-14 to learn about the newest developments across the .NET platform, open source, and dev tools. Mark your calendar!");
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