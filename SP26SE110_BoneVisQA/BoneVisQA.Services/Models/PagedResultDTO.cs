namespace BoneVisQA.Services.Models;

public class PagedResultDTO<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }

    public PagedResultDTO()
    {
        // Computed during construction so it gets serialized in JSON responses
    }

    public int ComputeTotalPages()
    {
        TotalPages = PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
        return TotalPages;
    }
}
