using CollisionDetection.Harness;
using System;

try {
    bool runTests = args.Length == 0;
    bool runBenchmarks = false;
    string outputDirectory = null;

    for (int i = 0; i < args.Length; ++i) {
        switch (args[i]) {
        case "--test":
            runTests = true;
            break;
        case "--benchmark":
            runBenchmarks = true;
            break;
        case "--all":
            runTests = true;
            runBenchmarks = true;
            break;
        case "--output":
            if (++i == args.Length) {
                throw new ArgumentException("--output requires a directory.");
            }

            outputDirectory = args[i];
            break;
        default:
            throw new ArgumentException("Unknown argument: " + args[i]);
        }
    }

    if (runTests) {
        CorrectnessTests.Run();
    }

    if (runBenchmarks) {
        BenchmarkRunner.Run(outputDirectory);
    }

    return 0;
} catch (Exception exception) {
    Console.Error.WriteLine(exception);
    return 1;
}
