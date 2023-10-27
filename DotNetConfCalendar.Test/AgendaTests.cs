using System.Text.RegularExpressions;
using DotNetConfCalendar.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetConfCalendar.Test;

public class AgendaTests
{
    [Test]
    public async Task GetSessionsAsync_Test()
    {
        // Given
        using var services = TestHost.GetServiceProvider();
        var agenda = services.GetRequiredService<Agenda>();

        // When
        var sessions = await agenda.GetSessionsAsync();

        // Then
        sessions.Select(s => $"{s.StartTime:MM/dd/yyyy HH:mm} - {s.EndTime:MM/dd/yyyy HH:mm}, {s.Title}, {s.Speakers}, {s.Description}")
            .Is([
                "11/15/2023 07:45 - 11/15/2023 08:30, Session A, Speakers A, Description A",
                "11/16/2023 00:00 - 11/16/2023 00:30, Session B, Speakers B, Description B",
                "11/16/2023 07:30 - 11/16/2023 08:00, Session C, Speakers C, Description C",
                "11/17/2023 07:30 - 11/17/2023 08:00, Session D, Speakers D, Description D",
                "11/17/2023 07:30 - 11/17/2023 08:00, Session E, Speakers E, Description E",
            ]);
    }

    [Test]
    public async Task GetSessionsAsICalAsync_StartMidnight_Test()
    {
        // Given
        using var services = TestHost.GetServiceProvider();
        var agenda = services.GetRequiredService<Agenda>();

        // When
        var ical = await agenda.GetSessionsAsICalAsync();

        // Then
        ical.Split("\r\n")
            .Where(l => l.StartsWith("DTSTART"))
            .ToList()
            .ForEach(l => l.Is(l => Regex.IsMatch(l, "^DTSTART:202311(14|15|16|17)T\\d{6}Z$")));
    }
}