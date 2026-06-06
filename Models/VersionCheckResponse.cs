namespace resturanyar.Models
{
    public class VersionCheckResponse
    {
        public bool forceUpdate { get; set; }
        public bool softUpdate { get; set; }
        public string message { get; set; }
        public string updateUrl { get; set; }
    }
}
