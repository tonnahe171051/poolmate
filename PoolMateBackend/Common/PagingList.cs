
namespace PoolMate.Api.Common
{
    public class PagingList<T>
    {
        public int PageIndex { get; init; }
        public int TotalPages { get; init; }
        public int TotalRecords { get; init; }
        public int PageSize { get; init; }
        public bool HasPreviousPage { get; init; }
        public bool HasNextPage { get; init; }
        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

        public static PagingList<T> Create(
            IReadOnlyList<T> items, int totalRecords, int pageIndex, int pageSize)
            => new PagingList<T>
        {
                Items = items,
                TotalRecords = totalRecords,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                HasPreviousPage = pageIndex > 1,
                HasNextPage = pageIndex * pageSize < totalRecords
            };
    }
}
