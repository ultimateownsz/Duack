using Duack;
namespace DuackCli;

/// <summary>Entry point for the <c>duack</c> CLI (init / validate / convert).</summary>
public static class Program
{
    /// <summary>Runs the CLI and returns the process exit code (0 on success).</summary>
    public static int Main(string[] args) => Cli.Run(args, Console.Out, Console.Error);
}


