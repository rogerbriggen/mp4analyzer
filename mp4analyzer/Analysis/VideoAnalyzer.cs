namespace Mp4Analyzer.Analysis;

/// <summary>Outcome for a single video file.</summary>
internal sealed record VideoResult(string Path, MotionResult? Motion, string? Error)
{
    public bool Failed => Error is not null;
    public bool HasMovement => Motion?.HasMovement ?? false;
}

/// <summary>Runs motion detection on single files or whole folders of <c>.mp4</c> files.</summary>
internal sealed class VideoAnalyzer(FfmpegFrameSource frameSource, MotionOptions options)
{
    private readonly MotionDetector _detector = new(options);

    public VideoResult AnalyzeFile(string path)
    {
        try
        {
            var motion = _detector.Analyze(frameSource.ReadFrames(path, options));
            if (motion.FramesAnalyzed == 0)
                return new VideoResult(path, null, "no video frames could be decoded");
            return new VideoResult(path, motion, null);
        }
        catch (Exception ex)
        {
            return new VideoResult(path, null, ex.Message);
        }
    }

    /// <summary>All <c>.mp4</c> files (case-insensitive) in <paramref name="folder"/>, sorted by path.</summary>
    public static IReadOnlyList<string> FindVideos(string folder, bool recursive) =>
        Directory.EnumerateFiles(folder, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => string.Equals(System.IO.Path.GetExtension(f), ".mp4", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToList();
}
