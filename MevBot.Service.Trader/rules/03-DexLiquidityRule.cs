using MevBot.Service.Data;
using NRules.Fluent.Dsl;
using NRules.RuleModel;

namespace MevBot.Service.Trader.rules
{
    public class _03_DexLiquidityRule : Rule
    {
        public override void Define()
        {
            TradeData? trade = null;

            When()
                .Match<TradeData>(() => trade,
                    t => t.NetworkLiquidity >= t.AmountToSell
                );

            Then()
                .Do(ctx => SetDexLiquidityPassed(trade, ctx));
        }

        private void SetDexLiquidityPassed(TradeData? trade, IContext ctx)
        {
            trade.DexLiquidityPassed = true;
            ctx.Update(trade);
        }
    }
}