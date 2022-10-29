using Discord.Interactions;
using Discord.WebSocket;
using EdgeDBDiscordBot.Serivces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdgeDBDiscordBot.Modules
{
    public class TestModule : InteractionModuleBase<SocketInteractionContext>
    {
        public UserClientProvider ClientProvider { get; set; }

        public TestModule(UserClientProvider clientProvider)
        {
            ClientProvider = clientProvider;
        }

        [SlashCommand("test", "test some things")]
        public async Task TestAsync()
        {
            await DeferAsync();

            var timeoutToken = new CancellationTokenSource();
            timeoutToken.CancelAfter(5000);

            try
            {
                var client = await ClientProvider.GetClientForUserAsync(Context.User.Id, timeoutToken.Token);
            }
            catch(Exception x)
            {
                Console.WriteLine(x);
            }
        }
    }
}
