using System.Globalization;
using System.Reflection;
using Mp4Analyzer.Analysis;
using Mp4Analyzer.Cli;

namespace Mp4Analyzer;

internal static class Program
{
    private static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    internal static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        var (options, parseError) = CommandLineOptions.Parse(args);
        if (options is null)
        {
            error.WriteLine($"Error: {parseError}");
            error.WriteLine();
            PrintUsage(error);
            return ExitCodes.Usage;
        }
        if (options.ShowHelp)
        {
            PrintUsage(output);
            return ExitCodes.Success;
        }
        if (options.ShowVersion)
        {
            output.WriteLine($"mp4analyzer {GetVersion()}");
            return ExitCodes.Success;
        }

        string folder = Path.GetFullPath(options.Folder!);
        if (!Directory.Exists(folder))
        {
            error.WriteLine($"Error: folder not found: {folder}");
            return ExitCodes.Environment;
        }

        var frameSource = new FfmpegFrameSource(options.FfmpegPath);
        if (!frameSource.IsAvailable())
        {
            error.WriteLine($"Error: ffmpeg could not be started ('{options.FfmpegPath}'). " +
                            "Install ffmpeg and put it on PATH, or pass --ffmpeg <path>.");
            return ExitCodes.Environment;
        }

        var videos = VideoAnalyzer.FindVideos(folder, options.Recursive);
        output.WriteLine($"Analysing {videos.Count} video(s) in {folder}{(options.DryRun ? " (dry run, nothing is deleted)" : "")}");

        var analyzer = new VideoAnalyzer(frameSource, options.Motion);
        var results = new List<VideoResult>();
        foreach (var video in videos)
        {
            var result = analyzer.AnalyzeFile(video);
            results.Add(result);
            string name = Path.GetRelativePath(folder, video);

            if (result.Failed)
                output.WriteLine($"  ERROR     {name}: {result.Error}");
            else
                output.WriteLine($"  {(result.HasMovement ? "movement " : "no motion")} {name}{(options.Verbose ? Details(result.Motion!) : "")}");

            if (!result.Failed && !result.HasMovement && !options.DryRun)
            {
                try
                {
                    File.Delete(video);
                    output.WriteLine($"            deleted {name}");
                }
                catch (Exception ex)
                {
                    results[^1] = result with { Error = $"could not delete: {ex.Message}" };
                    error.WriteLine($"  ERROR     could not delete {name}: {ex.Message}");
                }
            }
        }

        PrintSummary(output, folder, results, options.DryRun);
        return results.Any(r => r.Failed) ? ExitCodes.AnalysisFailed : ExitCodes.Success;
    }

    private static string Details(MotionResult m) => string.Create(CultureInfo.InvariantCulture,
        $"  [frames={m.FramesAnalyzed}, motionFrames={m.MotionFrames}, maxChanged={m.MaxChangedFraction:P2}]");

    private static void PrintSummary(TextWriter output, string folder, List<VideoResult> results, bool dryRun)
    {
        string Rel(VideoResult r) => Path.GetRelativePath(folder, r.Path);
        var withMovement = results.Where(r => !r.Failed && r.HasMovement).ToList();
        var withoutMovement = results.Where(r => r.Motion is not null && !r.HasMovement).ToList();
        var failed = results.Where(r => r.Motion is null).ToList();

        output.WriteLine();
        output.WriteLine($"Videos with movement ({withMovement.Count}):");
        foreach (var r in withMovement) output.WriteLine($"  {Rel(r)}");

        output.WriteLine($"Videos without movement ({withoutMovement.Count}){(dryRun ? " - would be deleted" : " - deleted")}:");
        foreach (var r in withoutMovement)
            output.WriteLine($"  {Rel(r)}{(r.Error is not null ? $"  (NOT deleted: {r.Error})" : "")}");

        if (failed.Count > 0)
        {
            output.WriteLine($"Videos that could not be analysed ({failed.Count}) - left untouched:");
            foreach (var r in failed) output.WriteLine($"  {Rel(r)}: {r.Error}");
        }
    }

    private static string GetVersion()
    {
        var asm = typeof(Program).Assembly;
        return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "unknown";
    }

    private static void PrintUsage(TextWriter w) => w.WriteLine(
        """
        mp4analyzer - delete surveillance videos (.mp4) that contain no movement.

        The ticking clock in the top-right corner is ignored. Videos are decoded with ffmpeg,
        which must be installed (on PATH, or passed via --ffmpeg).

        Usage:
          mp4analyzer <folder> [options]

        Options:
          -n, --dry-run              Do not delete anything; only report the results.
          -r, --recursive            Also scan sub folders.
              --verbose              Print per-video statistics.
              --ffmpeg <path>        Path to the ffmpeg executable (default: ffmpeg).
              --pixel-threshold <n>  Gray-level difference for a pixel to count as changed (default 25).
              --changed-fraction <f> Fraction of changed pixels for a frame to count as motion (default 0.005).
              --min-motion-frames <n> Motion frames needed for a video to have movement (default 2).
              --fps <f>              Frames per second sampled from the video (default 2).
              --clock-width <f>      Width of the ignored top-right clock area, fraction of frame (default 0.25).
              --clock-height <f>     Height of the ignored top-right clock area, fraction of frame (default 0.10).
          -v, --version              Print the version.
          -h, --help                 Show this help.

        Exit codes: 0 success, 1 usage error, 2 folder/ffmpeg not found, 3 a video could not be analysed.
        """);
}
