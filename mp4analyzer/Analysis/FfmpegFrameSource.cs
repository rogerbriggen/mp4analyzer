using System.Diagnostics;
using System.Globalization;

namespace Mp4Analyzer.Analysis;

/// <summary>
/// Decodes a video with the external <c>ffmpeg</c> executable into small raw grayscale frames
/// (piped over stdout), so no video codec has to be implemented in .NET.
/// </summary>
internal sealed class FfmpegFrameSource(string ffmpegPath = "ffmpeg")
{
    public string FfmpegPath { get; } = ffmpegPath;

    /// <summary>True when the configured ffmpeg executable can be started.</summary>
    public bool IsAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(FfmpegPath, "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (p is null) return false;
            p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Yields every sampled frame of <paramref name="videoPath"/> as a new byte array.</summary>
    public IEnumerable<byte[]> ReadFrames(string videoPath, MotionOptions options)
    {
        var psi = new ProcessStartInfo(FfmpegPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        string filter = string.Create(CultureInfo.InvariantCulture,
            $"fps={options.SampleFps},scale={options.FrameWidth}:{options.FrameHeight},format=gray");
        foreach (var arg in new[] { "-v", "error", "-nostdin", "-i", videoPath, "-an", "-vf", filter, "-f", "rawvideo", "-" })
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start '{FfmpegPath}'.");
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.BaseStream;
        int frameSize = options.FrameWidth * options.FrameHeight;

        bool completed = false;
        try
        {
            while (true)
            {
                var frame = new byte[frameSize];
                int read = stdout.ReadAtLeast(frame, frameSize, throwOnEndOfStream: false);
                if (read < frameSize) break;
                yield return frame;
            }
            completed = true;
        }
        finally
        {
            // Caller stopped early (or an exception): don't leave ffmpeg running.
            if (!completed && !process.HasExited) process.Kill(entireProcessTree: true);
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"ffmpeg failed (exit code {process.ExitCode}) for '{videoPath}': {stderr.Result.Trim()}");
    }
}
