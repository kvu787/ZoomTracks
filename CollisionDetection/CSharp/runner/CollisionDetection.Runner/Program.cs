using System;

namespace ZoomTracks.CollisionDetection.Runner
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                string command = args.Length == 0 ? "all" : args[0].ToLowerInvariant();
                switch (command)
                {
                    case "test":
                        CorrectnessTests.Run();
                        return 0;
                    case "bench":
                        BenchmarkRunner.Run();
                        return 0;
                    case "all":
                        CorrectnessTests.Run();
                        BenchmarkRunner.Run();
                        return 0;
                    default:
                        Console.Error.WriteLine("Usage: CollisionDetection.Runner [test|bench|all]");
                        return 2;
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }
    }
}
