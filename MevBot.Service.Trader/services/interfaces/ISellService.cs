namespace MevBot.Service.Trader.services.interfaces
{
    public interface ISellService
    {
        Task<bool> Sell(decimal amountToSell, decimal expectedAmountOut, ulong gasFee, string buyerPublicKey, string tokenMintAddress);
    }
}
