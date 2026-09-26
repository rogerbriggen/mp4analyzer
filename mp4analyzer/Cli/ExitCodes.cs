namespace Mp4Analyzer.Cli;

internal static class ExitCodes
{
    public const int Success = 0;
    public const int Usage = 1;
    /// <summary>Environment problem: folder not found, ffmpeg not available.</summary>
    public const int Environment = 2;
    /// <summary>At least one video could not be analysed (it was left untouched).</summary>
    public const int AnalysisFailed = 3;
}
