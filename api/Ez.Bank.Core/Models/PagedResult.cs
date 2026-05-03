namespace Ez.Bank.Core.Models;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageSize { get; init; }
    public string? ContinuationToken { get; init; }
    public bool HasMore { get; init; }
}
