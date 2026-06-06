using System.ComponentModel.DataAnnotations;

namespace resturanyar.Models
{
    public class RestaurantTable
    {
        [Key]   
        public int TableId { get; set; }
        public int RestaurantId { get; set; }
        public string TableName { get; set; }
        public int Seats { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation property
        public Restaurant Restaurant { get; set; }
    }

}
