using System.Text.Json.Serialization;

public class CardsInfo
{
    [JsonIgnore]
    public int CardID { get; set; }

    [JsonPropertyName("SU")]
    public string SellerUsername { get; set; }

    [JsonPropertyName("SC")]
    public string SellerCountry { get; set; }

    [JsonPropertyName("CC")]
    public string CardCondition { get; set; }
    [JsonPropertyName("CL")]
    public string CardLanguage { get; set; }

    [JsonPropertyName("CCM")]
    public string CardComment { get; set; }

    [JsonPropertyName("CP")]
    public decimal CardPrice { get; set; }

    [JsonPropertyName("CQ")]
    public int CardQuantity { get; set; }

    public CardsInfo(int cardID, string sellerUsername, string sellerCountry, string cardCondition, string cardLanguage, string cardComment, decimal cardPrice, int cardQuantity)
    {
        CardID = cardID;
        SellerUsername = sellerUsername;
        SellerCountry = sellerCountry;
        CardCondition = cardCondition;
        CardLanguage = cardLanguage;
        CardComment = cardComment;
        CardPrice = cardPrice;
        CardQuantity = cardQuantity;
    }

}
