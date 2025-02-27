using MevBot.Service.Data;
using NRules.Fluent.Dsl;
using Solnet.Rpc;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Messages;
using Solnet.Rpc.Models;

namespace MevBot.Service.Trader.rules
{
    public class _03_DexLiquidityRule : Rule
    {
        private readonly IConfiguration _configuration;
        private readonly string _rpcUrl;
        private readonly IRpcClient _rpcClient;

        // ---
        private static readonly HttpClient client = new HttpClient();
        private const string RAYDIUM_API_URL = "https://api.raydium.io/v2/sdk/pools";
        // ---

        public _03_DexLiquidityRule(IConfiguration configuration)
        {
            _configuration = configuration;
            _rpcUrl = _configuration.GetValue<string>("Solana:RpcUrl") ?? string.Empty;
            _rpcClient = ClientFactory.GetClient(_rpcUrl);
        }

        public override void Define()
        {
            var trade = new TradeData();

            When()
                .Match(() => trade,
                    t => t.SlotNumber > 0,                          // Slot exists
                    t => t.ExpectedAmountOut >= t.MinimumReturn     // Valid slippage tolerance
                );

            Then()
                .Do(ctx => CheckDexLiquidity(trade));
        }

        // AYe6u4HP4WQXbfxURs9M2UgpHF9zpY9Wg19LfuvY9PTb

        /// <summary>
        /// Checks DEX liquidity using Solana RPC to fetch the token account balance.
        /// </summary>
        /// <param name="trade">The trade data instance containing liquidity parameters.</param>
        private async void CheckDexLiquidity(TradeData trade)
        {
            // Fetch the token account balance using RPC. 
            // Here trade.TokenMintAddress is assumed to be the liquidity pool account holding the tokens.
            var balanceResponse = await GetTokenAccountBalance(trade);

            var balanceValue = balanceResponse.Result.Value;
            // Convert the amount from its raw format using the provided decimals.
            decimal liquidity = decimal.Parse(balanceValue.Amount) / (decimal)Math.Pow(10, balanceValue.Decimals);
            trade.NetworkLiquidity = liquidity;

            // Check if the liquidity meets or exceeds the amount required for selling.
            if (liquidity >= trade.AmountToSell)
                trade.DexLiquidityPassed = true;
        }

        private async Task<RequestResult<ResponseValue<TokenBalance>>> GetTokenAccountBalance(TradeData trade)
        {
            {
                foreach (var value in trade.LiquidityPoolAddress)
                {
                    try
                    {
                        var balanceResponse = await _rpcClient.GetTokenAccountBalanceAsync(value);
                        if (balanceResponse.WasSuccessful && balanceResponse.Result?.Value != null)
                        {
                            return balanceResponse;
                        }
                    }
                    catch { }
                }
            }            
            return null;
        }
    }
}
