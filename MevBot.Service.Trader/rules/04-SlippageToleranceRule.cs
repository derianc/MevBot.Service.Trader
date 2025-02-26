using MevBot.Service.Data;
using NRules.Fluent.Dsl;
using NRules.RuleModel;

namespace MevBot.Service.Trader.rules
{
    public class _04_SlippageToleranceRule : Rule
    {
        public override void Define()
        {
            var trade = new TradeData();

            When()
                .Match(() => trade,
                    t => t.AmountIn > 0,
                    t => t.MinimumReturn > 0,
                    t => t.ExpectedAmountOut >= t.MinimumReturn);

            Then()
                .Do(ctx => RecordPass(ctx, trade));
        }

        private void RecordPass(IContext ctx, TradeData trade)
        {
            // Mark rule as passed.
            trade.SlippageTolerancePassed = true;
        }
    }
}
