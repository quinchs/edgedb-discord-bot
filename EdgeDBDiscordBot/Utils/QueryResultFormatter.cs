using EdgeDB;
using EdgeDB.DataTypes;
using EdgeDBDiscordBot.Modules;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EdgeDBDiscordBot.Utils
{
    public class FormattedQueryResult
    {
        public string RawResult { get; set; }

        public string Formatted { get; set; }

        public FormattedQueryResult(string result)
        {
            RawResult = result;
            Formatted = result;
        }
        public FormattedQueryResult(string result, string formatted)
        {
            Formatted = formatted;
            RawResult = result;
        }
    }
    
    internal class QueryResultFormatter
    {
        public static async Task<FormattedQueryResult> ExecuteAndFormatQueryAsync(string query, EdgeDBBinaryClient edgedb, 
            QueryFormat targetFormat)
        {
            switch (targetFormat)
            {
                case QueryFormat.Json:
                    {
                        var json = await edgedb.QueryJsonAsync(query).ConfigureAwait(false);
                        return new FormattedQueryResult(json);
                    }
                case QueryFormat.Binary:
                    {
                        var result = await edgedb.QueryAsync<dynamic>(query).ConfigureAwait(false);
                        var formatted = FormatBinaryObject(result);
                        
                        return new FormattedQueryResult(Regex.Replace(formatted, @"(\x00\x1b\[\d+;\d+m)", (_) => string.Empty), formatted);
                    }
                default:
                    throw new NotSupportedException($"No format executer found for {targetFormat}");
            }
        }

        private static string FormatBinaryObject(object? obj, int depth = 0, bool padStart = true)
        {
            if (obj is IDictionary<string, object?> expando)
            {
                var tname = (string)expando["__tname__"]!;

                List<string> properties = new();

                foreach(var prop in expando)
                {
                    if (prop.Key is "__tname__" or "__tid__")
                        continue;

                    var value = FormatBinaryObject(prop.Value, depth + 1);

                    properties.Add($"\u001b[0;{EdgeDBColorer.ANSI_COLOR_WHITE}m{prop.Key}: {value}");
                    
                }

                var body = string.Join($"\u001b[0;{EdgeDBColorer.ANSI_COLOR_WHITE}m,\n{"".PadLeft(depth + 1, '\t')}", properties);

                return $"{"".PadLeft(depth - 1, '\t')}\u001b[0;{EdgeDBColorer.ANSI_COLOR_PINK}m{tname} {{\n{"".PadLeft(depth + 1, '\t')}{body}\n{"".PadLeft(depth, '\t')}\u001b[0;{EdgeDBColorer.ANSI_COLOR_PINK}m}}";
            }
            else if (obj is IEnumerable rawEnumerable && obj is not string)
            {
                var enumerable = rawEnumerable.Cast<object?>();

                if (!enumerable.Any())
                    return $"{"".PadLeft(padStart ? depth : 0, '\t')}\u001b[0;{EdgeDBColorer.ANSI_COLOR_BLUE}m{{}}";

                var inner = enumerable.FirstOrDefault(x => x != null);

                if(inner is null)
                    return $"{"".PadLeft(padStart ? depth : 0, '\t')}\u001b[0;{EdgeDBColorer.ANSI_COLOR_BLUE}m{{}}";

                if(depth == 1 && inner is not IDictionary<string, object>)
                    return $"\u001b[0;{EdgeDBColorer.ANSI_COLOR_BLUE}m{{{string.Join($"\u001b[0;{EdgeDBColorer.ANSI_COLOR_WHITE}m,", enumerable.Select(x => FormatBinaryObject(x, depth + 1, false)))}\u001b[0;{EdgeDBColorer.ANSI_COLOR_BLUE}m}}";

                else if (inner is IDictionary<string, object?>)
                    return $"{"".PadLeft(padStart ? depth : 0, '\t')}\u001b[0;{EdgeDBColorer.ANSI_COLOR_BLUE}m{{\n{"".PadLeft(depth + 1, '\t')}{string.Join($"\u001b[0;{EdgeDBColorer.ANSI_COLOR_WHITE}m,\n{"".PadLeft(depth + 1, '\t')}", enumerable.Select(x => FormatBinaryObject(x, depth + 1)))}\n{"".PadLeft(depth, '\t')}\u001b[0;{EdgeDBColorer.ANSI_COLOR_BLUE}m}}";
                else
                    return $"{"".PadLeft(padStart ? depth : 0, '\t')}\u001b[0;{EdgeDBColorer.ANSI_COLOR_WHITE}m[{string.Join($"\u001b[0;{EdgeDBColorer.ANSI_COLOR_WHITE}m,", enumerable.Select(x => FormatBinaryObject(x, depth + 1)))}\u001b[0;{EdgeDBColorer.ANSI_COLOR_WHITE}m]";
            }
            else
                return FormatBinaryScalar(obj);
        }

        private static string FormatBinaryScalar(object? value)
        {
            if (value == null)
                return "{}";
            
            string str = "";
            var type = value.GetType();

            if (IsNumericType(value))
                str = value.ToString()!;
            else
            {
                str = value switch
                {
                    Enum enm => $"<{enm.GetType().Name}>\'{value}\'",
                    bool bl => bl ? "true" : "false",
                    string s => $"\"{s}\"",
                    DateTimeOffset dto => $"<std::datetime>\'{dto.ToString("O")}\'",
                    DateTime dt => $"<cal::local_datetime>\'{dt.ToString("O")}\'",
                    TimeSpan ts => $"<cal::relative_duration>\'{ts.ToString(@"hh\:mm\:ss")}\'",
                    Json json => $"<json>\'{(json.Value ?? "{}").Replace("\"", "\\\"").Replace("\'", "\\\'")}\'",
                    Guid guid => $"<uuid>\'{guid}\'",
                    _ => value.ToString()!
                };
            }

            return EdgeDBColorer.ColorSchemaOrQuery(str);
        }

        private static bool IsNumericType(object o)
        {
            switch (Type.GetTypeCode(o.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }
    }
}
