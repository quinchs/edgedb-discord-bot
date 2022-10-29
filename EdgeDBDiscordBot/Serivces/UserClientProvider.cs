using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EdgeDB;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EdgeDBDiscordBot.Serivces
{
    public class UserClientProvider
    {
        private readonly ConcurrentDictionary<ulong, EdgeDBBinaryClient> _clients;
        private readonly EdgeDBConnection _defaultConnection;
        private readonly SemaphoreSlim _semaphore;
        private readonly ILoggerFactory _logFactory;
        private ConcurrentBag<string> _databases;
        private bool _isInitialized;

        public UserClientProvider(ILoggerFactory loggerFactory)
        {
            _clients = new();
            _semaphore = new(1, 1);
            _logFactory = loggerFactory;
            _defaultConnection = EdgeDBConnection.ResolveConnection();
            _databases = new();
        }

        private ValueTask WaitForInitializationAsync(CancellationToken token = default)
        {
            if (!_isInitialized)
                return new ValueTask(InitializeAsync(token));
            return ValueTask.CompletedTask;
        }

        public async ValueTask EnsureDatabaseExistsForUserAsync(string userId, CancellationToken token = default)
        {
            await WaitForInitializationAsync(token).ConfigureAwait(false);
            if (!_databases.Contains($"u{userId}"))
                await CreateDatabaseAsync(userId, token).ConfigureAwait(false);
        }

        private async Task CreateDatabaseAsync(string userId, CancellationToken token = default)
        {
            await using var client = new EdgeDBTcpClient(_defaultConnection, new() { Logger = _logFactory.CreateLogger("Initialization client") });
            await client.ConnectAsync(token);
            await client.ExecuteAsync($"create database u{userId}", capabilities: Capabilities.DDL, token: token);
        }

        public async Task<EdgeDBBinaryClient> GetClientForUserAsync(ulong userId, CancellationToken token = default)
        {
            await _semaphore.WaitAsync(token).ConfigureAwait(false);
            await WaitForInitializationAsync(token).ConfigureAwait(false);

            try
            {
                if (_clients.TryGetValue(userId, out var client))
                {
                    return client;
                }

                var connection = new EdgeDBConnection
                {
                    Database = $"u{userId}",
                    Hostname = _defaultConnection.Hostname,
                    Password = _defaultConnection.Password,
                    Port = _defaultConnection.Port,
                    TLSCertificateAuthority = _defaultConnection.TLSCertificateAuthority,
                    TLSSecurity = _defaultConnection.TLSSecurity,
                    Username = _defaultConnection.Username
                };

                client = new EdgeDBTcpClient(connection, new EdgeDBConfig
                {
                    Logger = _logFactory.CreateLogger($"User {userId}"),
                    ExplicitObjectIds = false
                });

                await EnsureDatabaseExistsForUserAsync(userId.ToString(), token: token);

                await client.ConnectAsync(token).ConfigureAwait(false);

                return _clients[userId] = client;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await using var client = new EdgeDBTcpClient(_defaultConnection, new() { Logger = _logFactory.CreateLogger("Initialization client") });
            await client.ConnectAsync(cancellationToken);

            _databases = new(
                (await client.QueryAsync<string>("SELECT (SELECT sys::Database FILTER NOT .builtin).name", token: cancellationToken).ConfigureAwait(false))!
            );
            _isInitialized = true;
        }
    }
}
