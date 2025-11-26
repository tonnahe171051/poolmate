using PoolMate.Api.Dtos.Admin.Payout;
using PoolMate.Api.Dtos.Auth;

namespace PoolMate.Api.Services;

public interface IAdminPayoutService
{
    /// Lấy danh sách tất cả các mẫu chia thưởng (Templates).
    Task<List<PayoutTemplateDto>> GetTemplatesAsync(CancellationToken ct = default);
    
    /// Lấy chi tiết một mẫu chia thưởng theo ID.
    Task<PayoutTemplateDto?> GetTemplateByIdAsync(int id, CancellationToken ct = default);
    
    /// Tạo mới một mẫu chia thưởng.
    Task<PayoutTemplateDto> CreateTemplateAsync(CreatePayoutTemplateDto dto, CancellationToken ct = default);
    
    /// Cập nhật mẫu chia thưởng hiện có.
    Task<PayoutTemplateDto?> UpdateTemplateAsync(int id, CreatePayoutTemplateDto dto, CancellationToken ct = default);
    
    /// Xóa một mẫu chia thưởng.
    /// (Lưu ý: Cần check xem template có đang được sử dụng bởi giải đấu nào không trước khi xóa).
    Task<Response> DeleteTemplateAsync(int id, CancellationToken ct = default);
    
    /// Tính toán thử (Simulate) số tiền thưởng dựa trên tổng quỹ và công thức.
    /// Dùng để Admin kiểm tra trước khi áp dụng.
    Task<PayoutSimulationResultDto> SimulatePayoutAsync(PayoutSimulationRequestDto request,
        CancellationToken ct = default);
}