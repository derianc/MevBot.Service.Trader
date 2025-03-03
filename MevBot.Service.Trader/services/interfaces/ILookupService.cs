using MevBot.Service.Data;

namespace MevBot.Service.Trader.services.interfaces
{
    public interface ILookupService
    {
        Task<decimal> GetDexLiquidity(TradeData trade);
        Task<ulong> GetGasFee(TradeData trade);
        Task<double> GetOptimalBuyAmount(TradeData trade, double gasFees, double feeRate);
        decimal GetSlippage(TradeData trade, decimal poolLiquidity);
    }
}
