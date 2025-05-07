public class SellerItemsInfo
{
    public string ItemType { get; set; }
    public int Count { get; set; }
    public float AveragePrice { get; set; }

    public SellerItemsInfo(string itemType, int count, float averagePrice)
    {
        ItemType = itemType;
        Count = count;
        AveragePrice = averagePrice;
    }
}