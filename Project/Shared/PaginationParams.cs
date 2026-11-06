namespace Project.Shared;

public class PaginationParams
{
    private int page = 1;
    private int pageSize = 20;
    public int Page { get => page; init => page = Math.Clamp(value, 1, 1_000_000); }
    public int PageSize { get => pageSize; init => pageSize = Math.Clamp(value, 1, 100); }
    public int Skip => (Page - 1) * PageSize;
}
