namespace PoolMate.Api.Dtos.Admin.Player;

public class MergePlayerRequestDto
{
    /// ID của hồ sơ GỐC (Hồ sơ sẽ được giữ lại)
    public int TargetPlayerId { get; set; }
    
    /// Danh sách ID của các hồ sơ RÁC (Sẽ bị xóa sau khi gộp)
    public int SourcePlayerId { get; set; }
}