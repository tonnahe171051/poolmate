namespace PoolMate.Api.Dtos.Admin.Player;

public class FileExportDto
{
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = "text/csv";
    public string Content { get; set; } = default!;
}