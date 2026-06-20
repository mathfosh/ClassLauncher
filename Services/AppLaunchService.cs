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
    private readonly List<AppEntry> _autoDisabledThisCycle = new();

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
        _autoDisabledThisCycle.Clear();

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
                if (!app.IsEnabled) // 跳过已禁用的规则
                    continue;
                if (app.AdvanceMinutes > 0)
                    continue;
                await PromptAppIfNeeded(rootWindow, app);
            }

            // 本轮结束后，统一通知自动禁用事件
            if (_autoDisabledThisCycle.Count > 0)
            {
                await NotifyAutoDisabled(rootWindow);
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

        var appsToPrompt = _settings.Apps
            .Where(a => a.IsEnabled && a.AdvanceMinutes > 0 && leftTime.TotalMinutes <= a.AdvanceMinutes)
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

            if (_autoDisabledThisCycle.Count > 0)
            {
                await NotifyAutoDisabled(rootWindow);
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
            HandleLaunchFailure(app);

            var rootWindow = AppBase.Current.GetRootWindow();
            if (rootWindow != null)
            {
                _ = ShowErrorDialog(rootWindow, app.Name, ex.Message);
            }
        }
    }

    /// <summary>
    /// 处理启动失败：增加失败计数，检查是否需要自动禁用。
    /// </summary>
    private void HandleLaunchFailure(AppEntry app)
    {
        if (!_settings.AutoDisableEnabled || !app.AutoDisableOnFail)
            return;

        app.FailCount++;

        if (app.FailCount >= _settings.AutoDisableFailThreshold)
        {
            app.IsEnabled = false;
            app.AutoDisabledAt = DateTime.Now;
            _autoDisabledThisCycle.Add(app);
            _logger.LogWarning("自动禁用规则: {AppName}（失败 {Count} 次，阈值 {Threshold}）",
                app.Name, app.FailCount, _settings.AutoDisableFailThreshold);
        }
    }

    /// <summary>
    /// 通知用户本轮自动禁用了哪些规则。
    /// </summary>
    private async Task NotifyAutoDisabled(TopLevel rootWindow)
    {
        var names = _autoDisabledThisCycle.Select(a => a.Name);
        var dialog = new TaskDialog
        {
            Title = "规则自动禁用",
            Content = $"以下软件启动失败，已自动禁用相关规则：\n\n" +
                      string.Join("\n", names.Select(n => $"  \u2022 {n}")) +
                      $"\n\n可在插件设置中手动重新启用。",
            XamlRoot = rootWindow
        };
        await dialog.ShowAsync();
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