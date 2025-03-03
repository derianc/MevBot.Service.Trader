using MevBot.Service.Trader;
using MevBot.Service.Trader.rules;
using MevBot.Service.Trader.services;
using MevBot.Service.Trader.services.interfaces;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<IBuyService, BuyService>();
        services.AddSingleton<ISellService, SellService>();
        services.AddSingleton<ILookupService, LookupService>();
        services.AddTransient<_03_DexLiquidityRule>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
