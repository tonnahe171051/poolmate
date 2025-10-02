
using System.Collections.Generic;

namespace PoolMate.Api.Common
{
    public class PagingList<T> : List<T>
    {
        public int PageIndex { get; private set; }
        public int TotalPages { get; private set; }
        public int TotalRecords { get; private set; }
        public int PageSize { get; private set; }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public PagingList(List<T> items, int totalRecords, int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
            TotalRecords = totalRecords;
            TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            AddRange(items);
        }

        public static PagingList<T> Create(List<T> source, int totalRecords, int pageIndex, int pageSize)
        {
            return new PagingList<T>(source, totalRecords, pageIndex, pageSize);
        }
    }
}
