using MevBot.Service.Data;
using MevBot.Service.Trader.extensions;
using MevBot.Service.Trader.rules;
using MevBot.Service.Trader.services.interfaces;
using Solnet.Programs.Utilities;
using StackExchange.Redis;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;


namespace MevBot.Service.Trader
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _redisDb;
        private readonly ITradingService _tradingService;
        private readonly IServiceProvider _serviceProvider;

        private readonly string _redisBuyQueue = "solana_buy_queue";
        //private readonly string _redisTradeQueue = "solana_trade_queue";
        private readonly string _redisConnectionString;
        private readonly string _rpcUrl;
        private readonly string _wsUrl;
        private readonly string _splTokenAddress;
        private readonly string _walletAddress;

        public Worker(ILogger<Worker> logger, 
                      IConfiguration configuration, 
                      IServiceProvider serviceProvider, 
                      ITradingService tradingService)
        {
            _logger = logger;
            _configuration = configuration;
            _tradingService = tradingService;
            _serviceProvider = serviceProvider;

            _redisConnectionString = _configuration.GetValue<string>("Redis:REDIS_URL") ?? string.Empty;
            _rpcUrl = _configuration.GetValue<string>("Solana:RPC_URL") ?? string.Empty;
            _wsUrl = _configuration.GetValue<string>("Solana:WsUrl") ?? string.Empty;
            _splTokenAddress = _configuration.GetValue<string>("Solana:SPL_TOKEN_ADDRESS") ?? string.Empty;

            // Connect to Redis.
            var options = ConfigurationOptions.Parse(_redisConnectionString);
            _redis = ConnectionMultiplexer.Connect(options);
            _redisDb = _redis.GetDatabase();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{time} - Starting Solana MEV Bot Trader", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Try to pop a message from the Redis queue.
                // RedisValue message = await _redisDb.ListRightPopAsync(_redisBuyQueue);
                RedisValue message = await _redisDb.ListGetByIndexAsync(_redisBuyQueue, 0);

                if (!message.IsNullOrEmpty)
                {
                    _logger.LogInformation("{time} - Processing message", DateTimeOffset.Now);

                    // Deserialize the message.
                    var solanaTransaction = JsonSerializer.Deserialize<SolanaTransaction>(message);
                    if (solanaTransaction != null)
                    {
                        // Get the trade data from transaction
                        var victimTrade = solanaTransaction.GetTradeData(_splTokenAddress);
                        
                        var engine = new RuleEngine(_serviceProvider);

                        engine.EvaluateTrade(victimTrade);

                        // execute trade
                        if (victimTrade.SequenceAndTimingPassed)
                        {

                            // Define the amount in SOL
                            ulong amountLamports = SolHelper.ConvertToLamports(0.1m); // 0.1 SOL

                            // await _tradingService.Buy(amountLamports, "recipient_public_key");

                            // await _tradingService.Sell(amountLamports, buyOrder.ExpectedAmountOut, 10, "buyer_public_key", "token_mint_address");

                        }
                    }
                }

                // Add a small delay to prevent overloading Redis
                await Task.Delay(100000000, stoppingToken);
            }
        }
    }
}
