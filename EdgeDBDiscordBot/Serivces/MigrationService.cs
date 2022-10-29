using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using EdgeDB;
using EdgeDBDiscordBot.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdgeDBDiscordBot.Serivces
{
    public class MigrationService
    {
        public const string MIGRATION_DIR = "./user-migrations";
        public const string EDGEDB_INSTANCE = "edgedb_discord";
        public readonly List<User> ActiveMigratingUsers = new();
        
        private readonly UserClientProvider _clientProvider;
        private readonly EdgeDBClient _edgedb;

        public MigrationService(UserClientProvider clientProvider, EdgeDBClient edgedb)
        {
            _clientProvider = clientProvider;
            _edgedb = edgedb;
        }

        public string GetMigrationFilePath(ulong userId, string migrationNumber)
        {
            return Path.Combine(MIGRATION_DIR, $"{userId}", $"migrations", $"{migrationNumber}.edgeql");
        }

        public async Task<string> CreateAndApplyUserMigrationAsync(User user, string schema, Func<string, Task<string?>> expHandler, CancellationToken token = default)
        {
            ActiveMigratingUsers.Add(user);
            try
            {
                var migration = new UserSchema
                {
                    Schema = schema,
                    CreatedAt = DateTimeOffset.Now
                };

                await _clientProvider.EnsureDatabaseExistsForUserAsync(user.UserId!, token);

                var schemaDir = Path.Combine(MIGRATION_DIR, $"{user.UserId}");
                Directory.CreateDirectory(schemaDir);
                File.WriteAllText(Path.Combine(schemaDir, "default.esdl"), schema);

                List<string> results = new();

                var process = CliUtils.ExecuteEdgeDBCommand($"-I {EDGEDB_INSTANCE} -d u{user.UserId} migration create --schema-dir {schemaDir}");

                process.OutputDataReceived += async (o, d) =>
                {
                    if (d.Data is null)
                        return;

                    if (d.Data.StartsWith("did you"))
                        process.StandardInput.WriteLine("y");
                    else if (d.Data.StartsWith("Please specify"))
                    {
                        var result = await expHandler.Invoke(d.Data);

                        if (result == null)
                            process.Kill();

                        process.StandardInput.WriteLine(result);
                    }
                    else
                        results.Add(d.Data);
                };
                process.ErrorDataReceived += (o, d) =>
                {
                    if (d.Data is not null)
                        results.Add(d.Data);
                };

                await process.WaitForExitAsync();

                var result = await Cli.Wrap("edgedb")
                    .WithValidation(CommandResultValidation.None)
                    .WithWorkingDirectory(Environment.CurrentDirectory)
                    .WithArguments($"-I {EDGEDB_INSTANCE} -d u{user.UserId} migrate --quiet --schema-dir {schemaDir}")
                    .ExecuteBufferedAsync(token);

                if (result.ExitCode != 0)
                    throw new Exception($"{result.StandardOutput}\n{result.StandardError}");

                var id = user.UserId;
                await QueryBuilder
                    .Update<User>(old => new User
                    {
                        SchemaHistory = EdgeQL.AddLink(QueryBuilder.Insert(migration, false))
                    })
                    .Filter(x => x.UserId == id)
                    .ExecuteAsync(_edgedb, token: token);

                return string.Join("\n", results) + "\n" + result.StandardOutput;
            }
            finally
            {
                ActiveMigratingUsers.Remove(user);
            }
        }
    }
}
