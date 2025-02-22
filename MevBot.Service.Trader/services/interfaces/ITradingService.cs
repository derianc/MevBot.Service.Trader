namespace MevBot.Service.Trader.services.interfaces
{
    public interface ITradingService
    {
        Task<bool> Buy(ulong amountLamports, string recipientPublicKey);
        Task<bool> Sell(ulong amountLamports, string buyerPublicKey, string tokenMintAddress);
    }
}
