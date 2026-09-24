using System;
using System.Diagnostics;
using CinematicPoker.Engine.Simulation;

namespace CinematicPoker.Simulation
{
    /// <summary>
    /// Console soak runner: plays automated AI-vs-AI poker and verifies engine
    /// invariants. Usage: dotnet run [hands] [seed] [equityIterations]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            int hands = args.Length > 0 ? int.Parse(args[0]) : 10000;
            int seed = args.Length > 1 ? int.Parse(args[1]) : 42;
            int equityIterations = args.Length > 2 ? int.Parse(args[2]) : 60;

            Console.WriteLine($"Running {hands} hands (seed {seed}, {equityIterations} equity iterations)...");
            var sw = Stopwatch.StartNew();

            try
            {
                SimulationResult result = SimulationRunner.Run(hands, seed, equityIterations, Console.WriteLine);
                sw.Stop();

                Console.WriteLine();
                Console.WriteLine($"PASS in {sw.Elapsed.TotalSeconds:F1}s — all invariants held.");
                Console.WriteLine(result);
                Console.WriteLine();
                Console.WriteLine("Winning hand categories at showdown:");
                foreach (var kv in result.WinningCategories)
                    Console.WriteLine($"  {kv.Key,-15} {kv.Value}");
                return 0;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Console.Error.WriteLine($"FAIL after {sw.Elapsed.TotalSeconds:F1}s: {ex}");
                return 1;
            }
        }
    }
}
