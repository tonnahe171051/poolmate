namespace PoolMate.Api.Dtos.Auth
{
    public class Response
    {
        public string Status { get; init; } = default!;  // "Success" | "Error"
        public string Message { get; init; } = default!;
        public object? Data { get; init; }

        public static Response Ok(string message) => new() { Status = "Success", Message = message };
        public static Response Error(string message) => new() { Status = "Error", Message = message };
        public static Response Ok<T>(T data, string message = "OK")   
            => new() { Status = "Success", Message = message, Data = data };
    }
}

