using MevBot.Service.Data;
using Newtonsoft.Json.Linq;
using NRules.Fluent.Dsl;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Wallet;
using System.Text;
using System.Net.Http.Json;
using Solnet.Wallet.Bip39;

namespace MevBot.Service.Trader.rules
{
    public class _02_EstimatedGasFeeRule : Rule
    {
        private readonly string _wsUrl;
        private readonly string _rpcUrl;
        private readonly Wallet _wallet;
        private readonly IRpcClient _rpcClient;
        private readonly string _walletPrivateKey;
        private readonly IConfiguration _configuration;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _walletAddress;

        public _02_EstimatedGasFeeRule(IConfiguration configuration)
        {
            _configuration = configuration;
            _wsUrl = _configuration.GetValue<string>("Solana:WsUrl") ?? string.Empty;
            _rpcUrl = _configuration.GetValue<string>("Solana:RpcUrl") ?? string.Empty;
            _walletPrivateKey = _configuration.GetValue<string>("Solana:WALLET_PRIVATE_KEY") ?? string.Empty;
            _walletAddress = _configuration.GetValue<string>("Solana:WALLET_ADDRESS") ?? string.Empty;


            // Generate a new 12-word mnemonic (you can choose 15, 18, 21, or 24 words as well)
            var mnemonic = new Mnemonic(WordList.English, WordCount.Twelve);
            _wallet = new Wallet(mnemonic);

            _rpcClient = ClientFactory.GetClient(_rpcUrl);
        }

        public override void Define()
        {
            var trade = new TradeData();

            When()
                .Match<TradeData>(() => trade,
                    t => t.SlotNumber > 0,                          // Slot exists
                    t => t.ExpectedAmountOut >= t.MinimumReturn     // Valid slippage tolerance
                );

            Then()
                .Do(ctx => EvaluateGasFee(trade));
        }

        private async void EvaluateGasFee(TradeData trade)
        {
            // 1. Fetch recent block hash required for the transaction.
            var blockHashResponse = await _rpcClient.GetLatestBlockHashAsync();
            if (!blockHashResponse.WasSuccessful)
                throw new Exception("Failed to fetch recent block hash.");
            string recentBlockHash = blockHashResponse.Result.Value.Blockhash;
            Console.WriteLine($"Recent Block Hash: {recentBlockHash}");

            // 2. Convert the buy amount to lamports (assuming 6 decimals for USDC)
            // If trade.AmountToBuy is not set, use a dummy value (e.g., 10 USDC)
            decimal amountToBuy = trade.AmountToBuy > 0 ? trade.AmountToBuy : 10m;
            ulong amountLamports = (ulong)(amountToBuy * 1_000_000m);  // 1 USDC = 1_000_000 lamports
            Console.WriteLine($"Amount to Buy (lamports): {amountLamports}");

            // 3. Build a preliminary "buy" transaction.
            // For demonstration, we simulate a buy by issuing a transfer instruction to a dummy recipient.
            var transactionBuilder = new TransactionBuilder()
                .SetFeePayer(_wallet.Account)
                .SetRecentBlockHash(recentBlockHash)
                .AddInstruction(SystemProgram.Transfer(
                    _wallet.Account.PublicKey,
                    new PublicKey("11111111111111111111111111111111"),
                    amountLamports));

            // 4. Build the full transaction (returns a byte array).
            var txBytes = transactionBuilder.Build(_wallet.Account);

            // 5. Extract only the message portion from the transaction.
            // In Solana, the first byte is the signature count, and each signature is 64 bytes.
            byte[] messageBytes = ExtractMessageFromTransaction(txBytes);
            string serializedMessage = Convert.ToBase64String(messageBytes);
            Console.WriteLine($"Serialized Message: {serializedMessage}");

            // 6. Call the RPC client to get the fee for the transaction message.
            var feeResponse = await _rpcClient.GetFeeForMessageAsync(serializedMessage);
            if (!feeResponse.WasSuccessful || feeResponse.Result.Value == null)
                throw new Exception("Failed to get fee for message.");

            ulong feeLamports = feeResponse.Result.Value;
            Console.WriteLine($"Estimated Gas Fee (lamports): {feeLamports}");

            trade.GasFee = feeLamports;
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

    }
}