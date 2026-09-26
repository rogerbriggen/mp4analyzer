using Mp4Analyzer.Analysis;

namespace Mp4Analyzer.Tests;

[TestClass]
public sealed class MotionDetectorTests
{
    private static readonly MotionOptions Options = new();

    private static byte[] Frame(byte value = 40) => Enumerable.Repeat(value, Options.FrameWidth * Options.FrameHeight).ToArray();

    /// <summary>Copy of <paramref name="source"/> with a bright rectangle drawn in it.</summary>
    private static byte[] WithRect(byte[] source, int x0, int y0, int w, int h, byte value = 250)
    {
        var frame = (byte[])source.Clone();
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                frame[y * Options.FrameWidth + x] = value;
        return frame;
    }

    [TestMethod]
    public void StaticFrames_HaveNoMovement()
    {
        var result = new MotionDetector(Options).Analyze(Enumerable.Range(0, 20).Select(_ => Frame()));

        Assert.IsFalse(result.HasMovement);
        Assert.AreEqual(20, result.FramesAnalyzed);
        Assert.AreEqual(0, result.MotionFrames);
    }

    [TestMethod]
    public void ChangingClockInTopRightCorner_IsIgnored()
    {
        // Simulate a ticking clock: a different pattern in the top-right corner on every frame.
        var frames = Enumerable.Range(0, 20)
            .Select(i => WithRect(Frame(), 125 + i % 5, 2, 30, 5, (byte)(i % 2 == 0 ? 255 : 0)));

        var result = new MotionDetector(Options).Analyze(frames);

        Assert.IsFalse(result.HasMovement);
        Assert.AreEqual(0.0, result.MaxChangedFraction);
    }

    [TestMethod]
    public void MovingObject_IsDetected()
    {
        // A 10x10 block moving across the centre of the picture.
        var frames = Enumerable.Range(0, 10).Select(i => WithRect(Frame(), 20 + i * 10, 40, 10, 10));

        var result = new MotionDetector(Options).Analyze(frames);

        Assert.IsTrue(result.HasMovement);
        Assert.AreEqual(9, result.MotionFrames);
    }

    [TestMethod]
    public void SingleGlitchFrame_IsNotMovement()
    {
        // One frame differs, then the picture is static again -> 2 changed transitions would count,
        // so require more than that to make sure MinMotionFrames is honoured.
        var detector = new MotionDetector(Options with { MinMotionFrames = 3 });
        var frames = new[] { Frame(), WithRect(Frame(), 50, 30, 40, 40), Frame(), Frame() };

        var result = detector.Analyze(frames);

        Assert.AreEqual(2, result.MotionFrames);
        Assert.IsFalse(result.HasMovement);
    }

    [TestMethod]
    public void SensorNoiseBelowPixelThreshold_IsIgnored()
    {
        var rnd = new Random(42);
        var frames = Enumerable.Range(0, 20).Select(_ =>
        {
            var f = Frame(100);
            for (int i = 0; i < f.Length; i++) f[i] = (byte)(100 + rnd.Next(-10, 11));
            return f;
        });

        var result = new MotionDetector(Options).Analyze(frames);

        Assert.IsFalse(result.HasMovement);
    }

    [TestMethod]
    public void TinyChangeBelowChangedFraction_IsIgnored()
    {
        // 2x2 pixels flicker: 4 / ~13'900 pixels is far below 0.5 %.
        var frames = Enumerable.Range(0, 10).Select(i => i % 2 == 0 ? Frame() : WithRect(Frame(), 80, 60, 2, 2));

        var result = new MotionDetector(Options).Analyze(frames);

        Assert.IsFalse(result.HasMovement);
        Assert.IsGreaterThan(0.0, result.MaxChangedFraction);
    }

    [TestMethod]
    public void ClockMask_CoversTopRightCornerOnly()
    {
        var detector = new MotionDetector(Options);

        Assert.IsTrue(detector.IsIgnored(Options.FrameWidth - 1, 0));
        Assert.IsTrue(detector.IsIgnored(125, 8));
        Assert.IsFalse(detector.IsIgnored(0, 0));
        Assert.IsFalse(detector.IsIgnored(Options.FrameWidth - 1, Options.FrameHeight - 1));
        Assert.IsFalse(detector.IsIgnored(125, 20));
    }

    [TestMethod]
    public void WrongFrameSize_Throws()
    {
        var detector = new MotionDetector(Options);

        Assert.ThrowsExactly<ArgumentException>(() => detector.ChangedFraction(new byte[10], Frame()));
    }
}
