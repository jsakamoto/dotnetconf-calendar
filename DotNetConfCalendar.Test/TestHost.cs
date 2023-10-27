using System.Text;
using DotNetConfCalendar.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetConfCalendar.Test;

internal class TestHost
{
    private class AgendaPageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var content = File.ReadAllText(Path.Combine(baseDir, "Assets", "agenda.html"));
            return Task.FromResult(new HttpResponseMessage()
            {
                Content = new StringContent(content, Encoding.UTF8, "text/html")
            });
        }
    }

    public static ServiceProvider GetServiceProvider()
    {
        return new ServiceCollection()
            .AddHttpClient()
            .ConfigureHttpClientDefaults(builder =>
            {
                builder.ConfigurePrimaryHttpMessageHandler((_) => new AgendaPageHandler());
            })
            .AddSingleton<Agenda>()
            .BuildServiceProvider();
    }
}
