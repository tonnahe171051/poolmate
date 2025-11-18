namespace PoolMate.Api.Dtos.Player;

/// <summary>
/// Response sau khi claim Player thành công
/// </summary>
public class ClaimPlayerResponse
{
    public int PlayerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

