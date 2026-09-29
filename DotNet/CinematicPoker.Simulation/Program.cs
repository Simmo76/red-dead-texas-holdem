using System;
using System.Diagnostics;
using System.IO;
using CinematicPoker.Engine.Simulation;

namespace CinematicPoker.Simulation
{
    /// <summary>
    /// Console soak runner: plays automated AI-vs-AI poker and verifies engine
    /// invariants. Usage: dotnet run [hands] [seed] [equityIterations] [markdownLogPath]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            int hands = args.Length > 0 ? int.Parse(args[0]) : 10000;
            int seed = args.Length > 1 ? int.Parse(args[1]) : 42;
            int equityIterations = args.Length > 2 ? int.Parse(args[2]) : 60;
            string markdownPath = args.Length > 3 ? args[3] : null;

            Console.WriteLine($"Running {hands} hands (seed {seed}, {equityIterations} equity iterations)...");
            if (!string.IsNullOrEmpty(markdownPath))
                Console.WriteLine($"Hand log markdown: {Path.GetFullPath(markdownPath)}");

            var sw = Stopwatch.StartNew();
            HandSequenceLog handLog = !string.IsNullOrEmpty(markdownPath) ? new HandSequenceLog() : null;

            try
            {
                SimulationResult result = SimulationRunner.Run(
                    hands, seed, equityIterations, Console.WriteLine, handLog);
                sw.Stop();

                if (handLog != null)
                {
                    string fullPath = Path.GetFullPath(markdownPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
                    using (var writer = new StreamWriter(fullPath, false))
                        handLog.WriteMarkdown(writer, seed, hands, result);
                    Console.WriteLine($"Wrote {handLog.Hands.Count} hands to {fullPath}");
                }

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
