using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Octokit;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;

//Quick and dirty update checker based on GitHub Published Versions
//TODO make this a singleton
public class UpdaterChecker
{
    public delegate void UpdateCallback(UpdateCallbackResult result);

    private static UpdaterChecker _instance;
    private static readonly object _lock = new();

    public static readonly string GITHUB_USERNAME = "ciribob";

    public static readonly string GITHUB_REPOSITORY = "DCS-SimpleRadioStandalone";

    // Required for all requests against the GitHub API, as per https://developer.github.com/v3/#user-agent-required
    public static readonly string GITHUB_USER_AGENT = $"{GITHUB_USERNAME}_{GITHUB_REPOSITORY}";

    public static readonly string MINIMUM_PROTOCOL_VERSION = "1.9.0.0";

    public static readonly string VERSION = "2.3.8.2";

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static UpdaterChecker Instance
    {
        get
        {
            lock (_lock)
            {
                if (_instance == null) _instance = new UpdaterChecker();
            }

            return _instance;
        }
    }

    public const string MANUAL_RELEASE_TAG = "v2.3.8.2-cn.1";

    // Retain this API for server callers, without any background network request.
    public Task CheckForUpdateAsync(bool checkForBetaUpdates, UpdateCallback updateCallback)
    {
        updateCallback?.Invoke(new UpdateCallbackResult
        {
            UpdateAvailable = false,
            Version = Version.Parse(VERSION),
            Error = false
        });
        return Task.CompletedTask;
    }

    public Task<Release> GetManualReleaseAsync(bool upstream)
    {
        var client = new GitHubClient(new ProductHeaderValue("DCS-SRS-CN-noautoupdate", VERSION));
        return client.Repository.Release.GetLatest(
            upstream ? "ciribob" : "idanfang",
            upstream ? "DCS-SimpleRadioStandalone" : "DCS-SRS-CN-noautoupdate");
    }

    public bool LaunchManualUpdater(bool upstream, string tag)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var location = AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.Combine(location, "SRS-AutoUpdater.exe");
        if (!File.Exists(path)) path = Path.GetFullPath(Path.Combine(location, "../SRS-AutoUpdater.exe"));
        if (!File.Exists(path)) return false;
        var info = new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            WorkingDirectory = location,
            Verb = "runas"
        };
        info.ArgumentList.Add(upstream ? "-source=upstream" : "-source=cn");
        info.ArgumentList.Add("-tag=" + tag);
        return Process.Start(info) != null;
    }

    private bool IsDCSRunning()
    {
        foreach (var clsProcess in Process.GetProcesses())
            if (clsProcess.ProcessName.ToLower().Trim().Equals("dcs"))
                return true;

        return false;
    }

    public bool LaunchUpdater(bool beta)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var location = AppDomain.CurrentDomain.BaseDirectory;
        var autoUpdatePath = "";

        if (File.Exists(Path.Combine(location, "../SRS-AutoUpdater.exe")))
        {
            autoUpdatePath = Path.Combine(location, "../SRS-AutoUpdater.exe");
        }
        else if (File.Exists(Path.Combine(location, "SRS-AutoUpdater.exe")))
        {
            autoUpdatePath = Path.Combine(location, "SRS-AutoUpdater.exe");
        }
        else
        {
            _logger.Error("Unable to find SRS-AutoUpdater.exe");
            return false;
        }


        Task.Run(async Task () =>
        {
            while (IsDCSRunning())
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
            }

#pragma warning disable CA1416
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
#pragma warning restore CA1416
#pragma warning disable CA1416
            var hasAdministrativeRight = principal.IsInRole(WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416

            if (!hasAdministrativeRight)
            {
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = location,
                    FileName = autoUpdatePath,
                    Verb = "runas"
                };

                if (beta) startInfo.Arguments = "-beta";

                try
                {
                    var p = Process.Start(startInfo);
                }
                catch (Win32Exception)
                {
                    //TODO sort this out with a callback
                    // MessageBox.Show(
                    //     "SRS Auto Update Requires Admin Rights",
                    //     "UAC Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                if (beta)
                    Process.Start(new ProcessStartInfo(autoUpdatePath, "-beta")
                        { UseShellExecute = true });
                else
                    Process.Start(new ProcessStartInfo(autoUpdatePath)
                        { UseShellExecute = true });
            }
        });

        return true;
    }
}

public class UpdateCallbackResult
{
    public bool UpdateAvailable { get; set; }
    public string Branch { get; set; }
    public Version Version { get; set; }
    public string Url { get; set; }
    public bool Beta { get; set; }
    public bool Error { get; set; }
}