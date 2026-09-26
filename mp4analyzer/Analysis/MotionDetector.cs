namespace Mp4Analyzer.Analysis;

/// <summary>Result of analysing one stream of frames.</summary>
/// <param name="HasMovement">True when enough motion frames were seen.</param>
/// <param name="FramesAnalyzed">Number of sampled frames.</param>
/// <param name="MotionFrames">Number of frames that differed noticeably from their predecessor.</param>
/// <param name="MaxChangedFraction">Largest fraction of changed pixels between two consecutive frames.</param>
internal sealed record MotionResult(bool HasMovement, int FramesAnalyzed, int MotionFrames, double MaxChangedFraction);

/// <summary>
/// Pure frame-difference motion detector. Frames are 8-bit grayscale, row-major,
/// <see cref="MotionOptions.FrameWidth"/> x <see cref="MotionOptions.FrameHeight"/>.
/// The clock in the top-right corner is masked out so a ticking timestamp never counts as movement.
/// </summary>
internal sealed class MotionDetector
{
    private readonly MotionOptions _options;
    private readonly bool[] _ignored;
    private readonly int _consideredPixels;

    public MotionDetector(MotionOptions options)
    {
        _options = options;
        int w = options.FrameWidth, h = options.FrameHeight;
        _ignored = new bool[w * h];

        int maskFromX = w - (int)Math.Ceiling(w * options.ClockMaskWidth);
        int maskToY = (int)Math.Ceiling(h * options.ClockMaskHeight);
        for (int y = 0; y < maskToY; y++)
            for (int x = Math.Max(0, maskFromX); x < w; x++)
                _ignored[y * w + x] = true;

        _consideredPixels = _ignored.Count(i => !i);
    }

    public int FrameSize => _options.FrameWidth * _options.FrameHeight;

    /// <summary>True when the pixel at (x, y) lies inside the ignored clock area.</summary>
    public bool IsIgnored(int x, int y) => _ignored[y * _options.FrameWidth + x];

    /// <summary>Fraction (0..1) of unmasked pixels that changed by more than the pixel threshold.</summary>
    public double ChangedFraction(ReadOnlySpan<byte> previous, ReadOnlySpan<byte> current)
    {
        if (previous.Length != FrameSize || current.Length != FrameSize)
            throw new ArgumentException($"Frames must be {FrameSize} bytes.");

        int changed = 0;
        for (int i = 0; i < current.Length; i++)
        {
            if (_ignored[i]) continue;
            if (Math.Abs(current[i] - previous[i]) > _options.PixelThreshold) changed++;
        }
        return _consideredPixels == 0 ? 0 : (double)changed / _consideredPixels;
    }

    /// <summary>Analyses a sequence of frames, comparing each frame with its predecessor.</summary>
    public MotionResult Analyze(IEnumerable<byte[]> frames)
    {
        byte[]? previous = null;
        int count = 0, motionFrames = 0;
        double max = 0;

        foreach (var frame in frames)
        {
            count++;
            if (previous is not null)
            {
                double fraction = ChangedFraction(previous, frame);
                max = Math.Max(max, fraction);
                if (fraction >= _options.ChangedFraction) motionFrames++;
            }
            previous = frame;
        }

        return new MotionResult(motionFrames >= _options.MinMotionFrames, count, motionFrames, max);
    }
}
