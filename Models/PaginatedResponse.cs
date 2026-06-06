namespace resturanyar.Models
{
    public class PaginatedResponse<T>
    {
        public bool Success { get; set; }
        public List<T> Data { get; set; }
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public long LastCheck { get; set; }
        public string Message { get; set; }
    }
}