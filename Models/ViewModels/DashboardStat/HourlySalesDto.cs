namespace resturanyar.Models.ViewModels.DashboardStat
{
    public class HourlySalesDto
    {
        public decimal[] Today { get; set; }
        public decimal[] Yesterday { get; set; }
        public string[] Labels { get; set; }
    }
}
