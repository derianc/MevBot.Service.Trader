using MevBot.Service.Data;
using System.Text.RegularExpressions;

namespace MevBot.Service.Trader.extensions
{
    public static class LogParserExtension
    {
        public static TradeData GetTradeData(this SolanaTransaction transaction)
        {
            var tradeData = new TradeData
            {
                SlotNumber = transaction?.@params?.result?.context?.slot ?? 0,

                AmountIn = Convert.ToDecimal(transaction?.GetValue(new Regex(@"amount_in:\s*(\d+)", RegexOptions.IgnoreCase))),
                AmountOut = Convert.ToDecimal(transaction?.GetValue(new Regex(@"amount_out:\s*(\d+)", RegexOptions.IgnoreCase))),
                ExpectedAmountOut = Convert.ToDecimal(transaction?.GetValue(new Regex(@"expect_amount_out:\s*(\d+)", RegexOptions.IgnoreCase))),
                CommissionAmount = Convert.ToDecimal(transaction?.GetValue(new Regex(@"commission_amount:\s*(\d+)", RegexOptions.IgnoreCase))),
                CommissionDirection = transaction?.GetValue(new Regex(@"commission_direction:\s*(\w+)", RegexOptions.IgnoreCase)),
                SourceTokenChange = Convert.ToDecimal(transaction?.GetValue(new Regex(@"source_token_change:\s*(\d+)", RegexOptions.IgnoreCase))),
                DestinationTokenChange = Convert.ToDecimal(transaction?.GetValue(new Regex(@"destination_token_change:\s*(\d+)", RegexOptions.IgnoreCase))),
                BeforeSourceBalance = Convert.ToDecimal(transaction?.GetValue(new Regex(@"before_source_balance:\s*(\d+)", RegexOptions.IgnoreCase))),
                BeforeDestinationBalance = Convert.ToDecimal(transaction?.GetValue(new Regex(@"before_destination_balance:\s*(\d+)", RegexOptions.IgnoreCase))),
                MinimumReturn = Convert.ToDecimal(transaction?.GetValue(new Regex(@"min_return:\s*(\d+)", RegexOptions.IgnoreCase))),
            };

            return tradeData;
        }

        private static string GetValue(this SolanaTransaction transaction, Regex regex)
        {
            var transactionLog = transaction.@params?.result?.value?.logs;

            string value = transactionLog?.Select(log => regex.Match(log))
                                          .Where(match => match.Success)
                                          .Select(match => match.Groups[1].Value)
                                          .FirstOrDefault() ?? string.Empty;

            return value;
        }
    }
}
