using MevBot.Service.Data;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using NRules.Fluent.Dsl;
using Org.BouncyCastle.Crypto.Agreement.Srp;
using Solnet.Rpc;
using Solnet.Rpc.Types;
using Solnet.Wallet.Bip39;
using Solnet.Wallet;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;


namespace MevBot.Service.Trader.rules
{
    //public class _03_DexLiquidityRule : Rule
    //{
    //    private readonly string _wsUrl;
    //    private readonly IConfiguration _configuration;
    //    private static readonly HttpClient _httpClient = new HttpClient();
    //    private static readonly IRpcClient rpcClient = ClientFactory.GetClient(Cluster.MainNet);

    //    public _03_DexLiquidityRule(IConfiguration configuration)
    //    {
    //        _configuration = configuration;
    //        _wsUrl = _configuration.GetValue<string>("Solana:WsUrl") ?? string.Empty;
    //    }

    //    public override void Define()
    //    {
    //        var trade = new TradeData();

    //        When()
    //            .Match<TradeData>(() => trade,
    //                t => t.SlotNumber > 0,                          // Slot exists
    //                t => t.ExpectedAmountOut >= t.MinimumReturn     // Valid slippage tolerance
    //            );

    //        Then()
    //            .Do(ctx => CheckDexLiquidity1(trade));
    //    }


    //    /// <summary>
    //    /// Uses Solana WebSocket to check DEX liquidity in real time.
    //    /// </summary>
    //    private async void CheckDexLiquidity1(TradeData trade)
    //    {
    //        // Create a TaskCompletionSource to await the fee from the WebSocket response.
    //        var tcs = new TaskCompletionSource<decimal>();

    //        var solanaClient = new SolanaWebSocketClient(_wsUrl, async (message) =>
    //        {
    //            using JsonDocument doc = JsonDocument.Parse(message);

    //            if (doc.RootElement.TryGetProperty("result", out JsonElement result) &&
    //                result.TryGetProperty("value", out JsonElement value) &&
    //                value.TryGetProperty("data", out JsonElement dataElement))
    //            {
    //                // If "data" is a number directly
    //                if (dataElement.ValueKind == JsonValueKind.Number)
    //                {
    //                    decimal liquidity = dataElement.GetDecimal();
    //                    tcs.TrySetResult(liquidity);
    //                }
    //                else if (dataElement.ValueKind == JsonValueKind.Array && dataElement.GetArrayLength() > 0)
    //                {
    //                    // If "data" is an array and contains at least one element
    //                    decimal liquidity = dataElement[0].GetDecimal();
    //                    tcs.TrySetResult(liquidity);
    //                }
    //                else
    //                {
    //                    Console.WriteLine("Unexpected JSON format: 'data' is not a valid number or array.");
    //                }
    //            }
    //        });

    //        // Subscribe to liquidity updates
    //        var subscribeMessage = new
    //        {
    //            jsonrpc = "2.0",
    //            id = 1,
    //            method = "accountSubscribe",
    //            @params = new object[]
    //            {
    //                    trade.TokenMintAddress,
    //                    new
    //                    {
    //                        encoding = "jsonParsed"
    //                    }
    //            }
    //        };

    //        await solanaClient.ConnectAsync();
    //        await solanaClient.SendAsync(subscribeMessage);

    //        trade.NetworkLiquidity = await tcs.Task;

    //        if (trade.NetworkLiquidity >= trade.AmountToSell)
    //            trade.DexLiquidityPassed = true;

    //        await solanaClient.DisconnectAsync();
    //    }
    //}
}