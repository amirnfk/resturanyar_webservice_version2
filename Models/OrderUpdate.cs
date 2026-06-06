public class OrderUpdate
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int RestaurantId { get; set; }
    public int TargetRoleId { get; set; }
     public long UpdateTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
