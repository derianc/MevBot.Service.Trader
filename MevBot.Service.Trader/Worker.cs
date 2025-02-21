using MevBot.Service.Data;
using MevBot.Service.Trader.extensions;
using MevBot.Service.Trader.rules;
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

        private readonly string _redisBuyQueue = "solana_buy_queue";
        private readonly string _redisTradeQueue = "solana_trade_queue";
        private readonly string _redisConnectionString;
        private readonly string _rpcUrl;
        private readonly string _wsUrl;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _redisConnectionString = _configuration.GetValue<string>("Redis:REDIS_URL") ?? string.Empty;
            _rpcUrl = _configuration.GetValue<string>("Solana:RPC_URL") ?? string.Empty;
            _wsUrl = _configuration.GetValue<string>("Solana:WsUrl") ?? string.Empty;

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
                        var buyOrder = solanaTransaction.GetTradeData();
                        var transactionSignature = solanaTransaction?.@params?.result?.value?.signature;

                        //using (ClientWebSocket ws = new ClientWebSocket())
                        //{
                        //    await ws.ConnectAsync(new Uri(_wsUrl), stoppingToken);

                        //    string subscribeMessage = $@"
                        //    {{
                        //        ""jsonrpc"": ""2.0"",
                        //        ""id"": 1,
                        //        ""method"": ""signatureSubscribe"",
                        //        ""params"": [
                        //            {{ ""mentions"": [""{transactionSignature}""] }},
                        //            {{ ""commitment"": ""confirmed"" }}
                        //        ]
                        //    }}";

                        //    string messageJson = JsonSerializer.Serialize(subscribeMessage);
                        //    var messageBytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageJson));
                        //    await ws.SendAsync(messageBytes, WebSocketMessageType.Text, true, stoppingToken);

                        //    // Continuously receive and process messages.
                        //    while (ws.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
                        //    {
                        //        string completeMessage = await ReceiveFullMessageAsync(ws, stoppingToken);

                        //        if (completeMessage == null)
                        //        {
                        //            // If message is null, assume the connection closed.
                        //            _logger.LogWarning("{time} - Received null message, breaking out", DateTimeOffset.Now);
                        //            break;
                        //        }
                        //    }
                        //}

                        var engine = new RuleEngine();

                        // _logger.LogInformation("{time} - Begin trade evaluation", DateTimeOffset.Now);
                                                
                        
                        engine.EvaluateTrade(buyOrder);


                        // if trade is profitable, push to trade queue


                        // _logger.LogInformation("{time} - End trade evaluation", DateTimeOffset.Now);
                    }
                }
            }
        }

        private async Task<string> ReceiveFullMessageAsync(ClientWebSocket ws, CancellationToken stoppingToken)
        {
            var buffer = new byte[4096];
            using (var ms = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), stoppingToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogWarning("{time} - WebSocket closed by remote party", DateTimeOffset.Now);
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", stoppingToken);
                        return null;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(ms, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }
    }
}
