namespace MevBot.Service.Trader.models
{
    public class TradeData
    {
        public decimal Amount { get; set; }
        public string? TokenIn { get; set; }
        public string? TokenOut { get; set; }
        public decimal PriceImpact { get; set; }
        public string? TokenAddress { get; set; }
    }
}
