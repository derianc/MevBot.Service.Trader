using MevBot.Service.Data;
using NRules.Fluent.Dsl;
using NRules.RuleModel;

namespace MevBot.Service.Trader.rules
{
    public class _04_SlippageToleranceRule : Rule
    {
        public override void Define()
        {
            TradeData trade = null;

            When()
                .Match<TradeData>(() => trade,
                    t => t.AmountIn > 0,
                    t => t.MinimumReturn > 0);

            Then()
                .Do(ctx => CalculateSlippage(trade, ctx));
        }

        private void CalculateSlippage(TradeData? trade, IContext ctx)
        {
            try
            {
                var marketPrice = trade.AmountOut / trade.AmountIn;     // price per token from your log extraction
                var tradeSize = trade.AmountOut;                        // tokens the victim is trading
                var poolLiquidity = trade.NetworkLiquidity;             // available network liquidity

                // Estimate price impact using a simple model (constant product or linear approximation)
                var estimatedPriceImpact = tradeSize / poolLiquidity;
                var slippage = marketPrice * (1 + estimatedPriceImpact);     // slippage

                // Assume you bought slightly below the current market price (front-run price)
                var frontRunPrice = marketPrice * 1.001m; // includes a small premium for rapid execution

                // Calculate potential profit per token
                var profitPerToken = slippage - frontRunPrice;

                // Total profit from the trade:
                var totalProfit = profitPerToken * tradeSize;

                // Convert gas fee from lamports to USDC (assuming 1 USDC = 1,000,000 lamports)
                decimal gasFeeInUSDC = trade.GasFee / 1_000_000m;

                // Calculate slippage fee: extra cost per token due to price impact times number of tokens.
                decimal slippageFeeTotal = (slippage - marketPrice) * trade.AmountOut;

                // Required profit must cover the gas fee plus the slippage fee, with an additional 5% margin.
                decimal requiredProfitThreshold = (gasFeeInUSDC + slippageFeeTotal) * 1.05m;

                // Determine if the trade is profitable after considering costs
                if (totalProfit > requiredProfitThreshold)
                {
                    trade.Slippage = slippage;
                    trade.SlippagePassed = true;  // trade appears profitable
                }
                else
                {
                    trade.SlippagePassed = false; // not profitable under your criteria
                }

                ctx.Update(trade);
            }
            catch (Exception ex)
            {
                // Mark rule as passed.
                trade.SlippagePassed = false;
            }
        }
    }
}
