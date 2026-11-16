namespace HotelSaaS.Application.Common;

/// <summary>Consistent API envelope (PRD section 51).</summary>
public record ApiResponse<T>(bool Success, string? Message = null, T? Data = default, object? Errors = null)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, message, data);
    public static ApiResponse<T> Fail(string message, object? errors = null) => new(false, message, default, errors);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
