namespace Nucleus.Application.Common;

public record ApiResponse<T>(bool Success, T? Data, string? Error, PaginationMeta? Meta = null);

public record PaginationMeta(int Total, int Page, int Limit);

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data) => new(true, data, null);
    public static ApiResponse<T> Fail<T>(string error) => new(false, default, error);
    public static ApiResponse<object> Fail(string error) => new(false, null, error);
}
