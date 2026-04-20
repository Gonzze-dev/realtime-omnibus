namespace RealTime.Models;

public class Notification
{
    public Guid Id { get; set; }
    public string? GroupKey { get; set; }
    public string GroupName { get; set; } = null!;
    public DateTime Expiration { get; set; }
    public DateTime Date { get; set; }
    public string Payload { get; set; } = null!;
}
