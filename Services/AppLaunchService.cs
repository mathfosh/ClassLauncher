using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ClassLauncher.Models;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using SysBitmap = System.Drawing.Bitmap;
using SysIcon = System.Drawing.Icon;

namespace ClassLauncher.Services;

public class AppLaunchService : IHostedService
{
    private readonly ILessonsService _lessonsService;
    private readonly Settings _settings;
    private readonly ILogger<AppLaunchService> _logger;
    private bool _isPrompting;
    private bool _hasTriggeredPreClass;

    public AppLaunchService(ILessonsService lessonsService, Plugin plugin, ILogger<AppLaunchService> logger)
    {
        _lessonsService = lessonsService;
        _settings = plugin.Settings;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lessonsService.OnClass += OnClass;
        _lessonsService.PostMainTimerTicked += OnPostMainTimerTicked;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _lessonsService.OnClass -= OnClass;
        _lessonsService.PostMainTimerTicked -= OnPostMainTimerTicked;
        return Task.CompletedTask;
    }

    private async void OnClass(object? sender, EventArgs e)
    {
        _hasTriggeredPreClass = false;

        if (_isPrompting)
            return;

        _isPrompting = true;
        try
        {
            var rootWindow = AppBase.Current.GetRootWindow();
            if (rootWindow == null)
            {
                _logger.LogWarning("无法获取主窗口，跳过上课提示");
                return;
            }

            foreach (var app in _settings.Apps)
            {
                if (app.AdvanceMinutes > 0) // 提前提示的由定时器处理
                    continue;
                await PromptAppIfNeeded(rootWindow, app);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上课提示处理失败");
        }
        finally
        {
            _isPrompting = false;
        }
    }

    private async void OnPostMainTimerTicked(object? sender, EventArgs e)
    {
        if (_isPrompting || _hasTriggeredPreClass)
            return;

        var leftTime = _lessonsService.OnClassLeftTime;
        if (leftTime <= TimeSpan.Zero)
            return;

        // 检查是否有需要提前提示的软件
        var appsToPrompt = _settings.Apps
            .Where(a => a.AdvanceMinutes > 0 && leftTime.TotalMinutes <= a.AdvanceMinutes)
            .ToList();

        if (appsToPrompt.Count == 0)
            return;

        _hasTriggeredPreClass = true;
        _isPrompting = true;
        try
        {
            var rootWindow = AppBase.Current.GetRootWindow();
            if (rootWindow == null)
            {
                _logger.LogWarning("无法获取主窗口，跳过提前提示");
                return;
            }

            foreach (var app in appsToPrompt)
            {
                await PromptAppIfNeeded(rootWindow, app);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提前提示处理失败");
        }
        finally
        {
            _isPrompting = false;
        }
    }

    private async Task PromptAppIfNeeded(TopLevel rootWindow, AppEntry app)
    {
        if (string.IsNullOrWhiteSpace(app.Path))
            return;

        // 课程过滤
        if (!string.IsNullOrWhiteSpace(app.CourseNames))
        {
            var currentSubjectName = _lessonsService.CurrentSubject?.Name;
            if (string.IsNullOrEmpty(currentSubjectName))
                return;

            var courseNames = app.CourseNames
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim());
            if (!courseNames.Contains(currentSubjectName, StringComparer.OrdinalIgnoreCase))
                return;
        }

        var shouldOpen = await ShowConfirmDialog(rootWindow, app);
        if (shouldOpen)
        {
            LaunchApp(app);
        }
    }

    private static async Task<bool> ShowConfirmDialog(TopLevel rootWindow, AppEntry app)
    {
        var content = new StackPanel
        {
            Spacing = 12,
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 提取并显示 exe 图标
        var icon = ExtractIcon(app.Path);
        if (icon != null)
        {
            content.Children.Add(new Image
            {
                Source = icon,
                Width = 32,
                Height = 32
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = $"是否要打开「{app.Name}」？",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 14
        });

        var dialog = new TaskDialog
        {
            Title = "上课提示",
            Content = content,
            Buttons =
            {
                new TaskDialogButton("是", true) { IsDefault = true },
                new TaskDialogButton("否", false)
            },
            XamlRoot = rootWindow
        };

        var result = await dialog.ShowAsync();
        return result is true;
    }

    private static AvaloniaBitmap? ExtractIcon(string exePath)
    {
        try
        {
            using var icon = SysIcon.ExtractAssociatedIcon(exePath);
            if (icon == null)
                return null;

            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            return new AvaloniaBitmap(ms);
        }
        catch (Exception)
        {
            // 图标提取失败是正常情况（如路径不存在），静默返回 null
            return null;
        }
    }

    private void LaunchApp(AppEntry app)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = app.Path,
                Arguments = app.Arguments ?? "",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动应用失败: {AppName}", app.Name);
            var rootWindow = AppBase.Current.GetRootWindow();
            if (rootWindow != null)
            {
                _ = ShowErrorDialog(rootWindow, app.Name, ex.Message);
            }
        }
    }

    private static async Task ShowErrorDialog(TopLevel rootWindow, string appName, string message)
    {
        var dialog = new TaskDialog
        {
            Title = "打开失败",
            Content = $"无法打开「{appName}」：{message}",
            XamlRoot = rootWindow
        };
        await dialog.ShowAsync();
    }
}