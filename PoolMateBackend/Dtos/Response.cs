namespace PoolMate.Api.Dtos
{
    public class Response
    {
        public string Status { get; init; } = default!;  // "Success" | "Error"
        public string Message { get; init; } = default!;

        public static Response Ok(string message) => new() { Status = "Success", Message = message };
        public static Response Error(string message) => new() { Status = "Error", Message = message };
    }
}

