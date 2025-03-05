using MevBot.Service.Data;
using MevBot.Service.Trader.services.interfaces;
using Newtonsoft.Json.Linq;
using Solnet.Programs;
using Solnet.Programs.Models.TokenProgram;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Messages;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using Solnet.Programs.Abstract;
using Solnet.Rpc.Types;

namespace MevBot.Service.Trader.services
{
    public class LookupService : ILookupService
    {
        private readonly IConfiguration _configuration;

        private readonly string _rpcUrl;
        private readonly Solnet.Rpc.IRpcClient _rpcClient;

        private readonly Account _account;
        private readonly string _walletAddress;
        private readonly string _walletPrivateKey;

        private static readonly HttpClient httpClient = new HttpClient();
        private const string SolanaRpcUrl = "https://api.mainnet-beta.solana.com"; // Update if needed
        private const string SolanaWsUrl = "wss://silent-maximum-cloud.solana-mainnet.quiknode.pro/a781cdadcc0dcf1ea41d55e0e663a060060dfe74"; // Update if needed


        // The SPL Token Program ID is constant.
        private const string RaydiumAmmProgramId = "CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C";

        public LookupService(IConfiguration configuration)
        {
            _configuration = configuration;

            _rpcUrl = _configuration.GetValue<string>("Solana:RpcUrl") ?? string.Empty;
            _rpcClient = ClientFactory.GetClient(_rpcUrl);

            _walletPrivateKey = _configuration.GetValue<string>("Solana:WALLET_PRIVATE_KEY") ?? string.Empty;
            _walletAddress = _configuration.GetValue<string>("Solana:WALLET_ADDRESS") ?? string.Empty;
            _account = new Account(_walletPrivateKey, _walletAddress);
        }

        public async Task<decimal> GetDexLiquidity(TradeData trade)
        {
            string? lpTokenMintAddress = trade?.LiquidityPoolAddress?.First();
            
            do
            {
                await GetLiquidityPoolsWebSocket(lpTokenMintAddress);
            }
            while (1 > 0);

            //foreach (var temp in trade.LiquidityPoolAddress)
            //{
            //    var blah = await GetLiquidityPoolAddress(temp);
            //}


            //if (string.IsNullOrEmpty(lpTokenMintAddress))
            //    throw new ArgumentException("Liquidity pool address is not set.");

            // Fetch the liquidity pool address using the token mint address
            //var liquidityPoolAddress = await GetLiquidityPoolAddress(lpTokenMintAddress);

            // Fetch the reserve accounts for the liquidity pool
            var liquidityPoolAddress = "DeNgVCLjqXr2Ln1xuZK5PhTSRgj8WoXvTVuXWjoCxmAB";
            var (reserveATokenAccount, reserveBTokenAccount) = await GetReserveAccounts(liquidityPoolAddress);

            // Step 1: Get Liquidity Pool Reserves
            decimal reserveA = await GetTokenBalance(reserveATokenAccount);
            decimal reserveB = await GetTokenBalance(reserveBTokenAccount);  // Assume a dual-token pool

            // Step 2: Get LP Token Supply
            var lpSupply = await GetTokenSupply(lpTokenMintAddress);
            Console.WriteLine($"Total LP Token Supply: {lpSupply}");

            // Step 3: Fetch Token Prices (Example: From CoinGecko)
            decimal tokenAPrice = await GetTokenPrice("solana");
            decimal tokenBPrice = await GetTokenPrice("usd-coin");

            // Step 4: Compute Liquidity Pool Value
            decimal liquidityPoolValue = ((reserveA / lpSupply) * tokenAPrice) + ((reserveB / lpSupply) * tokenBPrice);

            return liquidityPoolValue;
        }

        public async Task GetLiquidityPoolsWebSocket(string tokenMintAddress)
        {
            var webSocketClient = new SolanaWebSocketClient(SolanaWsUrl, async (message) =>
            {
                Console.WriteLine($"Received message: {message}");
            });

            List<string> liquidityPools = new List<string>();

            // Subscribe to all Raydium AMM pools
            string[] raydiumAmmPrograms = new string[]
            {
                "CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C", // New AMM
                "675kPX9MHTjS2zt1qfr1NYHuzeLXfQM9H24wFSUt1Mp8"  // Old AMM
            };

            var requestPayload = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "programSubscribe",
                @params = new object[]
                {
                    "CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C", // Raydium AMM Program ID
                    new
                    {
                        encoding = "jsonParsed",
                        commitment = "finalized",
                        filters = new object[]
                        {
                            new
                            {
                                memcmp = new
                                {
                                    offset = 0,  // Adjust offset based on where the liquidity pool address is stored
                                    bytes = tokenMintAddress // Filter for the specific pool
                                }
                            }
                        }
                    }
                }
            };

            await webSocketClient.ConnectAsync();
            await webSocketClient.SendAsync(requestPayload);
            //await webSocketClient.DisconnectAsync();
        }

        private async Task<string> GetLiquidityPoolAddress(string? tokenMintAddress)
        {
            var temp_rpcClient = ClientFactory.GetClient("https://ssc-dao.genesysgo.net/");

            //string programId = "9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin";
            tokenMintAddress = "So11111111111111111111111111111111111111112";
            //string programId = "CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C"; // Raydium AMM Program ID
            string programId = "675kPX9MHTjS2zt1qfr1NYHuzeLXfQM9H24wFSUt1Mp8";
            try
            {
                // Define filter for Raydium AMM Liquidity Pools
                var filters = new MemCmp[]
                {
                    new MemCmp
                    {
                        Offset = 32, // Match at start of account data
                        Bytes = tokenMintAddress // Replace with a relevant token mint address if needed
                    }
                };

                // Query Solana RPC
                var response = await temp_rpcClient.GetProgramAccountsAsync(
                    new PublicKey(programId),
                    Commitment.Confirmed
                    ,322 // Raydium AMM liquidity pool size filter
                    ,filters
                );

                if (!response.WasSuccessful || response.Result == null)
                {
                    Console.WriteLine("Failed to fetch program accounts.");
                    return string.Empty;
                }

                Console.WriteLine($"Found {response?.Result?.Count} liquidity pools:");

                foreach (var account in response.Result)
                {
                    Console.WriteLine($"Liquidity Pool Address: {account.PublicKey}");
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching program accounts: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task<ulong> GetGasFee(TradeData trade)
        {
            // 1. Fetch recent block hash required for the transaction.
            var blockHashResponse = await _rpcClient.GetLatestBlockHashAsync();
            if (!blockHashResponse.WasSuccessful)
                throw new Exception("Failed to fetch recent block hash.");
            string recentBlockHash = blockHashResponse.Result.Value.Blockhash;

            // 2. Convert the buy amount to lamports (assuming 6 decimals for USDC)
            // If trade.AmountToBuy is not set, use a dummy value (e.g., 10 USDC)
            decimal amountToBuy = trade.AmountToBuy > 0 ? trade.AmountToBuy : 10m;
            ulong amountLamports = (ulong)(amountToBuy * 1_000_000m);  // 1 USDC = 1_000_000 lamports

            // 3. Build a preliminary "buy" transaction.
            // For demonstration, we simulate a buy by issuing a transfer instruction to a dummy recipient.
            var transactionBuilder = new TransactionBuilder()
                .SetFeePayer(_account)
                .SetRecentBlockHash(recentBlockHash)
                .AddInstruction(SystemProgram.Transfer(
                    _account.PublicKey,
                    new PublicKey("11111111111111111111111111111111"),
                    amountLamports));

            // 4. Build the full transaction (returns a byte array).
            var txBytes = transactionBuilder.Build(_account);

            // 5. Extract only the message portion from the transaction.
            // In Solana, the first byte is the signature count, and each signature is 64 bytes.
            byte[] messageBytes = ExtractMessageFromTransaction(txBytes);
            string serializedMessage = Convert.ToBase64String(messageBytes);

            // 6. Call the RPC client to get the fee for the transaction message.
            var feeResponse = await _rpcClient.GetFeeForMessageAsync(serializedMessage);
            if (!feeResponse.WasSuccessful || feeResponse?.Result.Value == null)
                throw new Exception("Failed to get fee for message.");

            return feeResponse.Result.Value;
        }

        public decimal GetSlippage(TradeData trade, decimal poolLiquidity)
        {
            var marketPrice = trade.AmountOut / trade.AmountIn;     // price per token from your log extraction
            var tradeSize = trade.AmountOut;                        // tokens the victim is trading
            // var poolLiquidity = trade.NetworkLiquidity;             // available network liquidity

            // Estimate price impact using a simple model (constant product or linear approximation)
            var estimatedPriceImpact = tradeSize / poolLiquidity;
            var slippage = marketPrice * (1 + estimatedPriceImpact);     // slippage

            return slippage;
        }

        /// <summary>
        /// Calculates the optimal front-run buy amount given:
        /// - The pool's starting reserves (X for tokenA and Y for tokenB)
        /// - The victim's trade amount (v in tokenA)
        /// - Gas fees (in tokenA)
        /// - The fee rate (for each swap)
        /// 
        /// The simulation performs three steps:
        ///   1. Your front-run: swapping 'a' tokenA to receive tokenB.
        ///   2. Victim's trade: victim swaps v tokenA after your trade.
        ///   3. Your back-run: swapping the tokenB you acquired back for tokenA.
        /// 
        /// Your profit is tokenA received from the back-run minus 'a' and gas fees.
        /// The function searches over a range of possible 'a' values to find the one that maximizes profit.
        /// </summary>
        /// <param name="X">Pool's source reserve (tokenA).</param>
        /// <param name="Y">Pool's destination reserve (tokenB).</param>
        /// <param name="v">Victim's trade input (tokenA amount).</param>
        /// <param name="gasFees">Gas fees (in tokenA units).</param>
        /// <param name="feeRate">Swap fee rate (e.g. 0.003 for 0.3%).</param>
        /// <returns>The optimal amount 'a' (in tokenA) to use in your front-run buy.</returns>
        public async Task<double> GetOptimalBuyAmount(TradeData trade, double gasFees, double feeRate)
        {
            double bestA = 0;
            double bestProfit = double.MinValue;

            // Define search range for our trade amount 'a'
            // (e.g. from 0 up to 5% of the pool's tokenA reserve)
            double aMin = 0;
            double aMax = Convert.ToDouble(trade.SourceTokenChange) * 0.05;
            int steps = 1000;
            double stepSize = aMax / steps;

            for (double a = aMin; a <= aMax; a += stepSize)
            {
                // Step 1: Your front-run trade
                // Swap 'a' tokenA for tokenB using the pool's current reserves.
                double tokenBBought = GetAmountOut(a, Convert.ToDouble(trade.SourceTokenChange), Convert.ToDouble(trade.DestinationTokenChange), feeRate);
                double X1 = Convert.ToDouble(trade.SourceTokenChange) + a;
                double Y1 = Convert.ToDouble(trade.DestinationTokenChange) - tokenBBought;

                // Step 2: Victim's trade
                // Victim swaps 'v' tokenA for tokenB using the updated reserves.
                double victimTokenB = GetAmountOut(Convert.ToDouble(trade.AmountIn), X1, Y1, feeRate);
                double X2 = X1 + Convert.ToDouble(trade.AmountIn);
                double Y2 = Y1 - victimTokenB;

                // Step 3: Your back-run trade
                // You now sell the tokenB you bought to get tokenA.
                // For this trade, tokenB is the input and the roles of reserves are reversed.
                double tokenAReceived = GetAmountOut(tokenBBought, Y2, X2, feeRate);

                // Calculate profit in tokenA (subtract your initial input and gas fees)
                double profit = tokenAReceived - a - gasFees;

                if (profit > bestProfit)
                {
                    bestProfit = profit;
                    bestA = a;
                }
            }

            return bestA;
        }

        public async Task<(string, string)> GetReserveAccounts(string liquidityPoolAddress)
        {
            var requestPayload = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "getAccountInfo",
                @params = new object[]
                {
                    liquidityPoolAddress,
                    new { encoding = "base64" }
                }
            };

            string jsonRequest = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await httpClient.PostAsync(SolanaRpcUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to fetch account info: {response.StatusCode}");
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            JsonDocument jsonDocument = JsonDocument.Parse(jsonResponse);

            // Extract the base64-encoded data
            string base64Data = jsonDocument
                .RootElement
                .GetProperty("result")
                .GetProperty("value")
                .GetProperty("data")[0]
                .GetString();

            byte[] decodedData = Convert.FromBase64String(base64Data);

            // Extract Reserve A & B Token Accounts from the Raydium AMM Pool data
            string reserveATokenAccount = ConvertPublicKey(decodedData, 8);  // Offset for Reserve A
            string reserveBTokenAccount = ConvertPublicKey(decodedData, 40); // Offset for Reserve B

            return (reserveATokenAccount, reserveBTokenAccount);
        }

        public static string ConvertPublicKey(byte[] data, int offset)
        {
            byte[] pubkeyBytes = new byte[32];
            Array.Copy(data, offset, pubkeyBytes, 0, 32);
            return EncodeBase58(pubkeyBytes);
        }

        private static string EncodeBase58(byte[] input)
        {
            const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
            var digits = new int[input.Length * 2];
            int length = 0;

            foreach (byte t in input)
            {
                int carry = t;
                for (int j = 0; j < length; j++)
                {
                    carry += digits[j] << 8;
                    digits[j] = carry % 58;
                    carry /= 58;
                }

                while (carry > 0)
                {
                    digits[length++] = carry % 58;
                    carry /= 58;
                }
            }

            string result = "";
            for (int i = 0; i < input.Length && input[i] == 0; i++)
                result += Alphabet[0];

            for (int i = length - 1; i >= 0; i--)
                result += Alphabet[digits[i]];

            return result;
        }

        #region -- Private Methods --

        /// <summary>
        /// Fetch token account balance
        /// </summary>
        private async Task<decimal> GetTokenBalance(string tokenAccountAddress)
        {
            var response = await _rpcClient.GetTokenAccountBalanceAsync(new PublicKey(tokenAccountAddress));
            return response.WasSuccessful ? decimal.Parse(response.Result.Value.UiAmountString) : 0;
        }

        /// <summary>
        /// Fetch total LP token supply
        /// </summary>
        private async Task<decimal> GetTokenSupply(string tokenMintAddress)
        {
            var response = await _rpcClient.GetTokenSupplyAsync(new PublicKey(tokenMintAddress));
            return response.WasSuccessful ? decimal.Parse(response.Result.Value.UiAmountString) : 0;
        }

        /// <summary>
        /// Fetch token price from CoinGecko
        /// </summary>
        static async Task<decimal> GetTokenPrice(string tokenId)
        {
            using HttpClient client = new HttpClient();
            string url = $"https://api.coingecko.com/api/v3/simple/price?ids={tokenId}&vs_currencies=usd";
            var response = await client.GetStringAsync(url);
            var priceData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(response);
            return priceData.GetProperty(tokenId).GetProperty("usd").GetDecimal();
        }

        /// <summary>
        /// Fetches the token account balance using RPC.
        /// </summary>
        /// <param name="trade">TradeData</param>
        /// <returns>token account balance</returns>
        private async Task<RequestResult<ResponseValue<TokenBalance>>> GetTokenAccountBalance(TradeData trade)
        {
            RequestResult<ResponseValue<TokenBalance>> bestResponse = null;
            decimal bestBalance = 0;

            foreach (var address in trade.LiquidityPoolAddress)
            {
                try
                {
                    var balanceResponse = await _rpcClient.GetTokenAccountBalanceAsync(address);
                    if (balanceResponse.WasSuccessful && balanceResponse.Result?.Value != null)
                    {
                        if (decimal.TryParse(balanceResponse.Result.Value.Amount, out decimal currentBalance))
                        {
                            // Select the account with the highest balance.
                            if (currentBalance > bestBalance)
                            {
                                bestBalance = currentBalance;
                                bestResponse = balanceResponse;
                            }
                        }
                    }
                }
                catch
                {
                    // Optionally log exception details here.
                }
            }
            return bestResponse;
        }


        /// <summary>
        /// Extracts the message bytes from a full transaction byte array.
        /// Assumes the first byte is the signature count, and each signature is 64 bytes.
        /// </summary>
        private byte[] ExtractMessageFromTransaction(byte[] txBytes)
        {
            // Get the signature count from the first byte.
            int signatureCount = txBytes[0];
            int signatureBytesLength = 1 + (signatureCount * 64);
            if (txBytes.Length < signatureBytesLength)
                throw new Exception("Transaction data is invalid or incomplete.");

            byte[] messageBytes = new byte[txBytes.Length - signatureBytesLength];
            Array.Copy(txBytes, signatureBytesLength, messageBytes, 0, messageBytes.Length);
            return messageBytes;
        }

        /// <summary>
        /// Computes the swap output using the constant product formula.
        /// For a given input amount, after fee deduction, the output is:
        ///   output = (amountInAfterFee * reserveOut) / (reserveIn + amountInAfterFee)
        /// </summary>
        /// <param name="amountIn">The input token amount.</param>
        /// <param name="reserveIn">The reserve for the input token.</param>
        /// <param name="reserveOut">The reserve for the output token.</param>
        /// <param name="feeRate">The swap fee rate (e.g. 0.003 for 0.3%).</param>
        /// <returns>The output token amount.</returns>
        public static double GetAmountOut(double amountIn, double reserveIn, double reserveOut, double feeRate)
        {
            double amountInAfterFee = amountIn * (1 - feeRate);
            return (amountInAfterFee * reserveOut) / (reserveIn + amountInAfterFee);
        }

        #endregion
    }
}
