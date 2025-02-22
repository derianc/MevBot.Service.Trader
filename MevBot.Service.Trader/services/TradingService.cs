using MevBot.Service.Trader.services.interfaces;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Wallet;

namespace MevBot.Service.Trader.services
{
    public class TradingService : ITradingService
    {
        private readonly string _rpcUrl;
        private readonly string _walletPrivateKey;

        private readonly IConfiguration _configuration;
        
        private readonly Wallet _wallet;
        private readonly IRpcClient _rpcClient;

        public TradingService(IConfiguration configuration)
        {
            _configuration = configuration;
            _walletPrivateKey = _configuration.GetValue<string>("Solana:WALLET_PRIVATE_KEY") ?? string.Empty;
            _rpcUrl = _configuration.GetValue<string>("Solana:RPC_URL") ?? string.Empty;

            _wallet = new Wallet(_walletPrivateKey);
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

        public async Task<bool> Sell(ulong amountLamports, string buyerPublicKey, string tokenMintAddress)
        {
            // Get seller's associated token account
            PublicKey sellerTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                _wallet.Account.PublicKey,
                new PublicKey(tokenMintAddress)
            );

            // Get buyer's associated token account
            PublicKey buyerTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                new PublicKey(buyerPublicKey),
                new PublicKey(tokenMintAddress)
            );

            // Ensure buyer has an associated token account (otherwise create one)
            var buyerTokenAccountInfo = await _rpcClient.GetAccountInfoAsync(buyerTokenAccount);
            bool buyerAccountExists = buyerTokenAccountInfo.WasSuccessful && buyerTokenAccountInfo.Result != null;

            // Create the transaction builder
            var transactionBuilder = new TransactionBuilder()
                .SetFeePayer(_wallet.Account)
                .AddInstruction(TokenProgram.Transfer(
                    sellerTokenAccount, // Source (Seller's token account)
                    buyerTokenAccount,  // Destination (Buyer's token account)
                    amountLamports,     // Amount in smallest unit (e.g., lamports for SPL tokens)
                    _wallet.Account
                ));

            // If buyer's token account doesn't exist, create it
            if (!buyerAccountExists)
            {
                Console.WriteLine("Buyer does not have an associated token account. Creating one...");
                transactionBuilder.AddInstruction(AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                    _wallet.Account.PublicKey,
                    new PublicKey(buyerPublicKey),
                    new PublicKey(tokenMintAddress)
                ));
            }

            // Build and sign transaction
            var transaction = transactionBuilder.Build(_wallet.Account);

            // Send the transaction
            var sendResult = await _rpcClient.SendTransactionAsync(transaction);

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
    }
}
