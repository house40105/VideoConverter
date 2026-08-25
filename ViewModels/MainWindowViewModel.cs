using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoConverter.Models;

namespace VideoConverter.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private CancellationTokenSource? _cts;

    // 工作佇列集合
    public ObservableCollection<ConversionItem> ConversionQueue { get; } = new();

    // 併發任務數控管 (預設 1 個)
    [ObservableProperty] private int _maxConcurrentTasks = 1;

    // 介面控制與全域狀態
    [ObservableProperty] private string _outputFolderPath = "";
    [ObservableProperty] private double _crfValue = 23;
    [ObservableProperty] private string _globalStatusMessage = "請點選 [新增檔案] 開始建立轉檔佇列。";
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

    public IAsyncRelayCommand StartQueueCommand { get; }
    public IRelayCommand CancelConversionCommand { get; }
    public IRelayCommand ClearQueueCommand { get; }
    public IRelayCommand<ConversionItem> RemoveItemCommand { get; }

    public MainWindowViewModel()
    {
        StartQueueCommand = new AsyncRelayCommand(StartQueueConversionAsync);
        CancelConversionCommand = new RelayCommand(CancelAllConversions);
        ClearQueueCommand = new RelayCommand(ClearQueue);
        RemoveItemCommand = new RelayCommand<ConversionItem>(RemoveQueueItem);
    }

    // 加入檔案至佇列
    public void AddFileToQueue(string filePath)
    {
        if (ConversionQueue.Any(x => x.InputFilePath == filePath)) return;

        FileInfo fileInfo = new FileInfo(filePath);

        if (fileInfo.Length < 1024)
        {
            GlobalStatusMessage = $"已跳過無效或損毀的檔案：{fileInfo.Name}";
            return;
        }

        var item = new ConversionItem
        {
            InputFilePath = filePath,
            Status = ConversionStatus.Waiting,
            StatusMessage = "等待中"
        };

        ConversionQueue.Add(item);
        GlobalStatusMessage = $"佇列中共有 {ConversionQueue.Count} 個檔案。";
    }

    private void RemoveQueueItem(ConversionItem? item)
    {
        if (item != null && !IsConverting)
        {
            ConversionQueue.Remove(item);
            GlobalStatusMessage = $"佇列中共有 {ConversionQueue.Count} 個檔案。";
        }
    }

    private void ClearQueue()
    {
        if (!IsConverting)
        {
            ConversionQueue.Clear();
            GlobalStatusMessage = "佇列已清空。";
        }
    }

    public void CancelAllConversions()
    {
        _cts?.Cancel();
    }

    // 啟動佇列處理邏輯
    private async Task StartQueueConversionAsync()
    {
        var waitingItems = ConversionQueue.Where(x => x.Status == ConversionStatus.Waiting || x.Status == ConversionStatus.Failed || x.Status == ConversionStatus.Canceled).ToList();
        if (waitingItems.Count == 0)
        {
            GlobalStatusMessage = "目前沒有等待轉檔的任務。";
            return;
        }

        IsConverting = true;
        _cts = new CancellationTokenSource();
        SemaphoreSlim semaphore = new SemaphoreSlim(MaxConcurrentTasks, MaxConcurrentTasks);

        GlobalStatusMessage = $"開始執行佇列，同時平行任務數：{MaxConcurrentTasks}";

        List<Task> tasks = new List<Task>();

        foreach (var item in waitingItems)
        {
            // 設定該任務的轉檔參數快照
            PrepareItemParameters(item);

            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(_cts.Token);
                try
                {
                    if (_cts.Token.IsCancellationRequested) return;
                    await ProcessSingleItemAsync(item, _cts.Token);
                }
                finally
                {
                    semaphore.Release();
                }
            }, _cts.Token));
        }

        try
        {
            await Task.WhenAll(tasks);
            GlobalStatusMessage = "所有佇列任務已執行完畢！";
        }
        catch (OperationCanceledException)
        {
            GlobalStatusMessage = "批量轉檔已手動停止。";
        }
        finally
        {
            IsConverting = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void PrepareItemParameters(ConversionItem item)
    {
        FileInfo fileInfo = new FileInfo(item.InputFilePath);
        double fileSizeGb = fileInfo.Length / (1024.0 * 1024.0 * 1024.0);

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
                    codecDisplayName = $"H.265 (大檔)";
                }
                else
                {
                    targetCodec = "libx264";
                    codecDisplayName = $"H.264";
                }
                break;
        }

        item.TargetCodec = targetCodec;
        item.CodecDisplayName = codecDisplayName;
        item.CrfValue = CrfValue;

        if (targetCodec == "libsvtav1")
        {
            string[] av1PresetOptions = { "12", "10", "8", "6", "4" };
            item.TargetPreset = av1PresetOptions[Math.Clamp(SelectedPresetIndex, 0, 4)];
        }
        else
        {
            string[] presetOptions = { "ultrafast", "fast", "medium", "slow", "veryslow" };
            item.TargetPreset = presetOptions[Math.Clamp(SelectedPresetIndex, 0, 4)];
        }

        string[] audioBitrateOptions = { "128k", "192k", "320k" };
        item.TargetAudioBitrate = audioBitrateOptions[Math.Clamp(SelectedAudioBitrateIndex, 0, 2)];

        // 檔名與輸出路徑計算
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(item.InputFilePath);
        if (EnableFileNameSuffix && !string.IsNullOrWhiteSpace(FileNameSuffixText))
        {
            fileNameWithoutExt += FileNameSuffixText;
        }

        string targetFolder = string.IsNullOrWhiteSpace(OutputFolderPath)
            ? Path.GetDirectoryName(item.InputFilePath)!
            : OutputFolderPath;

        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

        string outputFilePath = Path.Combine(targetFolder, $"{fileNameWithoutExt}.mp4");
        if (string.Equals(outputFilePath, item.InputFilePath, StringComparison.OrdinalIgnoreCase))
        {
            outputFilePath = Path.Combine(targetFolder, $"{fileNameWithoutExt}_compressed.mp4");
        }

        item.OutputFilePath = outputFilePath;
    }

    private async Task ProcessSingleItemAsync(ConversionItem item, CancellationToken token)
    {
        if (File.Exists(item.OutputFilePath))
        {
            item.Status = ConversionStatus.Failed;
            item.StatusMessage = "⚠️ 檔名已存在，已跳過";
            return;
        }

        item.Status = ConversionStatus.Converting;
        item.StatusMessage = $"轉檔中 [{item.CodecDisplayName}]...";
        item.ProgressValue = 0;

        string ffmpegExecutable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe")
            : "ffmpeg";

        TimeSpan totalDuration = TimeSpan.Zero;

        try
        {
            var cmd = Cli.Wrap(ffmpegExecutable)
                .WithArguments(args =>
                {
                    args.Add("-i").Add(item.InputFilePath)
                        .Add("-c:v").Add(item.TargetCodec)
                        .Add("-crf").Add(item.CrfValue.ToString())
                        .Add("-preset").Add(item.TargetPreset);

                    if (item.TargetCodec == "libsvtav1")
                    {
                        args.Add("-pix_fmt").Add("yuv420p");
                    }

                    args.Add("-c:a").Add("aac")
                        .Add("-b:a").Add(item.TargetAudioBitrate)
                        .Add("-y")
                        .Add(item.OutputFilePath);
                });

            await foreach (var cmdEvent in cmd.ListenAsync(token))
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
                        item.ProgressValue = Math.Min(100, Math.Max(0, progress));
                        item.StatusMessage = $"轉檔中 ({item.ProgressValue:F0}%)";
                    }
                }
            }

            item.ProgressValue = 100;
            item.Status = ConversionStatus.Completed;
            item.StatusMessage = "✅ 轉檔完成";
        }
        catch (OperationCanceledException)
        {
            item.Status = ConversionStatus.Canceled;
            item.StatusMessage = "⏹️ 已取消";
            item.ProgressValue = 0;
            if (File.Exists(item.OutputFilePath))
            {
                try { File.Delete(item.OutputFilePath); } catch { }
            }
        }
        catch (Exception ex)
        {
            item.Status = ConversionStatus.Failed;
            item.StatusMessage = $"❌ 失敗：{ex.Message}";
        }
    }
}