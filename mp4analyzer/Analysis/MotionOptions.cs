namespace Mp4Analyzer.Analysis;

/// <summary>Tuning knobs for motion detection. Defaults are calibrated against <c>testdata/</c>.</summary>
internal sealed record MotionOptions
{
    /// <summary>Width of the downscaled grayscale frames that are compared.</summary>
    public int FrameWidth { get; init; } = 160;

    /// <summary>Height of the downscaled grayscale frames that are compared.</summary>
    public int FrameHeight { get; init; } = 90;

    /// <summary>How many frames per second are sampled from the video.</summary>
    public double SampleFps { get; init; } = 2;

    /// <summary>A pixel counts as "changed" when its gray value differs by more than this (0-255).</summary>
    public int PixelThreshold { get; init; } = 25;

    /// <summary>A frame counts as a "motion frame" when at least this fraction of the
    /// (unmasked) pixels changed compared to the previous sampled frame.</summary>
    public double ChangedFraction { get; init; } = 0.005;

    /// <summary>A video has movement when at least this many motion frames were found
    /// (guards against a single decoding glitch).</summary>
    public int MinMotionFrames { get; init; } = 2;

    /// <summary>Width of the ignored clock area in the top-right corner, as a fraction of the frame width.</summary>
    public double ClockMaskWidth { get; init; } = 0.25;

    /// <summary>Height of the ignored clock area in the top-right corner, as a fraction of the frame height.</summary>
    public double ClockMaskHeight { get; init; } = 0.10;
}
