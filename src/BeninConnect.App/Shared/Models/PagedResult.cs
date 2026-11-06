namespace BeninConnect.App.Shared.Models;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Skip)
{
    public bool HasMore => Skip + Items.Count < TotalCount;
}
