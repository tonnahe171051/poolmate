using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PoolMate.Api.Hubs;

[Authorize]
public class TournamentHub : Hub
{
    public const string TournamentGroupPrefix = "tournament-";

    public static string GetGroupName(int tournamentId) => $"{TournamentGroupPrefix}{tournamentId}";

    public async Task JoinTournament(int tournamentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(tournamentId));
    }

    public async Task LeaveTournament(int tournamentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(tournamentId));
    }
}
