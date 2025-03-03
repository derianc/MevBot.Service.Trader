using MevBot.Service.Trader.services.interfaces;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Wallet;

namespace MevBot.Service.Trader.services
{
    public class BuyService : IBuyService
    {
        private readonly IConfiguration _configuration;

        private readonly string _rpcUrl;
        private readonly IRpcClient _rpcClient;

        private readonly string _walletAddress;
        private readonly string _walletPrivateKey;
        private readonly Account _account;

        public BuyService(IConfiguration configuration)
        {
            _configuration = configuration;
            
            _walletAddress = _configuration.GetValue<string>("Solana:WALLET_ADDRESS") ?? string.Empty;
            _walletPrivateKey = _configuration.GetValue<string>("Solana:WALLET_PRIVATE_KEY") ?? string.Empty;
            _account = new Account(_walletPrivateKey, _walletAddress);
            
            _rpcUrl = _configuration.GetValue<string>("Solana:RpcUrl") ?? string.Empty;
            _rpcClient = ClientFactory.GetClient(_rpcUrl);
        }

        public async Task<bool> Buy(ulong amountLamports, string recipientPublicKey)
        {
            var transaction = new TransactionBuilder()
                .SetFeePayer(_account) // Sender pays fees
                .AddInstruction(SystemProgram.Transfer(
                    _account.PublicKey,
                    new PublicKey(recipientPublicKey),
                    amountLamports))
                .Build(_account);

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
    }
}
