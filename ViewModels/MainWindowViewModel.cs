using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FT_AlarmFixer.Models;
using FT_AlarmFixer.Services;
using FT_AlarmFixer.Views;

namespace FT_AlarmFixer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly Regex TagParseRegex = new(@"^(?<base>[^\[]+)(\[(?<index>\d+)\])?(\.(?<bit>\d+))?$", RegexOptions.Compiled);
    private static readonly IBrush DropBorderIdle = new SolidColorBrush(Color.Parse("#C7CED6"));
    private static readonly IBrush DropBorderActive = new SolidColorBrush(Color.Parse("#2D6CDF"));
    private static readonly IBrush DropBackgroundIdle = new SolidColorBrush(Color.Parse("#F4F6F9"));
    private static readonly IBrush DropBackgroundActive = new SolidColorBrush(Color.Parse("#EAF1FF"));

    private readonly AlarmParser _parser = new();
    private readonly ExcelExporter _exporter = new();
    private readonly IStorageProvider? _storageProvider;

    public MainWindowViewModel(IStorageProvider? storageProvider)
    {
        _storageProvider = storageProvider;
        InputFiles.CollectionChanged += OnInputFilesChanged;
        Status = "Drop FactoryTalk View XML exports here or click Open.";
    }

    public ObservableCollection<string> InputFiles { get; } = new();

    [ObservableProperty]
    private string? outputPath;

    [ObservableProperty]
    private string status = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private bool isDragOver;

    [ObservableProperty]
    private bool ignoreBlankDescriptions = false;

    [ObservableProperty]
    private int totalRows;

    [ObservableProperty]
    private int exportedRows;

    [ObservableProperty]
    private int skippedRows;

    public bool CanExport => InputFiles.Count > 0
                             && !IsBusy
                             && !string.IsNullOrWhiteSpace(OutputPath);

    public string InputSummary
        => InputFiles.Count switch
        {
            0 => "No files selected.",
            1 => $"1 file selected: {Path.GetFileName(InputFiles[0])}",
            _ => $"{InputFiles.Count} files selected."
        };

    public string DropTitle => IsDragOver ? "Release to add XML files" : "Drop XML exports here";

    public string DropSubtitle => IsDragOver ? "Only .xml files are accepted." : "Drag one or more FactoryTalk View XML exports.";

    public string RowSummary
        => TotalRows == 0
            ? "No rows parsed yet."
            : $"Rows exported: {ExportedRows} / {TotalRows} (skipped {SkippedRows})";

    public IBrush DropBorderBrush => IsDragOver ? DropBorderActive : DropBorderIdle;

    public IBrush DropBackground => IsDragOver ? DropBackgroundActive : DropBackgroundIdle;

    public void AddFiles(IEnumerable<string> paths)
    {
        var added = 0;
        foreach (var path in paths.Where(IsXmlFile))
        {
            if (!InputFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                InputFiles.Add(path);
                added++;
            }
        }

        if (added > 0)
        {
            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                var first = InputFiles[0];
                var directory = Path.GetDirectoryName(first) ?? string.Empty;
                OutputPath = Path.Combine(directory, "Alarm_Tags.xlsx");
            }

            Status = $"Loaded {InputFiles.Count} file(s). Ready to export.";
        }
        else if (paths.Any())
        {
            Status = "No XML files found in the drop selection.";
        }
    }

    [RelayCommand]
    private async Task OpenFilesAsync()
    {
        if (_storageProvider is null)
        {
            Status = "File picker is not available yet.";
            return;
        }

        var files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Open FactoryTalk View XML exports",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("FactoryTalk XML") { Patterns = new List<string> { "*.xml" } }
            }
        });

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .ToArray();

        AddFiles(paths);
        OnPropertyChanged(nameof(InputSummary));
    }

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        if (_storageProvider is null)
        {
            Status = "File picker is not available yet.";
            return;
        }

        var file = await _storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Excel export",
            DefaultExtension = "xlsx",
            SuggestedFileName = string.IsNullOrWhiteSpace(OutputPath)
                ? "Alarm_Tags.xlsx"
                : Path.GetFileName(OutputPath)
        });

        var path = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            OutputPath = path;
            Status = "Export location updated.";
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (!CanExport)
        {
            return;
        }

        IsBusy = true;
        Progress = 0;
        Status = "Parsing XML files...";

        try
        {
            var allRows = new List<AlarmRow>();
            var fileCount = InputFiles.Count;
            var current = 0;

            foreach (var file in InputFiles)
            {
                var rows = await Task.Run(() => _parser.ParseFile(file));
                allRows.AddRange(rows);

                current++;
                Progress = fileCount == 0 ? 1 : (double)current / fileCount;
                Status = $"Parsed {current} of {fileCount} files.";
            }

            TotalRows = allRows.Count;

            var filteredRows = IgnoreBlankDescriptions
                ? allRows.Where(row => !string.IsNullOrWhiteSpace(row.Description)).ToList()
                : allRows;

            SkippedRows = TotalRows - filteredRows.Count;

            var orderedRows = filteredRows
                .OrderBy(row => TagSortKey(row.Tag), TagKeyComparer.Instance)
                .ToList();

            ExportedRows = orderedRows.Count;
            Status = $"Writing {ExportedRows} rows to Excel...";
            await Task.Run(() => _exporter.Export(OutputPath!, orderedRows));

            Status = $"Exported {ExportedRows} of {TotalRows} rows to {OutputPath}";
        }
        catch (Exception ex)
        {
            Status = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            Progress = 1;
            OnPropertyChanged(nameof(CanExport));
        }
    }

    [RelayCommand]
    private void ClearFiles()
    {
        InputFiles.Clear();
        Progress = 0;
        Status = "Cleared files. Drop more XML exports to continue.";
        TotalRows = 0;
        ExportedRows = 0;
        SkippedRows = 0;
        OnPropertyChanged(nameof(InputSummary));
    }

    [RelayCommand]
    private void OpenOptions()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var window = new OptionsWindow
            {
                DataContext = this
            };
            window.Show();
        });
    }

    [RelayCommand]
    private void OpenAbout()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var window = new AboutWindow();
            window.Show();
        });
    }

    private void OnInputFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(InputSummary));
    }

    partial void OnOutputPathChanged(string? value)
    {
        OnPropertyChanged(nameof(CanExport));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanExport));
    }

    partial void OnIsDragOverChanged(bool value)
    {
        OnPropertyChanged(nameof(DropBorderBrush));
        OnPropertyChanged(nameof(DropBackground));
        OnPropertyChanged(nameof(DropTitle));
        OnPropertyChanged(nameof(DropSubtitle));
    }

    partial void OnTotalRowsChanged(int value)
    {
        OnPropertyChanged(nameof(RowSummary));
    }

    partial void OnExportedRowsChanged(int value)
    {
        OnPropertyChanged(nameof(RowSummary));
    }

    partial void OnSkippedRowsChanged(int value)
    {
        OnPropertyChanged(nameof(RowSummary));
    }

    private static bool IsXmlFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase)
               && File.Exists(path);
    }

    private static TagKey TagSortKey(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return new TagKey(string.Empty, int.MaxValue, int.MaxValue, string.Empty);
        }

        var match = TagParseRegex.Match(tag.Trim());
        if (!match.Success)
        {
            return new TagKey(tag, int.MaxValue, int.MaxValue, tag);
        }

        var baseName = match.Groups["base"].Value;
        var index = match.Groups["index"].Success ? int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture) : int.MaxValue;
        var bit = match.Groups["bit"].Success ? int.Parse(match.Groups["bit"].Value, CultureInfo.InvariantCulture) : int.MaxValue;

        return new TagKey(baseName, index, bit, tag);
    }

    private readonly record struct TagKey(string BaseName, int Index, int Bit, string Raw);

    private sealed class TagKeyComparer : IComparer<TagKey>
    {
        public static TagKeyComparer Instance { get; } = new();

        public int Compare(TagKey x, TagKey y)
        {
            var baseCompare = string.Compare(x.BaseName, y.BaseName, StringComparison.OrdinalIgnoreCase);
            if (baseCompare != 0)
            {
                return baseCompare;
            }

            var indexCompare = x.Index.CompareTo(y.Index);
            if (indexCompare != 0)
            {
                return indexCompare;
            }

            var bitCompare = x.Bit.CompareTo(y.Bit);
            if (bitCompare != 0)
            {
                return bitCompare;
            }

            return string.Compare(x.Raw, y.Raw, StringComparison.OrdinalIgnoreCase);
        }
    }
}
