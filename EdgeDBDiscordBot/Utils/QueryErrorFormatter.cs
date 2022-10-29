using EdgeDB;
using EdgeDB.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdgeDBDiscordBot.Utils
{
    internal class QueryErrorFormatter
    {
        public static string FormatError(string query, EdgeDBErrorException error)
        {
            var coloredQuery = EdgeDBColorer.ColorSchemaOrQuery(query);

            var headers = error.ErrorResponse.Headers.Cast<Header?>();
            var rawCharStart = headers.FirstOrDefault(x => x.HasValue && x.Value.Code == 0xFFF9, null);
            var rawCharEnd = headers.FirstOrDefault(x => x.HasValue && x.Value.Code == 0xFFFA, null);

            if (!rawCharStart.HasValue || !rawCharEnd.HasValue)
                return coloredQuery;

            int charStart = int.Parse(rawCharStart.Value.ToString()), 
                charEnd = int.Parse(rawCharEnd.Value.ToString());

            var queryErrorSource = query[charStart..charEnd];
            var count = charEnd - charStart;
            
            // make the error section red
            var coloredIndex = coloredQuery.IndexOf(queryErrorSource, charStart);
            coloredQuery = coloredQuery.Remove(coloredIndex, count);
            coloredQuery = coloredQuery.Insert(coloredIndex, $"\u001b[0;31m{queryErrorSource}");
            coloredQuery = coloredQuery + $"\n\u001b[0;31m{"".PadLeft(charStart)}{"".PadLeft(count, '^')} error";
            return coloredQuery;
        }
    }
}
