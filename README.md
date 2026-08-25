# VideoConverter

[![Release](https://img.shields.io/badge/Release-v2.0-blue.svg)](https://github.com/your-username/VideoConverter/releases/tag/v2.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x86__64-0078D6?logo=windows)](https://github.com/your-username/VideoConverter/releases)
[![Framework](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![UI Framework](https://img.shields.io/badge/AvaloniaUI-11.1.3-3399ff)](https://avaloniaui.net/)
[![Architecture](https://img.shields.io/badge/Architecture-MVVM-informational)](#architecture)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

A high-performance, cross-platform desktop video transcoding and compression application built with **.NET 8**, **Avalonia UI**, and **FFmpeg**. Designed with the **MVVM pattern**, VideoConverter provides an intuitive GUI for batch video processing, hardware/software codec configuration, real-time progress parsing, and dynamic concurrency management.

---

## Download & Quick Start (Standalone Release)

### Latest Release: `VideoConverter_v2.0` (Windows x86_64)

Pre-built standalone single-file executables are available for Windows x64 users under **[Releases](https://github.com/house40105/VideoConverter/releases)**.

#### Quick Start Instructions:
1. **Download & Extract Release**: Download `VideoConverter_win64_v2.0.zip` from the [GitHub Releases](https://github.com/house40105/VideoConverter/releases) page and extract it.
2. **FFmpeg Integration**: `ffmpeg.exe` is bundled directly with the release package. Ensure `ffmpeg.exe` remains in the **same directory** as `VideoConverter_win64_v2.0.exe`.
3. **Run Application**: Double-click `VideoConverter_win64_v2.0.exe` to start converting videos immediately.
   > **Note:** The standalone Windows build is fully self-contained. Installing the .NET Runtime is **not** required.

---

## Key Features

- **Batch Processing Queue**
  - Add multiple video and audio files into a unified execution queue.
  - Granular queue management: individual task removal, full queue clearing, and batch cancellation.
- **Multi-Codec & Adaptive Transcoding**
  - **H.264 (`libx264`)**: Maximum device compatibility.
  - **H.265 / HEVC (`libx265`)**: High compression efficiency.
  - **AV1 (`libsvtav1`)**: Next-generation codec with `yuv420p` pixel format standardization.
  - **Smart Auto-Switching**: Automatically selects codecs based on configurable file size thresholds (e.g., automatically applies H.265/HEVC to files exceeding 3.0 GB).
- **Concurrent Execution & Throttle Control**
  - Managed concurrent task execution (`1` to `8` parallel workers) using `SemaphoreSlim` to balance CPU load and conversion speed.
- **Granular Encoding Parameters**
  - **Constant Rate Factor (CRF)**: Slider control ranging from `18` (high quality) to `32` (high compression).
  - **Encoding Presets**: Support for standard speed/efficiency presets (`ultrafast`, `fast`, `medium`, `slow`, `veryslow`) and SVT-AV1 preset tiers (`12` down to `4`).
  - **Audio Bitrate Control**: AAC audio transcoding with options for `128 kbps`, `192 kbps`, and `320 kbps`.
- **Real-Time Progress & Event Stream Parsing**
  - Non-blocking asynchronous process orchestration via [CliWrap](https://github.com/Tyrrrz/CliWrap).
  - Real-time `stderr` parsing via regex to calculate precise duration and progress percentage per item.
- **Safe Output & Path Management**
  - Custom output directory selection (defaults to source file path).
  - Configurable filename suffixing (e.g., `_compressed`).
  - Overwrite protection: prevents source file collisions by auto-resolving output file names.
- **Cross-Platform Compatibility**
  - Runs on Windows, macOS, and Linux using Avalonia UI 11.
  - Automatic platform-aware resolution of the FFmpeg binary executable.

---

## Tech Stack & Architecture

### Stack Overview
- **Language & Runtime**: C# 12 / .NET 8.0 SDK
- **GUI Framework**: Avalonia UI 11.1.3 (Fluent Theme, Inter Font)
- **MVVM Toolkit**: `CommunityToolkit.Mvvm` 8.2.2 (`ObservableObject`, `ObservableProperty`, `AsyncRelayCommand`)
- **Process Orchestration**: `CliWrap` 3.6.6 (Asynchronous stream listening)
- **Media Engine**: FFmpeg

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                      Avalonia UI                        │
│             (Views / MainWindow.axaml)                  │
└───────────────────────────┬─────────────────────────────┘
                            │ Data Binding & Commands
┌───────────────────────────▼─────────────────────────────┐
│                 MainWindowViewModel                     │
│    (Queue Management, Concurrency Control & State)      │
└──────────────┬───────────────────────────┬──────────────┘
               │                           │
  Updates State│                           │ Orchestrates Execution
               ▼                           ▼
┌─────────────────────────────┐  ┌────────────────────────┐
│       ConversionItem        │  │        CliWrap         │
│  (Data Model & Parameters)  │  │  (FFmpeg CLI Process)  │
└─────────────────────────────┘  └───────────┬────────────┘
                                             │ Stderr Event Stream
                                             ▼
                                 ┌────────────────────────┐
                                 │ Real-time Regex Parser │
                                 └────────────────────────┘
```

---

## Project Structure

```
VideoConverter/
├── Assets/                 # Application icons and visual assets
├── Models/
│   └── ConversionItem.cs   # Observable data model for queue items & encoding options
├── ViewModels/
│   ├── ViewModelBase.cs    # Base ViewModel class inheriting ObservableObject
│   └── MainWindowViewModel.cs # Core business logic, batch queue, and FFmpeg integration
├── Views/
│   ├── MainWindow.axaml    # Desktop XAML UI layout (Transcoding Queue & Advanced Settings)
│   └── MainWindow.axaml.cs # Code-behind file dialog handling and window lifecycle
├── App.axaml               # Global application styles and Fluent theme initialization
├── App.axaml.cs            # Application startup and lifetime handler
├── Program.cs              # Main entry point and Avalonia bootstrapper
├── ViewLocator.cs          # DataTemplate resolution for MVVM view mapping
├── app.manifest            # Windows application manifest
├── LICENSE                 # Apache License 2.0
└── VideoConverter.csproj   # Project file & NuGet package dependencies
```

---

## Prerequisites

- **FFmpeg Engine**:
  - **Windows**: Place `ffmpeg.exe` in the application folder alongside `VideoConverter.exe`, or add `ffmpeg` to your system `PATH`.
  - **Linux / macOS**: Ensure `ffmpeg` is installed and available in system `PATH` (`sudo apt install ffmpeg` / `brew install ffmpeg`).
- **For Building from Source**: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

---

## Building and Running from Source

### 1. Clone the Repository
```bash
git clone https://github.com/your-username/VideoConverter.git
cd VideoConverter
```

### 2. Build the Application
```bash
dotnet build
```

### 3. Run Locally
```bash
dotnet run
```

### 4. Publish Packaging

#### Publish for Windows (Self-Contained / Portable Single File):
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist-windows
```

#### Publish for Linux:
```bash
dotnet publish -c Release -r linux-x64 --self-contained true
```

#### Publish for macOS:
```bash
dotnet publish -c Release -r osx-x64 --self-contained true
```

---

## Usage Guide

1. **Add Files**: On the **Main Page**, click **Add Files...** to select single or multiple video/audio files (`.mp4`, `.mkv`, `.mov`, `.avi`, `.webm`, `.flv`, `.wmv`, `.3gp`, `.mp3`).
2. **Configure Output Path**: Specify a target directory or leave empty to output converted files in the same directory as the source file.
3. **Select Codec & Concurrency**:
   - Choose your preferred target video codec (H.264, H.265, AV1, or Auto Mode).
   - Adjust the **Max Concurrent Tasks** numerical spinner to match your machine's CPU core capacity.
4. **Tune Parameters (Optional)**: Switch to the **Advanced Settings** tab to adjust:
   - File suffix flags (e.g., `_compressed`).
   - Constant Rate Factor (CRF quality level).
   - Encoding Speed Preset (`ultrafast` to `veryslow`).
   - Audio Bitrate (`128 kbps` to `320 kbps`).
5. **Start Conversion**: Click **Start Queue**. Real-time progress bars will display conversion completion percentages. You can cancel active tasks at any time using **Stop Queue**.

---

## License

Distributed under the **Apache License 2.0**. See [`LICENSE`](LICENSE) for more information.
