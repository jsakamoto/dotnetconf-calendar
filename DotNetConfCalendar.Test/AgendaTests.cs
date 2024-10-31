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
                "11/13/2024 07:45 - 11/13/2024 08:30, Session A, Speakers A, Description A",
                "11/14/2024 01:00 - 11/14/2024 01:30, Session B, Speakers B, Description B",
                "11/14/2024 07:30 - 11/14/2024 08:00, Session C, Speakers C, Description C",
                "11/15/2024 07:30 - 11/15/2024 08:00, Session D, Speakers D, Description D",
                "11/15/2024 07:30 - 11/15/2024 08:00, Session E, Speakers E, Description E",
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
            .ForEach(l => l.Is(l => Regex.IsMatch(l, "^DTSTART:202411(13|14|15|16)T\\d{6}Z$")));
    }
}