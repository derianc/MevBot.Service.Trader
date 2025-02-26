using MevBot.Service.Trader.services.interfaces;
using System.Net.Http.Json;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Wallet;
using Solnet.Programs;
using Newtonsoft.Json.Linq;
using System.Text;
using Solnet.Wallet.Bip39;

namespace MevBot.Service.Trader.services
{
    public class TradingService : ITradingService
    {
        private readonly string _wsUrl;
        private readonly string _rpcUrl;
        private readonly string _walletAddress;
        private readonly string _walletPrivateKey;

        private readonly IConfiguration _configuration;
        
        private readonly Wallet _wallet;
        private readonly IRpcClient _rpcClient;

        private static readonly HttpClient _httpClient = new HttpClient();

        public TradingService(IConfiguration configuration)
        {
            _configuration = configuration;
            _walletAddress = _configuration.GetValue<string>("Solana:WALLET_ADDRESS") ?? string.Empty;
            _walletPrivateKey = _configuration.GetValue<string>("Solana:WALLET_PRIVATE_KEY") ?? string.Empty;
            _rpcUrl = _configuration.GetValue<string>("Solana:RpcUrl") ?? string.Empty;
            _wsUrl = _configuration.GetValue<string>("Solana:WsUrl") ?? string.Empty;

            // Generate a new 12-word mnemonic (you can choose 15, 18, 21, or 24 words as well)
            var mnemonic = new Mnemonic(WordList.English, WordCount.Twelve);
            _wallet = new Wallet(mnemonic);

            _rpcClient = ClientFactory.GetClient(_rpcUrl);
        }

        public async Task<bool> Buy(ulong amountLamports, string recipientPublicKey)
        {
            var transaction = new TransactionBuilder()
                .SetFeePayer(_wallet.Account) // Sender pays fees
                .AddInstruction(SystemProgram.Transfer(
                    _wallet.Account.PublicKey, 
                    new PublicKey(recipientPublicKey), 
                    amountLamports))
                .Build(_wallet.Account);

            // Send the transaction
            var sendResult = await _rpcClient.SendTransactionAsync(transaction);

            if (sendResult.WasSuccessful)
            {
                Console.WriteLine($"Transaction successful! TxHash: {sendResult.Result}");
                return true;
            }
            else
            {
                Console.WriteLine($"Transaction failed: {sendResult.Reason}");
                return false;
            }
        }

        public async Task<bool> Sell(decimal amountToSell, decimal expectedAmountOut, ulong gasFee, string buyerPublicKey, string tokenMintAddress)
        {
            try
            {
                Console.WriteLine("Fetching real-time token price for USDC...");
                decimal tokenPrice = await GetTokenPriceAsync("usd-coin");
                Console.WriteLine($"Current Token Price (USDC): ${tokenPrice}");

                // Convert the sale amount to lamports (assuming 6 decimals for USDC)
                ulong amountLamports = ConvertToLamports(amountToSell, 6);

                // Derive seller's and buyer's associated token accounts.
                PublicKey sellerTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                    _wallet.Account.PublicKey,
                    new PublicKey(tokenMintAddress)
                );
                PublicKey buyerTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                    new PublicKey(buyerPublicKey),
                    new PublicKey(tokenMintAddress)
                );

                // Ensure buyer has an associated token account (otherwise create one)
                var buyerTokenAccountInfo = await _rpcClient.GetAccountInfoAsync(buyerTokenAccount);
                bool buyerAccountExists = buyerTokenAccountInfo.WasSuccessful && buyerTokenAccountInfo.Result != null;

                // --- STEP 4: Build the final transaction, adding a ComputeBudget instruction to include
                // the gas fee as an extra tip for prioritization.
                var finalBuilder = new TransactionBuilder()
                    .SetFeePayer(_wallet.Account)
                    // The ComputeBudget instruction is added first.
                    .AddInstruction(ComputeBudgetProgram.RequestUnits(1_400_000, gasFee))
                    .AddInstruction(TokenProgram.Transfer(
                        sellerTokenAccount,
                        buyerTokenAccount,
                        amountLamports,
                        _wallet.Account
                    ));
                if (!buyerAccountExists)
                {
                    finalBuilder.AddInstruction(AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                        _wallet.Account.PublicKey,
                        new PublicKey(buyerPublicKey),
                        new PublicKey(tokenMintAddress)
                    ));
                }
                var finalTransaction = finalBuilder.Build(_wallet.Account);

                // --- STEP 5: Send the final transaction.
                var sendResult = await _rpcClient.SendTransactionAsync(finalTransaction);
                if (sendResult.WasSuccessful)
                {
                    Console.WriteLine($"Sell transaction successful! TxHash: {sendResult.Result}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Sell transaction failed: {sendResult.Reason}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Sell operation: {ex.Message}");
                return false;
            }
        }



        /// <summary>
        /// Fetches the real-time price of the token from CoinGecko.
        /// </summary>
        private async Task<decimal> GetTokenPriceAsync(string tokenId)
        {
            try
            {
                var url = $"https://api.coingecko.com/api/v3/simple/price?ids={tokenId}&vs_currencies=usd";
                var response = await _httpClient.GetFromJsonAsync<dynamic>(url);
                return (decimal)response[tokenId]["usd"];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching token price: {ex.Message}");
                return 0m;
            }
        }

        /// <summary>
        /// Converts token amount to Lamports based on decimal places.
        /// </summary>
        private ulong ConvertToLamports(decimal amount, int decimals)
        {
            return (ulong)(amount * (decimal)Math.Pow(10, decimals));
        }
    }
}
