using DotNetConfCalendar.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddTransient<Agenda>();
builder.Services.AddCors();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors(builder => builder.WithMethods("GET").AllowAnyOrigin().AllowAnyHeader());

app.MapGet("/ical/v1", async (Agenda agenda) =>
{
    var icalString = await agenda.GetSessionsAsICalAsync();
    return Results.Text(icalString, "text/calendar");
});

app.Run();
