using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VideoConverter.ViewModels;

namespace VideoConverter.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CancelAllConversions();
        }
        base.OnClosing(e);
    }

    // 選擇多個檔案
    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "選擇要轉檔的影片或音訊檔 (可複選)",
            AllowMultiple = true, // 允許複選檔案
            FileTypeFilter = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("影音檔案")
                {
                    Patterns = new[] { "*.avi", "*.3gp", "*.flv", "*.wmv", "*.webm", "*.mov", "*.mp3", "*.mkv", "*.mp4" }
                },
                new Avalonia.Platform.Storage.FilePickerFileType("所有檔案")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        if (files.Count > 0 && DataContext is MainWindowViewModel vm)
        {
            foreach (var file in files)
            {
                vm.AddFileToQueue(file.Path.LocalPath);
            }
        }
    }

    // 選擇輸出資料夾
    private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "選擇輸出資料夾",
            AllowMultiple = false
        });

        if (folders.Count > 0 && DataContext is MainWindowViewModel vm)
        {
            vm.OutputFolderPath = folders[0].Path.LocalPath;
        }
    }
}