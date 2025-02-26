using MevBot.Service.Trader;
using MevBot.Service.Trader.rules;
using MevBot.Service.Trader.services;
using MevBot.Service.Trader.services.interfaces;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ITradingService, TradingService>();
        services.AddTransient<_02_EstimatedGasFeeRule>();
        //services.AddTransient<_03_DexLiquidityRule>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
