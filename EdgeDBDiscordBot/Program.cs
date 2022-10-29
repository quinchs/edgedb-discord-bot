using CliWrap;
using Discord.Addons.Hosting;
using Discord.WebSocket;
using EdgeDB;
using EdgeDBDiscordBot;
using EdgeDBDiscordBot.Serivces;
using EdgeDBDiscordBot.Utils;
using Fergun.Interactive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Text;

var test = EdgeDBColorer.ColorSchemaOrQuery("select <array<uuid>>'test'");

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} - {Level}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

using var host = Host.CreateDefaultBuilder()
    .ConfigureDiscordHost((context, config) =>
    {
        config.SocketConfig = new DiscordSocketConfig
        {
            LogLevel = Discord.LogSeverity.Debug,
            GatewayIntents = Discord.GatewayIntents.AllUnprivileged
        };

        config.Token = context.Configuration["token"];
    })
    .UseInteractionService((context, config) =>
    {
        config.LogLevel = Discord.LogSeverity.Info;
        config.UseCompiledLambda = true;
        config.DefaultRunMode = Discord.Interactions.RunMode.Async;
    })
    .ConfigureServices((services) =>
    {
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(dispose: true);
        });
        services.AddHostedService<CommandHandler>();
        services.AddSingleton<UserClientProvider>();
        services.AddSingleton<UserSchemaService>();
        services.AddSingleton<MigrationService>();
        services.AddSingleton<InteractiveService>();

        services.AddEdgeDB();

    }).Build();

await host.RunAsync();