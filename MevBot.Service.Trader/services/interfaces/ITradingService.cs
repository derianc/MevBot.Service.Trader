namespace MevBot.Service.Trader.services.interfaces
{
    public interface ITradingService
    {
        Task<bool> Buy(ulong amountLamports, string recipientPublicKey);
        Task<bool> Sell(decimal amountToSell, decimal expectedAmountOut, ulong gasFee, string buyerPublicKey, string tokenMintAddress);
    }
}
