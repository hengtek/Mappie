﻿using Mappie.FileStorage;
using Mappie.Games;
using Serilog;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Mappie
{
    internal class Program
    {
        public static CordycepProcess Cordycep;
        private static BaseGame GameInstance;

        public delegate void DumpMapFunc(string name, bool noStaticProps = false, Vector3 staticPropsOrigin = new(),
            uint range = 0, bool onlyJson = false);

        public delegate string[] GetMapListFunc();

        static void Main(string[] args)
        {
            //Logging
            if (File.Exists("DotnesktLog.txt"))
            {
                File.Delete("DotnesktLog.txt");
            }

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(
                    "DotnesktLog.txt",
                    outputTemplate:
                    "[ {Timestamp:dd-MM-yyyy HH-mm-ss}    ] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    fileSizeLimitBytes: null,
                    rollOnFileSizeLimit: false,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1)
                )
                .CreateLogger();

            var processes = Process.GetProcessesByName("Cordycep.CLI");
            if (processes.Length <= 0)
            {
                Log.Error("Cordycep.CLI is not running. Please start the CLI first.");
                Console.ReadKey();
                return;
            }

            Cordycep = new CordycepProcess(processes[0]);

            Cordycep.LoadState();

            string gameId = Encoding.UTF8.GetString(BitConverter.GetBytes(Cordycep.GameID));
            Log.Information("{name} is running @ {environment}", "Cordycep.CLI", Cordycep.WorkingEnvironment);
            Log.Information("GameID: {game}", gameId);
            Log.Information("Pools Address: {address:X}", Cordycep.PoolsAddress);
            Log.Information("Strings Address: {address:X}", Cordycep.StringsAddress);
            Log.Information("Game Directory: {directory}", Cordycep.GameDirectory);
            Log.Information("Flag: {flag}", string.Join(", ", Cordycep.Flags));
            switch (gameId)
            {
                case "YAMYAMOK":
                    XSub.LoadFiles(Cordycep.GameDirectory);
                    GameInstance =Cordycep.IsSinglePlayer() ? new ModernWarfare6SP() : new ModernWarfare6();
                    break;
                case "BLACKOP6":
                    XSub.LoadFiles(Cordycep.GameDirectory);
                    GameInstance = new BlackOps6();
                    break;
                default:
                    Log.Error("Game is not supported. :(");
                    return;
            }

            ListenConsole();
        }


        public static void ListenConsole()
        {
            Log.Information("Enter 'help' for a list of commands.");
            while (true)
            {
                string[] commandAndArgs = Console.ReadLine().Split(" ");
                string command = commandAndArgs[0];
                string[] args = commandAndArgs.Skip(1).ToArray();

                switch (command)
                {
                    case "help":
                        Log.Information("Commands: list, dump");
                        break;
                    case "list":
                        string[] maps = GameInstance.GetMapList();
                        Log.Information("There are currently {0} loaded maps.", maps.Length);
                        foreach (string map in maps)
                        {
                            Log.Information(">> {map}", map);
                        }

                        break;
                    case "dump":
                        if (args.Length <= 0 || args[0] == "")
                        {
                            Log.Warning("Usage: dump <map_name>");
                            Log.Information("-nostaticprops                     : Skip static props");
                            Log.Information("-staticpropsrange <x> <y> <range>  : Only exports static props in given area");
                            Log.Information("-onlyjson                          : Only export json files");
                            break;
                        }

                        string mapName = args[0];
                        int index = 1;
                        bool noStaticProps = false;
                        Vector3 staticPropsOrigin = Vector3.Zero;
                        uint range = 0;
                        bool onlyJson = false;
                        while (index < args.Length)
                        {
                            string option = args[index];
                            if (option == "-nostaticprops")
                            {
                                noStaticProps = true;
                            }
                            else if (option == "-staticpropsrange")
                            {
                                if (args.Length - index < 4)
                                {
                                    Log.Warning("Usage: dump <map_name>");
                                    Log.Information("-nostaticprops                     : Skip static props");
                                    Log.Information("-staticpropsrange <x> <y> <range>  : Only exports static props in given area");
                                    Log.Information("-onlyjson                          : Only export json files");
                                    break;
                                }

                                staticPropsOrigin = new Vector3(float.Parse(args[index + 1]),
                                    float.Parse(args[index + 2]), 0);
                                range = uint.Parse(args[index + 3]);
                                index += 3;
                            }
                            else if (option == "-onlyjson")
                            {
                                onlyJson = true;
                            }

                            index++;
                        }

                        Log.Information("Dumping options: ");
                        Log.Information(">> No static props: {0}", noStaticProps);
                        Log.Information(">> Static props origins: {0} {1}", staticPropsOrigin.X, staticPropsOrigin.Y);
                        Log.Information(">> Static props range: {0}", range);

                        GameInstance.DumpMap(mapName, noStaticProps, staticPropsOrigin, range, onlyJson);
                        break;
                    default:
                        Log.Warning("Unknown command. Enter 'help' for a list of commands.");
                        break;
                }
            }
        }
    }
}