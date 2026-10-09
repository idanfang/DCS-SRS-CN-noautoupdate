using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Octokit;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace AutoUpdater;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public static readonly string GITHUB_USERNAME = Environment.GetCommandLineArgs().Contains("-source=cn") ? "idanfang" : "ciribob";

    public static readonly string GITHUB_REPOSITORY = Environment.GetCommandLineArgs().Contains("-source=cn") ? "DCS-SRS-CN-noautoupdate" : "DCS-SimpleRadioStandalone";

    // Required for all requests against the GitHub API, as per https://developer.github.com/v3/#user-agent-required
    public static readonly string GITHUB_USER_AGENT = $"{GITHUB_USERNAME}_{GITHUB_REPOSITORY}";
    private bool _cancel;
    private string _directory;
    private string _file;

    private bool _finished;
    private double _lastValue = -1;
    private DispatcherTimer _progressCheckTimer;
    private Uri _uri;
    private WebClient _downloadClient;
    private DateTime _lastProgressAt = DateTime.UtcNow;

    private string changelogURL = "";

    public MainWindow()
    {
        InitializeComponent();
        if (!Environment.GetCommandLineArgs().Any(arg => arg.StartsWith("-tag=")))
        {
            MessageBox.Show("请在 SRS“高级设置 → 全局设置”中选择来源并点击更新按钮。", "手动更新");
            Close();
            return;
        }

        if (IsAnotherRunning())
        {
            MessageBox.Show("已有更新器在运行，请关闭另一个更新器后再试。", "SRS 手动更新器",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(0);

            return;
        }

        try
        {
            DownloadLatestVersion();
        }
        catch (Exception exception)
        {
            ShowError();
        }
    }

    private bool IsDCSRunning()
    {
        foreach (var clsProcess in Process.GetProcesses())
            if (clsProcess.ProcessName.ToLower().Trim().Equals("dcs"))
                return true;

        return false;
    }

    private void QuitSimpleRadio()
    {
        foreach (var clsProcess in Process.GetProcesses())
            if (clsProcess.ProcessName.ToLower().Trim().StartsWith("sr-server") ||
                clsProcess.ProcessName.ToLower().Trim().StartsWith("srs-server") ||
                clsProcess.ProcessName.ToLower().Trim().StartsWith("sr-client"))
            {
                clsProcess.Kill();
                clsProcess.WaitForExit(5000);
                clsProcess.Dispose();
            }
    }

    private bool IsAnotherRunning()
    {
        var currentProcess = Process.GetCurrentProcess();
        var currentProcessName = currentProcess.ProcessName.ToLower().Trim();

        foreach (var clsProcess in Process.GetProcesses())
            if (clsProcess.Id != currentProcess.Id &&
                clsProcess.ProcessName.ToLower().Trim() == currentProcessName)
                return true;

        return false;
    }

    private Release FindRightRelease(IReadOnlyList<Release> releases)
    {
        var allowBeta = AllowBeta();
        var allowAlpha = AllowAlpha();

        Release latestAlpha = null;
        Release latestBeta = null;
        Release latestStable = null;
        
        foreach (var release in releases)
        {
            if (release.Prerelease)
            {
                if (release.Name.ToLower().Contains("alpha") && latestAlpha == null)
                {
                    latestAlpha = release;
                }

                if (release.Name.ToLower().Contains("beta") && latestBeta == null)
                {
                    latestBeta = release;
                }
            }
            else
            {
                if (latestStable == null)
                {
                    latestStable = release;
                }
            }

            if (latestAlpha != null && latestBeta != null && latestStable != null)
            {
                break;
            }
        }

        var lastStableVersion = new Version(latestStable?.TagName.Replace("v", "") ?? "0.0.0.0");
        var lastBetaVersion = new Version(latestBeta?.TagName.Replace("v", "") ?? "0.0.0.0");
        var lastAlphaVersion = new Version(latestAlpha?.TagName.Replace("v", "") ?? "0.0.0.0");
        
        if (allowAlpha && latestAlpha != null 
                       && lastAlphaVersion > lastBetaVersion 
            && lastAlphaVersion > lastStableVersion)
        {
            return latestAlpha;
        }
        //allowing alpha gives you beta if its newer
        if ((allowBeta || allowAlpha) && latestBeta != null && lastBetaVersion > lastStableVersion)
        {
            return latestBeta;
        }
            
        return latestStable;
    }
    
    private async Task<Uri> GetPathToLatestVersion()
    {
        Status.Content = "正在查找 SRS 版本";
        var githubClient = new GitHubClient(new ProductHeaderValue(GITHUB_USER_AGENT, "1.0.0.0"));

        var tagArgument = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("-tag="));
        // No implicit latest-version download: the settings screen must select the release.
        if (tagArgument == null) throw new InvalidOperationException("请从高级设置 → 全局设置中发起手动更新。");
        var release = await githubClient.Repository.Release.Get(GITHUB_USERNAME, GITHUB_REPOSITORY, tagArgument.Substring(5));
        var asset = release.Assets.FirstOrDefault(item =>
            item.Name.StartsWith("DCS-SimpleRadioStandalone", StringComparison.OrdinalIgnoreCase)
            && item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        if (asset == null) throw new InvalidOperationException("该版本未提供安装包。");
        changelogURL = release.HtmlUrl;
        Status.Content = "正在下载 " + release.TagName;
        return new Uri(asset.BrowserDownloadUrl);
    }
    private bool AllowBeta()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
            if (arg.Trim().Equals("-beta"))
                return true;

        return false;
    }

    private bool AllowAlpha()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
            if (arg.Trim().Equals("-alpha"))
                return true;

        return false;
    }

    private string ServerPath()
    {
        foreach (var commandLineArg in Environment.GetCommandLineArgs())
            if (commandLineArg.Trim().StartsWith("-path="))
            {
                var line = commandLineArg.Trim();
                line = line.Replace("-path=", "");

                return line;
            }

        return "";
    }

    private bool ServerInstall()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
            if (arg.Trim().Equals("-server"))
                return true;

        return false;
    }

    private bool WaitInstaller()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
            if (arg.Trim().Equals("-wait"))
                return true;

        return false;
    }

    public void ShowError()
    {
        MessageBox.Show(
            $"手动更新失败，请检查网络连接后重试。\n\n也可从所选来源的发布页手动下载完整 ZIP 安装包，解压全部文件，再运行 installer.exe。\n\nhttps://github.com/{GITHUB_USERNAME}/{GITHUB_REPOSITORY}/releases",
            "手动更新失败",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Close();
    }

    public async void DownloadLatestVersion()
    {
        try
        {
            _uri = await GetPathToLatestVersion();

            if (_uri == null) Environment.Exit(0);


            _directory = GetTemporaryDirectory();
            _file = _directory + "\\temp.zip";

            _downloadClient = new MyWebClient();
            var wc = _downloadClient;
            wc.Headers.Add("user-agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)");
            wc.DownloadProgressChanged += DownloadProgressChanged;
            wc.DownloadFileCompleted += DownloadComplete;
            // Keep the client alive until completion or cancellation.
            _lastProgressAt = DateTime.UtcNow;
            _progressCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _progressCheckTimer.Tick += CheckProgress;
            _progressCheckTimer.Start();
            wc.DownloadFileAsync(_uri, _file);
        }
        catch (Exception exception)
        {
            ShowError();
        }
    }

    private void CheckProgress(object sender, EventArgs e)
    {
        if (!_finished && !_cancel && DateTime.UtcNow - _lastProgressAt > TimeSpan.FromMinutes(2))
            ShowError();

        _lastValue = DownloadProgress.Value;
    }

    private bool ShouldRestart()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
            if (arg.Trim().Equals("-restart"))
                return true;

        return false;
    }

    private void DownloadComplete(object sender, AsyncCompletedEventArgs e)
    {
        _finished = true;
        _progressCheckTimer?.Stop();
        if (_cancel || e.Cancelled) return;
        if (e.Error != null) { ShowError(); return; }
        if (!_cancel)
        {
            try
            {
                ZipFile.ExtractToDirectory(_file, Path.Combine(_directory, "extract"));
                if (!File.Exists(Path.Combine(_directory, "extract", "installer.exe")))
                    throw new InvalidDataException("安装包中缺少 installer.exe。");
            }
            catch (Exception)
            {
                ShowError();
                return;
            }

            Thread.Sleep(400);

            if (!ServerInstall())
            {
                while (IsDCSRunning())
                    MessageBox.Show(
                        "无法安装 SRS。请先关闭 DCS，再点击确定。",
                        "请关闭 DCS",
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                var releaseNotes = MessageBox.Show(
                    "是否查看版本说明？建议安装前阅读版本说明。",
                    "查看版本说明",
                    MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (releaseNotes == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(changelogURL)
                        { UseShellExecute = true });
                }
            }

            QuitSimpleRadio();
            var procInfo = new ProcessStartInfo();
            procInfo.WorkingDirectory = Path.Combine(_directory, "extract");
            if (ServerInstall())
            {
                procInfo.Arguments = "-autoupdate";
                procInfo.Arguments += " -server ";
                procInfo.Arguments += " -path=\"" + ServerPath() + "\"";

                if (ShouldRestart()) procInfo.Arguments += " -restart ";
            }
            else
            {
                procInfo.Arguments = "-autoupdate";
            }

            procInfo.FileName = Path.Combine(Path.Combine(_directory, "extract"), "installer.exe");
            procInfo.UseShellExecute = false;
            var installerProcess = Process.Start(procInfo);

            if (WaitInstaller() && installerProcess != null)
            {
                installerProcess.WaitForExit();
            }
            


            //Process.Start(changelogURL);
        }

        Close();
    }

    public string GetTemporaryDirectory()
    {
        var tempFolder = Path.GetTempFileName();
        File.Delete(tempFolder);
        Directory.CreateDirectory(tempFolder);

        return tempFolder;
    }

    private void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        _lastProgressAt = DateTime.UtcNow;
        DownloadProgress.Value = e.ProgressPercentage;
    }

    private void CancelButtonClick(object sender, RoutedEventArgs e)
    {
        _cancel = true;
        _downloadClient?.CancelAsync();
        Close();
    }

    private void OnClosing(object sender, CancelEventArgs e)
    {
        _cancel = true;
        _progressCheckTimer?.Stop();
        _downloadClient?.CancelAsync();
        _downloadClient?.Dispose();
    }
}
