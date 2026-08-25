using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoConverter.Models;

public enum ConversionStatus
{
    Waiting,    // 等待中
    Converting, // 轉檔中
    Completed,  // 已完成
    Canceled,   // 已取消
    Failed      // 失敗
}

public partial class ConversionItem : ObservableObject
{
    [ObservableProperty] private string _inputFilePath = "";
    [ObservableProperty] private string _outputFilePath = "";
    [ObservableProperty] private double _progressValue = 0;
    [ObservableProperty] private string _statusMessage = "等待中...";
    [ObservableProperty] private ConversionStatus _status = ConversionStatus.Waiting;

    // 該任務轉檔時使用的參數快照
    public string TargetCodec { get; set; } = "libx264";
    public string CodecDisplayName { get; set; } = "H.264";
    public double CrfValue { get; set; } = 23;
    public string TargetPreset { get; set; } = "medium";
    public string TargetAudioBitrate { get; set; } = "192k";

    public string FileName => System.IO.Path.GetFileName(InputFilePath);
}