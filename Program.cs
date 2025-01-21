using DotnesktRemastered.Games;
using Serilog;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DotnesktRemastered
{
    internal class Program
    {
        public static CordycepProcess Cordycep;

        static unsafe void Main(string[] args)
        {
            //Logging
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
            var processes = Process.GetProcessesByName("Cordycep.CLI");
            if (processes.Length <= 0)
            {
                Log.Error("Cordycep.CLI is not running. Please start the CLI first.");
                return;
            }
            Cordycep = new CordycepProcess(processes[0]);

            Cordycep.LoadState();

            string gameId = Encoding.UTF8.GetString(BitConverter.GetBytes(Cordycep.GameID));
            Log.Information("{name} is running @ {environment}", "Cordycep.CLI", Cordycep.WorkingEnvironment);
            Log.Information("GameID: {game}", gameId);
            Log.Information("Pools Address: {address}", $"{(nint)Cordycep.PoolsAddress:X}");
            Log.Information("Strings Address: {address}", $"{(nint)Cordycep.StringsAddress:X}");
            Log.Information("Game Directory: {directory}", Cordycep.GameDirectory);
            switch (gameId)
            {
                case "YAMYAMOK":
                    ModernWarfare6.DumpMap("mp_frontend_jup_01");
                    break;
                default:
                    Log.Error("Game is not supported :(.");
                    return;
            }
        }
    }
}
