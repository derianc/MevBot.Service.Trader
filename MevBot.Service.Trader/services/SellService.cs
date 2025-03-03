using MevBot.Service.Trader.services.interfaces;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Wallet;
using System.Net.Http.Json;

namespace MevBot.Service.Trader.services
{
    public class SellService : ISellService
    {

        private readonly IConfiguration _configuration;

        private readonly string _rpcUrl;
        private readonly IRpcClient _rpcClient;

        private readonly string _walletAddress;
        private readonly string _walletPrivateKey;
        private readonly Account _account;

        private static readonly HttpClient _httpClient = new HttpClient();

        public SellService(IConfiguration configuration)
        {
            _configuration = configuration;
         
            _walletAddress = _configuration.GetValue<string>("Solana:WALLET_ADDRESS") ?? string.Empty;
            _walletPrivateKey = _configuration.GetValue<string>("Solana:WALLET_PRIVATE_KEY") ?? string.Empty;
            _account = new Account(_walletPrivateKey, _walletAddress);
            
            _rpcUrl = _configuration.GetValue<string>("Solana:RpcUrl") ?? string.Empty;
            _rpcClient = ClientFactory.GetClient(_rpcUrl);
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
                    _account.PublicKey,
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
                    .SetFeePayer(_account)
                    // The ComputeBudget instruction is added first.
                    .AddInstruction(ComputeBudgetProgram.RequestUnits(1_400_000, gasFee))
                    .AddInstruction(TokenProgram.Transfer(
                        sellerTokenAccount,
                buyerTokenAccount,
                        amountLamports,
                        _account
                    ));
                if (!buyerAccountExists)
                {
                    finalBuilder.AddInstruction(AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                        _account.PublicKey,
                        new PublicKey(buyerPublicKey),
                        new PublicKey(tokenMintAddress)
                    ));
                }
                var finalTransaction = finalBuilder.Build(_account);

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


        #region  -- Private Methods --

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

        #endregion
    }
}
