using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using EdgeDBDiscordBot.Serivces;
using EdgeDBDiscordBot.Utils;
using Fergun.Interactive;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EdgeDBDiscordBot.Modules
{
    [Group("schema", "View and edit schemas")]
    public class SchemaModule : InteractionModuleBase<SocketInteractionContext>
    {
        public UserSchemaService UserSchemaService { get; }
        public UserClientProvider ClientProvider { get; }
        public MigrationService MigrationService { get; }
        public InteractiveService InteractiveService { get; }

        public SchemaModule(UserSchemaService schemaService, 
            UserClientProvider clientProvider,
            MigrationService migrationService,
            InteractiveService interactiveService)
        {
            InteractiveService = interactiveService;
            UserSchemaService = schemaService;
            ClientProvider = clientProvider;
            MigrationService = migrationService;
        }

        [SlashCommand("view", "Get the current user-specific schema")]
        public async Task SchemaViewAsync(
            [Summary("user", "The user whos schema to view")]
            IUser? target = null)
        {
            await DeferAsync();

            target ??= Context.User;
            
            var user = await UserSchemaService.GetOrCreateUserAsync(target.Id).ConfigureAwait(false);

            var schema = user.CurrentSchema?.Schema ?? "module default {\n\n}";

            var coloredSchema = EdgeDBColorer.ColorSchemaOrQuery(schema);

            if(schema.Length + 10 >= 4096)
            {
                using (var ms = new MemoryStream())
                {
                    ms.Write(Encoding.UTF8.GetBytes(schema));
                    await FollowupWithFileAsync(new FileAttachment(ms, "schema.esdl", $"{target.Username}'s schema"));
                }
            }
            else
            {
                var embed = new EmbedBuilder()
                    .WithTitle($"{target.Username}'s schema")
                    .WithDescription($"```ansi\n{coloredSchema}```")
                    .WithColor(Color.Blue);
                await FollowupAsync(embed: embed.Build());
            }
        }

        [SlashCommand("edit", "Edit the current schema")]
        public async Task SchemaEditAsync(
            [Summary("schema", "The file containing the schema.")]
            Attachment? schemaFile = null)
        {
            var user = await UserSchemaService.GetOrCreateUserAsync(Context.User.Id);

            if(schemaFile == null)
            {
                var modal = new ModalBuilder()
                    .WithTitle("Edit Schema")
                    .WithCustomId("schema-modal")
                    .AddTextInput(new TextInputBuilder()
                        .WithLabel("Schema")
                        .WithCustomId("schema-text")
                        .WithValue(user.CurrentSchema?.Schema ?? "module default {\n\n}")
                        .WithStyle(TextInputStyle.Paragraph)
                     ).Build();

                await RespondWithModalAsync(modal).ConfigureAwait(false);
                return;
            }
        }

        [ModalInteraction("schema-modal", true)]
        public async Task EditModalSubmitAsync(EditSchemaModal schemaModal)
        {
            await RespondAsync("Applying migration...", ephemeral: true);

            var user = await UserSchemaService.GetOrCreateUserAsync(Context.User.Id);

            var result = await MigrationService.CreateAndApplyUserMigrationAsync(user, schemaModal.Schema!, async (question) =>
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Migration conflict")
                    .WithDescription(question)
                    .WithColor(Color.Blue);

                var buttons = new ComponentBuilder()
                    .WithButton("Submit response", "migration-submit", ButtonStyle.Success)
                    .WithButton("Cancel migration", "migration-cancel", ButtonStyle.Danger);

                await Context.Interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = null;
                    x.Embed = embed.Build();
                    x.Components = buttons.Build();
                });

                var interaction = await InteractiveService.NextInteractionAsync(x => 
                    x.User.Id == Context.User.Id && 
                    x is SocketMessageComponent componentInteraction && 
                    componentInteraction.Data.CustomId is "migration-submit" or "migration-cancel");

                if (!interaction.IsSuccess)
                    return null;

                if (interaction.Value is not SocketMessageComponent messageComponent)
                    throw new Exception("This is totally incorrect");

                if (messageComponent.Data.CustomId == "migration-cancel")
                {
                    await Context.Interaction.ModifyOriginalResponseAsync(x =>
                    {
                        x.Content = "Migration cancelled.";
                        x.Embeds = null;
                        x.Components = null;
                    });
                    return null;
                }

                var modalResponse = await InteractiveService.NextInteractionAsync(x =>
                    x.User.Id == Context.User.Id &&
                    x is SocketModal socketModal &&
                    socketModal.Data.CustomId == "migration-conflict"
                );

                if (!modalResponse.IsSuccess)
                    return null;

                if (modalResponse.Value is not SocketModal socketModal)
                    throw new Exception("This is also totally incorrect");

                await socketModal.DeferAsync();

                await Context.Interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Components = null;
                    x.Embed = null;
                    x.Content = "Applying migration...";
                });

                return socketModal.Data.Components.First().Value;
            });

            var migrationInfo = Regex.Match(result, @"migrations\\(\d+?)\..*?id:.*(.{54})$");

            var embed = new EmbedBuilder()
                .WithTitle("Sucessfully applied migration!")
                .AddField("Migration Number", migrationInfo.Groups[1].Value)
                .AddField("Migration ID", migrationInfo.Groups[2].Value)
                .WithColor(Color.Green);

            var components = new ComponentBuilder()
                .WithButton("View migration file", $"mf{Context.User.Id}-{migrationInfo.Groups[1].Value}");

            await Context.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = null;
                x.Components = components.Build();
                x.Embed = embed.Build();
            });
            
        }

        [ModalInteraction("migration-conflict", true)]
        public async Task HandleMigrationConflictAsync(MigrationConflictModal? modal)
        {
            await Task.Delay(500);

            if (Context.Interaction.HasResponded)
                return;

            await RespondAsync("The current migration pipeline has been killed. Please rerun the `/schema edit` command", ephemeral: true);

        }

        [ComponentInteraction("migration-submit", true)]
        public Task OpenMigrationModalAsync()
        {
            return RespondWithModalAsync(new ModalBuilder()
                    .WithTitle("Migration conflict")
                    .WithCustomId("migration-conflict")
                    .AddTextInput(new TextInputBuilder()
                        .WithStyle(TextInputStyle.Paragraph)
                        .WithLabel("Migration resolver")
                        .WithCustomId("migration-resolver")
                    ).Build());
        }

        [ComponentInteraction("mf*-*", true)]
        public async Task ViewMigrationAsync(string rawUserId, string migrationNumber)
        {
            ulong userId = ulong.Parse(rawUserId);

            if(Context.User.Id != userId)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Insufficient permissions")
                    .WithDescription($"Only <@{rawUserId}> can view their migration files")
                    .WithColor(Color.Red);
                await RespondAsync(embed: embed.Build(), ephemeral: true);
                return;
            }

            var migrationFile = MigrationService.GetMigrationFilePath(userId, migrationNumber);

            if(!File.Exists(migrationFile))
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Failed to find migration")
                    .WithDescription($"The migration `{migrationNumber}.edgeql` couldn't be found!")
                    .WithColor(Color.Red);
                await RespondAsync(embed: embed.Build(), ephemeral: true);    
            }

            await RespondWithFileAsync(new FileAttachment(migrationFile, $"{migrationNumber}.edgeql.txt", $"The migration {migrationNumber}"), ephemeral: true);

        }
    }

    public class EditSchemaModal : IModal
    {
        public string Title => "Edit Schema";

        [InputLabel("Schema")]
        [ModalTextInput("schema-text", TextInputStyle.Paragraph)]
        public string? Schema { get; set; }
    }

    public class MigrationConflictModal : IModal
    {
        public string Title => "";
    }
}
