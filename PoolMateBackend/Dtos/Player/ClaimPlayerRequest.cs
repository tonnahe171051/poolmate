namespace PoolMate.Api.Dtos.Player;

/// <summary>
/// Request để user tự claim Player profile
/// </summary>
public class ClaimPlayerRequest
{
    /// <summary>
    /// Có copy data từ Player về User profile không (optional)
    /// </summary>
    public bool UpdateUserProfile { get; set; } = false;
}

