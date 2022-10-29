using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace EdgeDBDiscordBot.Utils
{
    internal class CliUtils
    {
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static Process ExecuteEdgeDBCommand(string args)
        {
            var p = new Process()
            {
                StartInfo = new()
                {
                    Arguments = args,
                    FileName = IsLinux ? "edgedb" : "edgedb.exe",
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            p.Start();
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();
            return p;
        }
    }
}
