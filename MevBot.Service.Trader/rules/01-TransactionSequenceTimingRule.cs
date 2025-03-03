using MevBot.Service.Data;
using NRules.Fluent.Dsl;
using NRules.RuleModel;

namespace MevBot.Service.Trader.rules
{
    public class _01_TransactionSequenceTimingRule : Rule
    {
        public override void Define()
        {
            var trade = new TradeData();

            When()
                .Match<TradeData>(() => trade,
                    t => t.SlotNumber > 0,                          // Slot exists
                    t => t.ExpectedAmountOut >= t.MinimumReturn   // Valid slippage tolerance
                    
                    // t => t.PriorityFee > trade.VictimPriorityFee,  // Higher priority fee
                    // t => t.FrontRunTimestamp < t.VictimTimestamp,  // Front-run timing

                    //t => t.VictimTimestamp < t.BackRunTimestamp,   // Back-run timing
                    //t => t.ComputeUnitsRemaining > 5000,           // Ensure compute units
                    //t => t.FrontRunExecuted && t.VictimExecuted && t.BackRunExecuted // Sequence validation
                );

            Then()
                .Do(ctx => EvaluateSandwichOpportunity(trade, ctx));
        }

        private void EvaluateSandwichOpportunity(TradeData trade, IContext ctx)
        {
            Console.WriteLine($"[Slot {trade.SlotNumber}] Checking for sandwich trade opportunity...");

            if (trade.SlotNumber % 2 == 0) // Example condition for sandwich trade
            {
                trade.SequenceAndTimingPassed = true;
            }

            ctx.Update(trade);
        }
    }
}