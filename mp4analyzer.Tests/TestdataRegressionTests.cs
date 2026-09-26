using Mp4Analyzer.Analysis;
using Mp4Analyzer.Cli;

namespace Mp4Analyzer.Tests;

/// <summary>
/// Regression tests against the real videos in <c>testdata/</c>. The expected result is the name of the
/// folder a video lives in: <c>testdata/movement/*.mp4</c> must be detected as movement,
/// <c>testdata/no_movement/*.mp4</c> as no movement. Drop new samples into these folders to extend the suite.
/// Requires ffmpeg on PATH; the tests are reported as inconclusive when it is missing
/// (or fail, when <c>MP4ANALYZER_REQUIRE_FFMPEG=1</c> is set, as in CI).
/// </summary>
[TestClass]
public sealed class TestdataRegressionTests
{
    private const string Movement = "movement";
    private const string NoMovement = "no_movement";

    private static readonly FfmpegFrameSource FrameSource = new();

    public static IEnumerable<object[]> Videos() =>
        new[] { Movement, NoMovement }
            .SelectMany(expected => Directory.EnumerateFiles(Path.Combine(TestdataFolder, expected), "*.mp4")
                .Order(StringComparer.Ordinal)
                .Select(file => new object[] { expected, Path.GetFileName(file) }));

    public static string TestdataFolder { get; } = FindTestdata();

    private static string FindTestdata()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "testdata");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException($"No 'testdata' folder found above {AppContext.BaseDirectory}.");
    }

    private static void RequireFfmpeg()
    {
        if (FrameSource.IsAvailable()) return;
        // CI sets this so a missing ffmpeg fails the build instead of silently skipping the regression tests.
        if (Environment.GetEnvironmentVariable("MP4ANALYZER_REQUIRE_FFMPEG") == "1")
            Assert.Fail("ffmpeg is not installed / not on PATH, but MP4ANALYZER_REQUIRE_FFMPEG=1.");
        Assert.Inconclusive("ffmpeg is not installed / not on PATH.");
    }

    [TestMethod]
    public void TestdataContainsBothCategories()
    {
        var videos = Videos().ToList();
        Assert.IsTrue(videos.Any(v => (string)v[0] == Movement), "no videos in testdata/movement");
        Assert.IsTrue(videos.Any(v => (string)v[0] == NoMovement), "no videos in testdata/no_movement");
    }

    [TestMethod]
    [DynamicData(nameof(Videos))]
    public void Video_ResultMatchesFolderName(string expectedFolder, string fileName)
    {
        RequireFfmpeg();
        var analyzer = new VideoAnalyzer(FrameSource, new MotionOptions());

        var result = analyzer.AnalyzeFile(Path.Combine(TestdataFolder, expectedFolder, fileName));

        Assert.IsNull(result.Error, result.Error);
        string actual = result.HasMovement ? Movement : NoMovement;
        Assert.AreEqual(expectedFolder, actual, $"{fileName}: {result.Motion}");
    }

    [TestMethod]
    public void DryRun_ReportsBothListsAndDeletesNothing()
    {
        RequireFfmpeg();
        var before = Directory.GetFiles(TestdataFolder, "*.mp4", SearchOption.AllDirectories);
        var output = new StringWriter();

        int exit = Program.Run([TestdataFolder, "--recursive", "--dry-run"], output, new StringWriter());

        Assert.AreEqual(ExitCodes.Success, exit, output.ToString());
        CollectionAssert.AreEquivalent(before, Directory.GetFiles(TestdataFolder, "*.mp4", SearchOption.AllDirectories));

        string text = output.ToString();
        string withMovement = Section(text, "Videos with movement", "Videos without movement");
        string withoutMovement = Section(text, "Videos without movement", null);
        foreach (var v in Videos())
        {
            string rel = Path.Combine((string)v[0], (string)v[1]);
            string expectedSection = (string)v[0] == Movement ? withMovement : withoutMovement;
            string otherSection = (string)v[0] == Movement ? withoutMovement : withMovement;
            Assert.Contains(rel, expectedSection, text);
            Assert.DoesNotContain(rel, otherSection, text);
        }
    }

    [TestMethod]
    public void RealRun_DeletesOnlyVideosWithoutMovement()
    {
        RequireFfmpeg();
        // Work on a copy so testdata/ stays intact.
        string work = Path.Combine(Path.GetTempPath(), "mp4analyzer-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var v in Videos())
            {
                string dest = Path.Combine(work, (string)v[0], (string)v[1]);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(Path.Combine(TestdataFolder, (string)v[0], (string)v[1]), dest);
            }

            int exit = Program.Run([work, "-r"], new StringWriter(), new StringWriter());

            Assert.AreEqual(ExitCodes.Success, exit);
            foreach (var v in Videos())
            {
                bool exists = File.Exists(Path.Combine(work, (string)v[0], (string)v[1]));
                Assert.AreEqual((string)v[0] == Movement, exists, $"{v[0]}/{v[1]} exists={exists}");
            }
        }
        finally
        {
            if (Directory.Exists(work)) Directory.Delete(work, recursive: true);
        }
    }

    [TestMethod]
    public void MissingFfmpeg_ReturnsEnvironmentError()
    {
        var error = new StringWriter();

        int exit = Program.Run([TestdataFolder, "--ffmpeg", "/definitely/not/ffmpeg"], new StringWriter(), error);

        Assert.AreEqual(ExitCodes.Environment, exit);
        Assert.Contains("ffmpeg", error.ToString());
    }

    [TestMethod]
    public void MissingFolder_ReturnsEnvironmentError()
    {
        int exit = Program.Run([Path.Combine(TestdataFolder, "does-not-exist")], new StringWriter(), new StringWriter());

        Assert.AreEqual(ExitCodes.Environment, exit);
    }

    private static string Section(string text, string start, string? end)
    {
        int s = text.IndexOf(start, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, s, $"'{start}' not found in output:\n{text}");
        int e = end is null ? text.Length : text.IndexOf(end, s, StringComparison.Ordinal);
        return text[s..(e < 0 ? text.Length : e)];
    }
}
