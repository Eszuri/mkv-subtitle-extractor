using System.Configuration;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace MksStudio.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent WPF from shutting down before async window creation
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Ensure Windows Explorer "Export Subtitle" context menu is registered
        Services.ShellIntegrationService.RegisterContextMenu();

        // Check if launched for quick subtitle export
        string? mkvTarget = null;
        if (e.Args.Length >= 2 && (e.Args[0].Equals("--quick-export", StringComparison.OrdinalIgnoreCase) || e.Args[0].Equals("-e", StringComparison.OrdinalIgnoreCase)))
        {
            mkvTarget = e.Args[1];
        }
        else if (e.Args.Length >= 1 && e.Args[0].StartsWith("--quick-export=", StringComparison.OrdinalIgnoreCase))
        {
            mkvTarget = e.Args[0].Substring("--quick-export=".Length).Trim('"', '\'');
        }

        if (!string.IsNullOrWhiteSpace(mkvTarget))
        {
            await HandleQuickExportAsync(mkvTarget);
            return;
        }

        // Check if launched for quick subtitle translate
        string? translateTarget = null;
        if (e.Args.Length >= 2 && (e.Args[0].Equals("--quick-translate", StringComparison.OrdinalIgnoreCase) || e.Args[0].Equals("--translate", StringComparison.OrdinalIgnoreCase) || e.Args[0].Equals("-t", StringComparison.OrdinalIgnoreCase)))
        {
            translateTarget = e.Args[1];
        }
        else if (e.Args.Length >= 1 && (e.Args[0].StartsWith("--quick-translate=", StringComparison.OrdinalIgnoreCase) || e.Args[0].StartsWith("--translate=", StringComparison.OrdinalIgnoreCase)))
        {
            int eq = e.Args[0].IndexOf('=');
            translateTarget = e.Args[0].Substring(eq + 1).Trim('"', '\'');
        }

        if (!string.IsNullOrWhiteSpace(translateTarget))
        {
            await HandleQuickTranslateAsync(translateTarget);
            return;
        }

        // Standard Launch: Open Main Application Window
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Closed += (_, _) => Shutdown();
        mainWindow.Show();
    }

    private async Task HandleQuickExportAsync(string mkvPath)
    {
        if (!File.Exists(mkvPath))
        {
            MessageBox.Show(
                $"File video tidak ditemukan:\n\"{mkvPath}\"",
                "Export Subtitle",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        try
        {
            var mergeService = new Core.MkvToolNix.MkvMergeService();
            var info = await mergeService.IdentifyAsync(mkvPath);
            var subTracks = info.SubtitleTracks.ToList();

            if (subTracks.Count == 0)
            {
                MessageBox.Show(
                    $"Tidak ada track subtitle pada file .mkv ini:\n\"{Path.GetFileName(mkvPath)}\"",
                    "Export Subtitle",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            var exportWindow = new Views.QuickExportWindow(mkvPath, subTracks);
            MainWindow = exportWindow;
            exportWindow.Closed += (_, _) => Shutdown();
            exportWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Gagal membaca informasi track MKV:\n{ex.Message}",
                "Export Subtitle - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private async Task HandleQuickTranslateAsync(string subtitlePath)
    {
        if (!File.Exists(subtitlePath))
        {
            MessageBox.Show(
                $"File subtitle tidak ditemukan:\n\"{subtitlePath}\"",
                "Translate Subtitle",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        try
        {
            string content = await File.ReadAllTextAsync(subtitlePath, Encoding.UTF8);
            var doc = Core.Subtitles.SubtitleFormatRouter.Parse(content, subtitlePath);

            if (doc.Cues.Count == 0)
            {
                MessageBox.Show(
                    $"Tidak ada baris subtitle yang ditemukan pada file ini:\n\"{Path.GetFileName(subtitlePath)}\"",
                    "Translate Subtitle",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            var translateWindow = new Views.QuickTranslateWindow(subtitlePath, doc);
            MainWindow = translateWindow;
            translateWindow.Closed += (_, _) => Shutdown();
            translateWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Gagal membaca file subtitle:\n{ex.Message}",
                "Translate Subtitle - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("DispatcherUnhandledException", e.Exception);
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nCheck crash.log for full details.",
            "MKS Studio - Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException("AppDomainUnhandledException", ex);
            MessageBox.Show(
                $"A critical error occurred:\n\n{ex.Message}\n\nCheck crash.log for full details.",
                "MKS Studio - Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static void LogException(string source, Exception ex)
    {
        try
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MksStudio");
            Directory.CreateDirectory(appData);
            var logPath = Path.Combine(appData, "crash.log");

            var sb = new StringBuilder();
            sb.AppendLine($"==================== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====================");
            sb.AppendLine($"Source: {source}");
            sb.AppendLine($"Exception: {ex.GetType().FullName}: {ex.Message}");
            sb.AppendLine($"StackTrace:\n{ex.StackTrace}");
            if (ex.InnerException != null)
            {
                sb.AppendLine($"InnerException: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                sb.AppendLine($"InnerStackTrace:\n{ex.InnerException.StackTrace}");
            }
            sb.AppendLine();

            File.AppendAllText(logPath, sb.ToString());
        }
        catch
        {
            // Ignore logging failures
        }
    }
}


