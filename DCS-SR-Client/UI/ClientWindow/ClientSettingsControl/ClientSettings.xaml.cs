using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using MessageBox = System.Windows.MessageBox;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl;

public partial class ClientSettings : UserControl
{
    public ClientSettings()
    {
        InitializeComponent();
        DataContext = new ClientSettingsViewModel();
    }

    private void OpenRepositoryClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/idanfang/DCS-SRS-CN-noautoupdate") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("无法打开浏览器：\n" + ex.Message, "打开 GitHub 仓库", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ManualUpdateClick(object sender, RoutedEventArgs e)
    {
        var upstream = (sender as Button)?.Tag as string == "upstream";
        var sourceName = upstream ? "原作者英文原版" : "本项目中文手动更新版";
        UpdateEnglishButton.IsEnabled = false;
        UpdateChineseButton.IsEnabled = false;
        ManualUpdateStatus.Visibility = Visibility.Visible;
        ManualUpdateStatus.Text = "正在检查…";
        try
        {
            var release = await UpdaterChecker.Instance.GetManualReleaseAsync(upstream);
            if (!release.Assets.Any(asset => asset.Name.StartsWith("DCS-SimpleRadioStandalone", StringComparison.OrdinalIgnoreCase)
                && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                ManualUpdateStatus.Text = "该来源尚未提供可安装的发行包。";
                return;
            }
            if (!upstream && release.TagName == UpdaterChecker.MANUAL_RELEASE_TAG)
            {
                ManualUpdateStatus.Text = "当前已是本项目最新版本。";
                return;
            }
            ManualUpdateStatus.Text = "最新可用版本：" + release.TagName;
            var notes = release.Body ?? "暂无更新说明。";
            if (notes.Length > 1000) notes = notes.Substring(0, 1000) + "\n（完整说明请查看发布页）";
            var warning = upstream
                ? "\n\n注意：安装英文原版将覆盖汉化与本项目的手动更新功能。原版可能恢复自动更新。"
                : "";
            if (MessageBox.Show($"来源：{sourceName}\n版本：{release.TagName}\n\n{notes}{warning}\n\n是否下载并安装？安装前请关闭 DCS。",
                "手动更新", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
            if (Process.GetProcessesByName("DCS").Length > 0)
            {
                MessageBox.Show("请关闭 DCS 后再进行手动更新。", "手动更新", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!UpdaterChecker.Instance.LaunchManualUpdater(upstream, release.TagName))
                throw new InvalidOperationException("找不到 SRS-AutoUpdater.exe，请从本项目发布页下载完整安装包。");
            ManualUpdateStatus.Text = "已启动手动更新器。";
        }
        catch (Octokit.NotFoundException)
        {
            ManualUpdateStatus.Text = "该来源尚未发布稳定版本。";
        }
        catch (Exception ex)
        {
            ManualUpdateStatus.Text = "更新未开始，请稍后重试。";
            MessageBox.Show("无法开始手动更新：\n" + ex.Message, "手动更新", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            UpdateEnglishButton.IsEnabled = true;
            UpdateChineseButton.IsEnabled = true;
        }
    }
}
