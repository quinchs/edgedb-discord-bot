using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdgeDBDiscordBot
{
    public class User
    {
        public string? UserId { get; set; }
        public UserSchema? CurrentSchema { get; set; }
        public UserSchema[]? SchemaHistory { get; set; }
    }

    public class UserSchema
    {
        public DateTimeOffset CreatedAt { get; set; }
        public string? Schema { get; set; }
    }
}
