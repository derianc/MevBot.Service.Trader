using MevBot.Service.Trader.models;
using StackExchange.Redis;
using System.Text.Json;


namespace MevBot.Service.Trader
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _redisDb;

        private readonly string _redisBuyQueue = "solana_buy_queue";
        private readonly string _redisTradeQueue = "solana_trade_queue";
        private readonly string _redisConnectionString;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _redisConnectionString = _configuration.GetValue<string>("Redis:REDIS_URL") ?? string.Empty;

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
                RedisValue message = await _redisDb.ListRightPopAsync(_redisBuyQueue);

                if (!message.IsNullOrEmpty)
                {
                    // Deserialize the message.
                    string buyOrderLog = string.Empty;
                    var buyOrder = JsonSerializer.Deserialize<SolanaTransaction>(message);
                    
                    if (buyOrder?.@params?.result?.value?.logs != null)
                        buyOrderLog = string.Concat(buyOrder?.@params?.result?.value?.logs?.ToArray());

                    if (buyOrderLog != null)
                    {
                        var parts = buyOrderLog.Split(new[] { ':', ',' }, StringSplitOptions.RemoveEmptyEntries)
                                               .Select(p => p.Trim())
                                               .ToArray();

                        decimal amount = 0;
                        string tokenIn = string.Empty;
                        string tokenOut = string.Empty;
                        decimal priceImpact = 0;
                        string tokenAddress = string.Empty;

                        foreach (var part in parts.Skip(1))
                        {
                            var kv = part.Split('=');
                            if (kv.Length == 2)
                            {
                                var key = kv[0].Trim().ToLower();
                                var value = kv[1].Trim();
                                switch (key)
                                {
                                    case "amount":
                                        amount = decimal.Parse(value);
                                        break;
                                    case "tokenin":
                                        tokenIn = value;
                                        break;
                                    case "tokenout":
                                        tokenOut = value;
                                        break;
                                    case "priceimpact":
                                        priceImpact = decimal.Parse(value);
                                        break;
                                    case "tokenaddress":
                                        tokenAddress = value;
                                        break;
                                }
                            }
                        }

                        if (amount > 0 || !string.IsNullOrEmpty(tokenIn) || !string.IsNullOrEmpty(tokenOut) || priceImpact > 0 || !string.IsNullOrEmpty(tokenAddress))
                            await _redisDb.ListLeftPushAsync(_redisTradeQueue, message);

                        var victimTrade = new TradeData
                        {
                            Amount = amount,
                            TokenIn = tokenIn,
                            TokenOut = tokenOut,
                            PriceImpact = priceImpact,
                            TokenAddress = tokenAddress
                        };
                    }
                }
            }
        }
    }
}
