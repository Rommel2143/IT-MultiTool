using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace ITMultiTool.Services;

/// <summary>
/// Installs the block page portal: copies the app to Program Files, trusts the local root
/// certificate, and registers a SYSTEM scheduled task that runs "ITMultiTool.exe --portal" at startup.
/// </summary>
public static class PortalService
{
    public const string TaskName = "ITMultiTool Block Portal";
    private const string SettingsKey = @"SOFTWARE\ITMultiTool";
    private const string PortalEnabled = "PortalEnabled";

    public static string InstallDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ITMultiTool");

    public static bool IsInstalled()
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.OpenSubKey(SettingsKey);
        return key?.GetValue(PortalEnabled) as int? == 1;
    }

    /// <summary>True if something answers on http://127.0.0.1.</summary>
    public static bool IsRunning()
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync(IPAddress.Loopback, 80).Wait(TimeSpan.FromMilliseconds(500)) && client.Connected;
        }
        catch (AggregateException)
        {
            return false;
        }
    }

    public static void Install()
    {
        StopTask();
        CopyApplication();
        using (PortalCertificateService.EnsureRootCa()) { }
        RegisterTask();
        RunSchtasks($"/Run /TN \"{TaskName}\"");
        SetEnabled(true);

        // Hand blocked sites over from the browsers' own block page to the portal.
        WebsiteBlockerService.ResyncBrowserPolicies();
    }

    public static void Uninstall()
    {
        StopTask();
        RunSchtasks($"/Delete /TN \"{TaskName}\" /F", throwOnError: false);
        PortalCertificateService.RemoveRootCa();
        SetEnabled(false);
        WebsiteBlockerService.ResyncBrowserPolicies();

        if (!IsSameDirectory(AppContext.BaseDirectory, InstallDirectory))
        {
            try { Directory.Delete(InstallDirectory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void SetEnabled(bool enabled)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.CreateSubKey(SettingsKey);
        key.SetValue(PortalEnabled, enabled ? 1 : 0, RegistryValueKind.DWord);
    }

    private static void StopTask()
    {
        RunSchtasks($"/End /TN \"{TaskName}\"", throwOnError: false);

        // /End can return before the process has exited and released its files.
        foreach (var process in Process.GetProcessesByName("ITMultiTool"))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId)
                    continue;
                try
                {
                    var path = process.MainModule?.FileName;
                    if (path is not null && IsSameDirectory(Path.GetDirectoryName(path)!, InstallDirectory))
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
        }
    }

    private static void CopyApplication()
    {
        var source = AppContext.BaseDirectory;
        if (IsSameDirectory(source, InstallDirectory))
            return;

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(InstallDirectory, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void RegisterTask()
    {
        var exe = SecurityElement.Escape(Path.Combine(InstallDirectory, "ITMultiTool.exe"));
        var xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Shows the {SecurityElement.Escape(Branding.CompanyName)} block page when a blocked website is opened.</Description>
              </RegistrationInfo>
              <Triggers>
                <BootTrigger><Enabled>true</Enabled></BootTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>S-1-5-18</UserId>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <StartWhenAvailable>true</StartWhenAvailable>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <RestartOnFailure>
                  <Interval>PT1M</Interval>
                  <Count>999</Count>
                </RestartOnFailure>
                <Enabled>true</Enabled>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{exe}</Command>
                  <Arguments>--portal</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;

        var xmlPath = Path.Combine(Path.GetTempPath(), $"itmultitool-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlPath, xml, Encoding.Unicode);
            RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F");
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    private static void RunSchtasks(string arguments, bool throwOnError = true)
    {
        using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        var error = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (throwOnError && process.ExitCode != 0)
            throw new InvalidOperationException($"Task Scheduler error: {error.Trim()}");
    }

    private static bool IsSameDirectory(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
}
