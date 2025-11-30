namespace PoolMate.Api.Dtos.Dashboard;

public class OrganizerActivityDto
{

    public DateTime CreatedAt { get; set; }
    public string Message { get; set; } = default!;
    public string Type { get; set; } = default!; 
}

public enum ActivityType
{
    PlayerRegistration,
    TournamentCreated,
    TournamentStarted,
    TournamentEnded,
    PlayerStatusChanged
}