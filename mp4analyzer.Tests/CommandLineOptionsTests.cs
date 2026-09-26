using Mp4Analyzer.Cli;

namespace Mp4Analyzer.Tests;

[TestClass]
public sealed class CommandLineOptionsTests
{
    [TestMethod]
    public void FolderOnly_UsesDefaults()
    {
        var (o, error) = CommandLineOptions.Parse(["videos"]);

        Assert.IsNull(error);
        Assert.AreEqual("videos", o!.Folder);
        Assert.IsFalse(o.DryRun);
        Assert.IsFalse(o.Recursive);
        Assert.AreEqual("ffmpeg", o.FfmpegPath);
    }

    [TestMethod]
    public void AllOptions_AreParsed()
    {
        var (o, error) = CommandLineOptions.Parse(
        [
            "--dry-run", "-r", "--verbose", "videos", "--ffmpeg", "/opt/ffmpeg",
            "--pixel-threshold", "30", "--changed-fraction", "0.01", "--min-motion-frames", "4",
            "--fps", "1.5", "--clock-width", "0.3", "--clock-height", "0.2",
        ]);

        Assert.IsNull(error);
        Assert.IsTrue(o!.DryRun);
        Assert.IsTrue(o.Recursive);
        Assert.IsTrue(o.Verbose);
        Assert.AreEqual("videos", o.Folder);
        Assert.AreEqual("/opt/ffmpeg", o.FfmpegPath);
        Assert.AreEqual(30, o.Motion.PixelThreshold);
        Assert.AreEqual(0.01, o.Motion.ChangedFraction);
        Assert.AreEqual(4, o.Motion.MinMotionFrames);
        Assert.AreEqual(1.5, o.Motion.SampleFps);
        Assert.AreEqual(0.3, o.Motion.ClockMaskWidth);
        Assert.AreEqual(0.2, o.Motion.ClockMaskHeight);
    }

    [TestMethod]
    [DataRow(new string[0], "Missing folder")]
    [DataRow(new[] { "a", "b" }, "Only one folder")]
    [DataRow(new[] { "a", "--bogus" }, "Unknown option")]
    [DataRow(new[] { "a", "--pixel-threshold", "300" }, "--pixel-threshold")]
    [DataRow(new[] { "a", "--changed-fraction" }, "--changed-fraction")]
    public void InvalidArguments_ReturnError(string[] args, string expected)
    {
        var (o, error) = CommandLineOptions.Parse(args);

        Assert.IsNull(o);
        Assert.Contains(expected, error!);
    }

    [TestMethod]
    public void HelpAndVersion_DoNotNeedFolder()
    {
        Assert.IsTrue(CommandLineOptions.Parse(["--help"]).Options!.ShowHelp);
        Assert.IsTrue(CommandLineOptions.Parse(["-v"]).Options!.ShowVersion);
    }
}
