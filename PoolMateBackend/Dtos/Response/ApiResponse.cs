namespace PoolMate.Api.Dtos.Response;

public class ApiResponse<T>
{
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    // Success
    public static ApiResponse<T> Ok(T data, string message = "Success")
    {
        return new ApiResponse<T>
        {
            StatusCode = 200,
            Success = true,
            Message = message,
            Data = data
        };
    }

    //  Created (201)
    public static ApiResponse<T> Created(T data, string message = "Created successfully")
    {
        return new ApiResponse<T>
        {
            StatusCode = 201,
            Success = true,
            Message = message,
            Data = data
        };
    }

    //  Error
    public static ApiResponse<T> Fail(int statusCode, string message, List<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            StatusCode = statusCode,
            Success = false,
            Message = message,
            Data = default,
            Errors = errors
        };
    }
}