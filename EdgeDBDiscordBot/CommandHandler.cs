using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace EdgeDBDiscordBot
{
    public class CommandHandler : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactionService;
        private readonly IServiceProvider _serivces;

        public CommandHandler(DiscordSocketClient client, InteractionService interactionService, IServiceProvider services)
        {
            _client = client;
            _interactionService = interactionService;
            _serivces = services;

            _client.InteractionCreated += _client_InteractionCreatedAsync;
        }

        private async Task _client_InteractionCreatedAsync(SocketInteraction arg)
        {
            var context = new SocketInteractionContext(_client, arg);
            await _interactionService.ExecuteCommandAsync(context, _serivces);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource();
            var readyCallback = async () =>
            {
                await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _serivces);

#if DEBUG
                await _interactionService.RegisterCommandsToGuildAsync(998593894906343496, true);
#else
                await _interactionService.RegisterCommandsGloballyAsync(true);
#endif
                tcs.TrySetResult();

            };
            _client.Ready += readyCallback;

            await tcs.Task.ConfigureAwait(false);
            
            _client.Ready -= readyCallback;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.InteractionCreated -= _client_InteractionCreatedAsync;
            return Task.CompletedTask;
        }
    }
}
