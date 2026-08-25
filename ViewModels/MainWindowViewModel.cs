using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VideoConverter.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private CancellationTokenSource? _cts;

    // 主頁面屬性
    [ObservableProperty] private string _inputFilePath = "";
    [ObservableProperty] private string _outputFolderPath = "";
    [ObservableProperty] private double _crfValue = 23;
    [ObservableProperty] private double _progressValue = 0;
    [ObservableProperty] private string _statusMessage = "準備就緒";

    // 控制是否正在轉檔 (控制取消按鈕的顯示/隱藏與控制項禁用)
    [ObservableProperty] private bool _isConverting = false;

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsAutoModeSelected))]
    private int _selectedCodecIndex = 0;

    [ObservableProperty] private decimal _autoThresholdGb = 3.0m;
    public bool IsAutoModeSelected => SelectedCodecIndex == 3;

    // 設定頁面屬性
    [ObservableProperty] private bool _enableFileNameSuffix = true;
    [ObservableProperty] private string _fileNameSuffixText = "_compressed";
    [ObservableProperty] private int _selectedPresetIndex = 2;
    [ObservableProperty] private int _selectedAudioBitrateIndex = 1;

    public IAsyncRelayCommand StartConversionCommand { get; }
    public IRelayCommand CancelConversionCommand { get; }

    public MainWindowViewModel()
    {
        StartConversionCommand = new AsyncRelayCommand(ConvertVideoAsync);
        CancelConversionCommand = new RelayCommand(CancelConversion);
    }

    // 呼叫此方法將取消背景 FFmpeg 進程
    public void CancelConversion()
    {
        _cts?.Cancel();
    }

    private async Task ConvertVideoAsync()
    {
        if (string.IsNullOrWhiteSpace(InputFilePath) || !File.Exists(InputFilePath))
        {
            StatusMessage = "警告：請先選擇有效的來源影片檔案！";
            return;
        }

        FileInfo fileInfo = new FileInfo(InputFilePath);
        double fileSizeGb = fileInfo.Length / (1024.0 * 1024.0 * 1024.0);

        // 1. 判斷 Codec 種類
        string targetCodec = "libx264";
        string codecDisplayName = "H.264";

        switch (SelectedCodecIndex)
        {
            case 0: targetCodec = "libx264"; codecDisplayName = "H.264"; break;
            case 1: targetCodec = "libx265"; codecDisplayName = "H.265"; break;
            case 2: targetCodec = "libsvtav1"; codecDisplayName = "AV1"; break;
            case 3:
                if (fileSizeGb >= (double)AutoThresholdGb)
                {
                    targetCodec = "libx265";
                    codecDisplayName = $"H.265 (原檔 {fileSizeGb:F2}GB ≥ 門檻 {AutoThresholdGb}GB)";
                }
                else
                {
                    targetCodec = "libx264";
                    codecDisplayName = $"H.264 (原檔 {fileSizeGb:F2}GB < 門檻 {AutoThresholdGb}GB)";
                }
                break;
        }

        // 2. 計算檔名標籤 (FLAG) 與輸出路徑
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(InputFilePath);
        
        if (EnableFileNameSuffix && !string.IsNullOrWhiteSpace(FileNameSuffixText))
        {
            fileNameWithoutExt += FileNameSuffixText;
        }

        string targetFolder = string.IsNullOrWhiteSpace(OutputFolderPath)
            ? Path.GetDirectoryName(InputFilePath)!
            : OutputFolderPath;

        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        string outputFilePath = Path.Combine(targetFolder, $"{fileNameWithoutExt}.mp4");

        if (string.Equals(outputFilePath, InputFilePath, StringComparison.OrdinalIgnoreCase))
        {
            outputFilePath = Path.Combine(targetFolder, $"{fileNameWithoutExt}_compressed.mp4");
        }

        if (File.Exists(outputFilePath))
        {
            StatusMessage = $"⚠️ 警告： [{Path.GetFileName(outputFilePath)}] 已存在！請先移除舊檔或修改檔名。";
            return;
        }

        // 3. Preset 與 AV1 專屬適配處理
        string targetPreset;
        if (targetCodec == "libsvtav1")
        {
            string[] av1PresetOptions = { "12", "10", "8", "6", "4" };
            targetPreset = av1PresetOptions[Math.Clamp(SelectedPresetIndex, 0, 4)];
        }
        else
        {
            string[] presetOptions = { "ultrafast", "fast", "medium", "slow", "veryslow" };
            targetPreset = presetOptions[Math.Clamp(SelectedPresetIndex, 0, 4)];
        }

        string[] audioBitrateOptions = { "128k", "192k", "320k" };
        string targetAudioBitrate = audioBitrateOptions[Math.Clamp(SelectedAudioBitrateIndex, 0, 2)];

        string ffmpegExecutable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe")
            : "ffmpeg";

        StatusMessage = $"使用 [{codecDisplayName}] 開始轉檔...";
        ProgressValue = 0;

        // 進入轉檔狀態 (顯示取消按鈕，隱藏轉換按鈕，並鎖定調整輸入框)
        IsConverting = true;
        _cts = new CancellationTokenSource();

        TimeSpan totalDuration = TimeSpan.Zero;

        try
        {
            var cmd = Cli.Wrap(ffmpegExecutable)
                .WithArguments(args =>
                {
                    args.Add("-i").Add(InputFilePath)
                        .Add("-c:v").Add(targetCodec)
                        .Add("-crf").Add(CrfValue.ToString())
                        .Add("-preset").Add(targetPreset);

                    if (targetCodec == "libsvtav1")
                    {
                        args.Add("-pix_fmt").Add("yuv420p");
                    }

                    args.Add("-c:a").Add("aac")
                        .Add("-b:a").Add(targetAudioBitrate)
                        .Add("-y")
                        .Add(outputFilePath);
                });

            await foreach (var cmdEvent in cmd.ListenAsync(_cts.Token))
            {
                if (cmdEvent is StandardErrorCommandEvent stdErr)
                {
                    string text = stdErr.Text;

                    if (totalDuration == TimeSpan.Zero)
                    {
                        var matchDuration = Regex.Match(text, @"Duration:\s*(\d+):(\d+):(\d+\.\d+)");
                        if (matchDuration.Success)
                        {
                            int h = int.Parse(matchDuration.Groups[1].Value);
                            int m = int.Parse(matchDuration.Groups[2].Value);
                            double s = double.Parse(matchDuration.Groups[3].Value);
                            totalDuration = new TimeSpan(0, h, m, (int)s, (int)((s - (int)s) * 1000));
                        }
                    }

                    var matchTime = Regex.Match(text, @"time=\s*(\d+):(\d+):(\d+\.\d+)");
                    if (matchTime.Success && totalDuration.TotalSeconds > 0)
                    {
                        int h = int.Parse(matchTime.Groups[1].Value);
                        int m = int.Parse(matchTime.Groups[2].Value);
                        double s = double.Parse(matchTime.Groups[3].Value);
                        var currentTime = new TimeSpan(0, h, m, (int)s, (int)((s - (int)s) * 1000));

                        double progress = (currentTime.TotalSeconds / totalDuration.TotalSeconds) * 100;
                        ProgressValue = Math.Min(100, Math.Max(0, progress));
                    }
                }
            }

            ProgressValue = 100;
            StatusMessage = $"轉檔成功！已儲存至：{Path.GetFileName(outputFilePath)}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "轉檔已手動取消/中斷。";
            ProgressValue = 0;
            if (File.Exists(outputFilePath))
            {
                try { File.Delete(outputFilePath); } catch { }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"轉檔失敗：{ex.Message}";
        }
        finally
        {
            // 離開轉檔狀態 (隱藏取消按鈕，恢復顯示開始按鈕與解鎖輸入框)
            IsConverting = false;
            _cts?.Dispose();
            _cts = null;
        }
    }
}