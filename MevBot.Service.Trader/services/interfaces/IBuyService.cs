namespace MevBot.Service.Trader.services.interfaces
{
    public interface IBuyService
    {
        Task<bool> Buy(ulong amountLamports, string recipientPublicKey);
    }
}
