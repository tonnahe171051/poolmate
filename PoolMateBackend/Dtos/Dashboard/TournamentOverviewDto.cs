namespace PoolMate.Api.Dtos.Dashboard;

public class TournamentOverviewDto
{
    // Thông tin cơ bản
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    // 1. Tiến độ trận đấu
    public int TotalMatches { get; set; }
    public int CompletedMatches { get; set; }
    public int InProgressMatches { get; set; }
    public int ScheduledMatches { get; set; } // Đã có đủ người, đang chờ bàn
    public double ProgressPercentage { get; set; } // VD: 45.5 (%)

    // 2. Tình trạng VĐV
    public int TotalPlayers { get; set; }
    public int ConfirmedPlayers { get; set; }
    public int UnconfirmedPlayers { get; set; } // Cần chú ý xử lý

    // 3. Tình trạng Bàn (Resources)
    public int TotalTables { get; set; }
    public int ActiveTables { get; set; } // Bàn đang có trận (Status = InUse)
    public int FreeTables { get; set; }   // Bàn trống
}

