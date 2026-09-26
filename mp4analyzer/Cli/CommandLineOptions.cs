using System.Globalization;
using Mp4Analyzer.Analysis;

namespace Mp4Analyzer.Cli;

/// <summary>Parsed command line. <see cref="Parse"/> returns an error message instead of throwing.</summary>
internal sealed record CommandLineOptions
{
    public string? Folder { get; init; }
    public bool DryRun { get; init; }
    public bool Recursive { get; init; }
    public bool Verbose { get; init; }
    public bool ShowHelp { get; init; }
    public bool ShowVersion { get; init; }
    public string FfmpegPath { get; init; } = "ffmpeg";
    public MotionOptions Motion { get; init; } = new();

    public static (CommandLineOptions? Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        var o = new CommandLineOptions();
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            string? Next() => i + 1 < args.Count ? args[++i] : null;

            switch (arg)
            {
                case "-h" or "--help" or "help": o = o with { ShowHelp = true }; break;
                case "-v" or "--version" or "version": o = o with { ShowVersion = true }; break;
                case "-n" or "--dry-run": o = o with { DryRun = true }; break;
                case "-r" or "--recursive": o = o with { Recursive = true }; break;
                case "--verbose": o = o with { Verbose = true }; break;
                case "--ffmpeg":
                    var path = Next();
                    if (string.IsNullOrWhiteSpace(path)) return (null, "--ffmpeg requires a path.");
                    o = o with { FfmpegPath = path };
                    break;
                case "--pixel-threshold":
                    if (!TryInt(Next(), 0, 255, out var pt)) return (null, "--pixel-threshold must be an integer 0-255.");
                    o = o with { Motion = o.Motion with { PixelThreshold = pt } };
                    break;
                case "--changed-fraction":
                    if (!TryDouble(Next(), 0, 1, out var cf)) return (null, "--changed-fraction must be a number 0-1.");
                    o = o with { Motion = o.Motion with { ChangedFraction = cf } };
                    break;
                case "--min-motion-frames":
                    if (!TryInt(Next(), 1, int.MaxValue, out var mf)) return (null, "--min-motion-frames must be an integer >= 1.");
                    o = o with { Motion = o.Motion with { MinMotionFrames = mf } };
                    break;
                case "--fps":
                    if (!TryDouble(Next(), 0.1, 60, out var fps)) return (null, "--fps must be a number 0.1-60.");
                    o = o with { Motion = o.Motion with { SampleFps = fps } };
                    break;
                case "--clock-width":
                    if (!TryDouble(Next(), 0, 1, out var cw)) return (null, "--clock-width must be a number 0-1.");
                    o = o with { Motion = o.Motion with { ClockMaskWidth = cw } };
                    break;
                case "--clock-height":
                    if (!TryDouble(Next(), 0, 1, out var ch)) return (null, "--clock-height must be a number 0-1.");
                    o = o with { Motion = o.Motion with { ClockMaskHeight = ch } };
                    break;
                default:
                    if (arg.StartsWith('-')) return (null, $"Unknown option: {arg}");
                    if (o.Folder is not null) return (null, $"Only one folder may be given (got '{o.Folder}' and '{arg}').");
                    o = o with { Folder = arg };
                    break;
            }
        }

        if (o.Folder is null && !o.ShowHelp && !o.ShowVersion)
            return (null, "Missing folder argument.");
        return (o, null);
    }

    private static bool TryInt(string? s, int min, int max, out int value) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;

    private static bool TryDouble(string? s, double min, double max, out double value) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
}
