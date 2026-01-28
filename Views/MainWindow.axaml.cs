using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Linq;
using Avalonia.Platform.Storage;
using FT_AlarmFixer.ViewModels;

namespace FT_AlarmFixer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ConfigureDragDrop();
        ConfigureTitleBar();
    }

    private void ConfigureDragDrop()
    {
        var dropZone = this.FindControl<Border>("DropZone");
        if (dropZone is null)
        {
            return;
        }

        dropZone.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        dropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        dropZone.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        dropZone.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void ConfigureTitleBar()
    {
        var titleBar = this.FindControl<Border>("TitleBar");
        if (titleBar is null)
        {
            return;
        }

        titleBar.AddHandler(PointerPressedEvent, OnTitleBarPointerPressed, handledEventsToo: true);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Source is Control control)
        {
            var current = control;
            while (current is not null)
            {
                if (current is Button)
                {
                    return;
                }

                current = current.Parent as Control;
            }
        }

        BeginMoveDrag(e);
        e.Handled = true;
    }

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var hasXml = HasXmlFiles(e);
        vm.IsDragOver = hasXml;
        e.DragEffects = hasXml ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var hasXml = HasXmlFiles(e);
        vm.IsDragOver = hasXml;
        e.DragEffects = hasXml ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsDragOver = false;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        vm.IsDragOver = false;

        var paths = GetXmlPaths(e);
        if (paths.Length == 0)
        {
            return;
        }

        vm.AddFiles(paths);
        e.Handled = true;
    }

    private static string[] GetXmlPaths(DragEventArgs e)
    {
        var files = e.DataTransfer?.TryGetFiles();
        if (files is null)
        {
            return [];
        }

        return files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrWhiteSpace(p) && p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Select(p => p!)
            .ToArray();
    }

    private static bool HasXmlFiles(DragEventArgs e) => GetXmlPaths(e).Length > 0;
}
