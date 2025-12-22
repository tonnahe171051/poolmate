using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PoolMate.Api.Hubs;


[Authorize]
public class AppHub : Hub
{
    private readonly ILogger<AppHub> _logger;

    public AppHub(ILogger<AppHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// The user is automatically identified via JWT claims.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        var connectionId = Context.ConnectionId;

        _logger.LogInformation(
            "User {UserId} connected to AppHub. ConnectionId: {ConnectionId}",
            userId ?? "Anonymous", connectionId);

        // Optionally add user to a group based on their role
        // var user = Context.User;
        // if (user?.IsInRole("Admin") == true)
        // {
        //     await Groups.AddToGroupAsync(connectionId, "Admins");
        // }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        var connectionId = Context.ConnectionId;

        if (exception != null)
        {
            _logger.LogWarning(exception,
                "User {UserId} disconnected from AppHub with error. ConnectionId: {ConnectionId}",
                userId ?? "Anonymous", connectionId);
        }
        else
        {
            _logger.LogInformation(
                "User {UserId} disconnected from AppHub. ConnectionId: {ConnectionId}",
                userId ?? "Anonymous", connectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }


    public async Task AcknowledgeLogout(string message)
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation(
            "User {UserId} acknowledged logout: {Message}",
            userId ?? "Anonymous", message);

        await Task.CompletedTask;
    }


    public async Task<object> Ping()
    {
        return await Task.FromResult(new
        {
            message = "Pong",
            serverTime = DateTimeOffset.UtcNow,
            connectionId = Context.ConnectionId
        });
    }
}


public static class AppHubEvents
{

    public const string ReceiveLogoutCommand = "ReceiveLogoutCommand";


    public const string ReceiveNotification = "ReceiveNotification";


    public const string SessionInvalidated = "SessionInvalidated";
}

