using System.Security.Cryptography;
using System.Text;

namespace DotNetConfCalendar.Models;

internal class Session
{
    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Title { get; set; }

    public string? Speakers { get; set; }

    public string? Description { get; set; }

    public string GetHashForUID()
    {
        return new Guid(MD5.HashData(Encoding.UTF8.GetBytes(this.Title ?? ""))).ToString();
    }
}
