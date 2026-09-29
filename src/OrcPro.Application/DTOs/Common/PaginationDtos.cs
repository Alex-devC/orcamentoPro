namespace OrcPro.Application.DTOs.Common;

public class SortRequest
{
    public string PropertyName { get; set; } = string.Empty;
    public bool IsDescending { get; set; }

    public SortRequest() { }

    public SortRequest(string propertyName, bool isDescending = false)
    {
        PropertyName = propertyName;
        IsDescending = isDescending;
    }
}

public class FilterRequest
{
    public string PropertyName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Operation { get; set; } = "Contains"; // Contains, Equals, GreaterThan, LessThan, In
}

public class PagedRequest
{
    private const int MaxPageSize = 500;
    private int _pageSize = 20;

    public int PageNumber { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : (value < 1 ? 1 : value);
    }

    public string? SearchTerm { get; set; }
    public List<SortRequest> Sorts { get; set; } = new();
    public List<FilterRequest> Filters { get; set; } = new();
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public static PagedResult<T> Empty(int pageNumber = 1, int pageSize = 20)
        => new(Array.Empty<T>(), 0, pageNumber, pageSize);
}
