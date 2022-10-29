using Discord;
using Discord.Interactions;
using EdgeDB;
using EdgeDBDiscordBot.Serivces;
using EdgeDBDiscordBot.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdgeDBDiscordBot.Modules
{
    public enum QueryFormat
    {
        Json,
        Binary,
    }
    public class QueryModule : InteractionModuleBase<SocketInteractionContext>
    {
        public UserClientProvider ClientProvider { get; }
        public QueryModule(UserClientProvider clientProvider)
        {
            ClientProvider = clientProvider;
        }

        [SlashCommand("query", "Executes a raw EdgeQL query")]
        public async Task ExecuteRawQueryAsync(
            [Summary("edgeql", "The EdgeQL query to execute.")]string query,
            [Summary("format", "The format of the query results.")]QueryFormat format = QueryFormat.Binary)
        {
            await DeferAsync(false);
            
            var client = await ClientProvider.GetClientForUserAsync(Context.User.Id);

            FormattedQueryResult result = null!;
            var sw = Stopwatch.StartNew();
            try
            {
                result = await QueryResultFormatter.ExecuteAndFormatQueryAsync(query, client, format).ConfigureAwait(false);
                sw.Stop();
            }
            catch(EdgeDBException x) when (x.InnerException is EdgeDBErrorException error)
            {
                sw.Stop();
                var failEmbed = new EmbedBuilder()
                    .WithTitle("Query failed")
                    .WithDescription($"{x.Message}: {error.Message}\n\n```ansi\n{QueryErrorFormatter.FormatError(query, error)}```")
                    .AddField("Execution Time", $"{sw.ElapsedMilliseconds}ms")
                    .WithColor(Color.Red);

                if (error.Hint is not null)
                    failEmbed.AddField("Hint", error.Hint);

                await FollowupAsync(embed: failEmbed.Build(), ephemeral: true);
                return;
            }
            catch(Exception x)
            {
                var failEmbed = new EmbedBuilder()
                   .WithTitle("Query failed")
                   .WithDescription("An internal error has occured within the bot.")
                   .AddField("Error",x)
                   .WithColor(Color.Red);

                await FollowupAsync(embed: failEmbed.Build(), ephemeral: true);
            }

            var formatted = result.Formatted.Length > 4000
                ? result.Formatted[..4000] + $"...\n{result.Formatted.Length - 4000} more."
                : result.Formatted;

            var embed = new EmbedBuilder()
                .WithTitle("Result")
                .WithDescription($"```{(format == QueryFormat.Binary ? "ansi" : "json")}\n{formatted}```")
                .WithColor(Color.Green)
                .AddField("Execution Time", $"{sw.ElapsedMilliseconds}ms");

            if (formatted.Length > 4000)
                await FollowupWithFileAsync(new FileAttachment(new MemoryStream(Encoding.UTF8.GetBytes(result.RawResult)), "result.edgeql.txt", "The result of your query"), embed: embed.Build());
            else
                await FollowupAsync(embed: embed.Build());
        }

        [SlashCommand("select", "Preforms a select query with the given parameters")]
        public Task Select(
            [Summary("exp", "The expression to select")] string expression,
            [Summary("filter", "The optional filter clause")] string? filter = null,
            [Summary("order_by", "The optional order by clause")] string? orderBy = null,
            [Summary("offset", "The optional offset by clause")] long? offset = null,
            [Summary("limit", "The optional limit by clause")] long? limit = null,
            [Summary("format", "The format of the query results.")] QueryFormat format = QueryFormat.Binary)
        {
            var query = "select " + expression;

            if (filter != null)
                query += " filter " + filter;
            if (orderBy != null)
                query += " order by " + orderBy;
            if (offset != null)
                query += " offset " + offset;
            if (limit != null)
                query += " limit " + limit;

            return ExecuteRawQueryAsync(query, format);
        }
    }
}
