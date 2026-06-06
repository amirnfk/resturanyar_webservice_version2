namespace resturanyar.Models

{
    public class OrderDto
    {
        public int OrderId { get; set; }
        public string TableNumber { get; set; }
        public int StatusId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedAtShamsi { get; set; }
        public string UpdatedAtShamsi { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerFullName { get; set; }
        public string CustomerMobile { get; set; }
        public string? Description { get; set; }
         
        public List<OrderItemDto> OrderItems { get; set; }

        
       
    }

}
