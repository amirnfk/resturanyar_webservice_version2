namespace resturanyar.Models.Receipt
{
    public enum ChargeCategory : byte
    {
        Discount = 0,
        Fee = 1,
        Tax = 2
    }

    public enum ChargeCalculationType : byte
    {
        Percentage = 0,
        Fixed = 1
    }

    public enum PercentageBaseKind : byte
    {
        ItemsNet = 0,
        ItemsNetPlusPriorFees = 1
    }

    public enum OrderTypeKind : byte
    {
        DineIn = 0,
        Takeaway = 1,
        Delivery = 2
    }

    [Flags]
    public enum OrderTypeFlags : byte
    {
        None = 0,
        DineIn = 1,
        Takeaway = 2,
        Delivery = 4,
        All = DineIn | Takeaway | Delivery
    }
}
