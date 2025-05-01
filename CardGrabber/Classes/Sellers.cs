using System;

namespace CardGrabber.Classes
{
    internal class Sellers
    {
        public string Username { get; set; }
        public string Type { get; set; }
        public string Info { get; set; }
        public string CardMarketRank { get; set; }
        public string Country { get; set; }
        public int Singles { get; set; }
        public int Buy { get; set; }
        public int Sell { get; set; }
        public int SellNotSent { get; set; }
        public int SellNotArrived { get; set; }
        public int BuyNotPayed { get; set; }
        public int BuyNotReceived { get; set; }
    }
}
