# mp4analyzer

[![CI](https://github.com/rogerbriggen/mp4analyzer/actions/workflows/ci.yml/badge.svg)](https://github.com/rogerbriggen/mp4analyzer/actions/workflows/ci.yml)

A command line tool that scans a folder of `.mp4` recordings (e.g. from a surveillance camera) and
**deletes every video without movement**. The camera's timestamp in the **top-right corner** changes
every second and is ignored, so a video in which only the clock ticks counts as "no movement".

A **dry run** deletes nothing and only prints which videos have movement and which do not.

Built on .NET 10. Videos are decoded with [ffmpeg](https://ffmpeg.org), which must be installed.

---

## For end users

### 1. Install ffmpeg

mp4analyzer runs `ffmpeg` to decode the videos (H.264, HEVC, ... whatever your ffmpeg supports).

```bash
sudo apt install ffmpeg        # Debian/Ubuntu
winget install ffmpeg          # Windows (or: choco install ffmpeg)
brew install ffmpeg            # macOS
```

If `ffmpeg` is not on `PATH`, pass its location with `--ffmpeg <path>`.

### 2. Build or publish

```bash
dotnet build -c Release
# or a self-contained executable you can copy anywhere:
dotnet publish mp4analyzer/mp4analyzer.csproj -c Release -r win-x64   # or linux-x64, osx-arm64, ...
```

### 3. Run

```bash
# See what would happen - nothing is deleted:
mp4analyzer /path/to/videos --dry-run

# Delete all videos without movement:
mp4analyzer /path/to/videos

# Include sub folders and show per-video statistics:
mp4analyzer /path/to/videos --recursive --dry-run --verbose
```

Example output (dry run on this repository's `testdata` folder):

```
Analysing 2 video(s) in /workspaces/mp4analyzer/testdata (dry run, nothing is deleted)
  movement  movement/1970-0101-012527.00300033.mp4  [frames=600, motionFrames=219, maxChanged=27.46 %]
  no motion no_movement/1970-0101-014527.00299967.mp4  [frames=600, motionFrames=0, maxChanged=0.00 %]

Videos with movement (1):
  movement/1970-0101-012527.00300033.mp4
Videos without movement (1) - would be deleted:
  no_movement/1970-0101-014527.00299967.mp4
```

Videos that cannot be decoded are reported and **never deleted**.

### Options

| Option | Description |
| --- | --- |
| `<folder>` | Folder containing the `.mp4` files (required). |
| `-n`, `--dry-run` | Do not delete anything; only report the results. |
| `-r`, `--recursive` | Also scan sub folders. |
| `--verbose` | Print per-video statistics (frames, motion frames, max. changed pixels). |
| `--ffmpeg <path>` | Path to the ffmpeg executable (default: `ffmpeg` on `PATH`). |
| `--pixel-threshold <n>` | Gray-level difference (0-255) for a pixel to count as changed. Default `25`. |
| `--changed-fraction <f>` | Fraction of changed pixels for a frame to count as a motion frame. Default `0.005` (0.5 %). |
| `--min-motion-frames <n>` | Motion frames required for a video to "have movement". Default `2`. |
| `--fps <f>` | Frames per second sampled from the video. Default `2`. |
| `--clock-width <f>` | Width of the ignored top-right clock area, as fraction of the frame width. Default `0.25`. |
| `--clock-height <f>` | Height of the ignored top-right clock area, as fraction of the frame height. Default `0.10`. |
| `-v`, `--version` | Print the version. |
| `-h`, `--help` | Show help. |

### Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Success |
| `1` | Usage error (unknown option, missing folder, ...) |
| `2` | Folder not found, or ffmpeg could not be started |
| `3` | At least one video could not be analysed or deleted (it was left untouched) |

---

## How it works

1. ffmpeg decodes each video, samples **2 frames per second**, scales them to **160x90** and converts
   them to 8-bit grayscale (`fps=2,scale=160:90,format=gray`), streamed as raw bytes over stdout.
   Downscaling averages away sensor noise and makes the analysis fast (a 5-minute 1080p HEVC clip
   takes ~2-3 s).
2. Each frame is compared with the previous sampled frame. The **top-right 25 % x 10 %** of the
   picture (the clock) is masked out.
3. A pixel is *changed* when its gray value differs by more than 25; a frame is a *motion frame* when
   at least 0.5 % of the unmasked pixels changed.
4. A video **has movement** when it contains at least 2 motion frames (a single glitch is ignored).

On the test videos the separation is large: 219 of 600 frames are motion frames in the video with
movement, 0 in the one without (even with the clock mask disabled, the clock alone changes only
~0.03 % of the pixels).

---

## For developers

### Layout

```
mp4analyzer/                  # the CLI application
  Program.cs                  # entry point: analyse, delete, print summary
  Cli/                        # CommandLineOptions (parser), ExitCodes
  Analysis/                   # MotionOptions, MotionDetector (pure frame diff),
                              # FfmpegFrameSource (ffmpeg -> raw frames), VideoAnalyzer
mp4analyzer.Tests/            # MSTest unit + regression tests
testdata/
  movement/*.mp4              # videos that MUST be detected as movement
  no_movement/*.mp4           # videos that MUST be detected as no movement
.github/workflows/ci.yml      # CI
```

### Prerequisites

- .NET 10 SDK (pinned via `global.json`) — **or** use the dev container below, which brings
  its own SDK.
- ffmpeg on `PATH` (the dev container installs it).

### Build / run / test

```bash
dotnet build mp4analyzer.slnx -c Release
dotnet test  mp4analyzer.slnx -c Release
dotnet run --project mp4analyzer -- testdata --recursive --dry-run --verbose
```

### Tests

- `MotionDetectorTests` — synthetic frames: static picture, ticking clock in the corner, moving
  object, sensor noise, tiny flicker, single glitch frame.
- `CommandLineOptionsTests` — argument parsing and validation.
- `TestdataRegressionTests` — runs the real detector over **every** `.mp4` in `testdata/movement`
  and `testdata/no_movement` and asserts that the result matches the folder name. It also runs the
  whole program in dry-run mode (nothing deleted, both lists correct) and in real mode on a
  temporary copy (only the `no_movement` videos are deleted).

**Adding a regression case:** drop the video into `testdata/movement/` or `testdata/no_movement/` —
it is picked up automatically.

When ffmpeg is missing the regression tests are reported as *inconclusive*; set
`MP4ANALYZER_REQUIRE_FFMPEG=1` (as CI does) to make them fail instead.

### Dev container

The repo ships a [dev container](https://containers.dev) (`.devcontainer/devcontainer.json`)
so you can develop in an isolated Docker container instead of installing anything on the host.

**Host requirements:** Docker (e.g. Docker Desktop) and VS Code with the
[Dev Containers](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
extension. Open the repo folder and run **“Dev Containers: Reopen in Container”**.

The container is based on the official .NET 10 SDK dev-container image and adds:

- **Tooling:** ffmpeg, git, GitHub CLI (`gh`), Node.js LTS, PowerShell (used by the CI release check).
- **AI CLIs:** Claude Code (`claude`), GitHub Copilot CLI (`copilot`), OpenAI Codex (`codex`).
- **VS Code extensions** (installed automatically *inside* the container): C# Dev Kit, C#,
  EditorConfig, Claude Code, Copilot + Copilot Chat, GitHub Pull Requests, GitHub Actions.

On first create it also runs `dotnet restore` so the NuGet cache is warm.

**One-time logins.** CLI credentials are persisted in fixed-name Docker volumes
(`claude-config`, `gh-config`, `copilot-config`, `codex-config`) mounted into the container,
so you authenticate once and the logins survive rebuilds — and are shared with any other dev
container on the machine that mounts the same volumes:

```bash
claude            # Claude Pro/Max OAuth flow
gh auth login     # GitHub CLI
copilot           # prompts for GitHub auth on first run
codex login       # ChatGPT-account OAuth
```

Git credentials need no setup: VS Code forwards the host's git credential helper (and SSH
agent) into the container automatically.

**Notes**

- The volumes only vanish via an explicit `docker volume rm` / `docker volume prune` —
  rebuilding or deleting the container keeps them.
- Because the auth volumes are shared, any code you run in a container that mounts them can
  read those tokens. For untrusted third-party code, use a container without these mounts.
- Nerdbank.GitVersioning needs full git history — don't use a shallow clone.

### Design notes

- **No video codec in .NET.** Decoding is delegated to the external `ffmpeg` process; the .NET
  side only reads raw grayscale bytes. This keeps the app dependency-free (no NuGet packages apart
  from Nerdbank.GitVersioning) and supports every codec ffmpeg supports.
- **Pure, testable core.** `MotionDetector` works on plain `byte[]` frames and has no I/O, so the
  detection logic is unit-tested with synthetic frames; the ffmpeg integration is covered by the
  `testdata` regression tests.
- **Fail safe.** A video is only deleted after it was decoded successfully and found to have no
  movement. Decoding errors leave the file untouched and set exit code 3.

### CI

`.github/workflows/ci.yml` installs ffmpeg, then builds and tests the solution (including the
`testdata` regression tests):
- **Linux** (`ubuntu-latest`) on every push and pull request — the cheap default.
- **Windows** (`windows-latest`) only on `main`, to exercise Windows without paying the higher
  Windows-minute cost on every PR.
- **`verify-release-version`** runs on `release/*` branches and PRs targeting them, and fails
  the build if `version.json` still has a `-pre` suffix. The check is a `pwsh` step so the
  same script is portable across Windows, macOS, and Linux runners.

### Versioning

The repo uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (NBGV),
driven by `version.json` at the repo root. NBGV stamps `AssemblyVersion`, `FileVersion`, and
`AssemblyInformationalVersion` on every build — there are no version literals in any `.csproj`.

- **Day-to-day** `version.json` reads `"version": "1.0-pre"`, so feature-branch builds produce
  e.g. `1.0.12-pre+abc1234` (height + short commit hash).
- **Releasing** Cut a `release/<x.y>` branch, edit `version.json` to drop the `-pre` suffix
  (e.g. `"version": "1.0"`), and push. The `verify-release-version` CI job will fail the
  branch if you forget. Merge the PR, then tag the merge commit.
- The `publicReleaseRefSpec` in `version.json` lists `main` and `release/*`, so builds on those
  refs omit the `+gitHash` build-metadata suffix.

`mp4analyzer --version` prints e.g. `mp4analyzer 1.0.12-pre+abc1234`.
