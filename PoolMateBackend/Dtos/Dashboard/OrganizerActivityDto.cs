namespace PoolMate.Api.Dtos.Dashboard;

public class OrganizerActivityDto
{
    public string Time { get; set; } = default!;
    public string Message { get; set; } = default!;
    public ActivityType Type { get; set; }
}

public enum ActivityType
{
    PlayerRegistration,
    TournamentCreated,
    TournamentStarted,
    TournamentEnded,
    PlayerStatusChanged
}

