using EdgeDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdgeDBDiscordBot.Serivces
{
    public class UserSchemaService
    {
        private readonly UserClientProvider _clientProvider;
        private readonly EdgeDBClient _edgedb;
        public UserSchemaService(UserClientProvider clientProvider, EdgeDBClient edgedb)
        {
            _clientProvider = clientProvider;
            _edgedb = edgedb;
        }

        public async Task<User> GetOrCreateUserAsync(ulong userId, CancellationToken token = default)
        {
            var strId = userId.ToString();
            return (await QueryBuilder
                .Insert((_) => new User { UserId = strId })
                .UnlessConflictOn(x => x.UserId)
                .ElseReturn()
                .ExecuteAsync(_edgedb, token: token))!;
        }
    }
}
