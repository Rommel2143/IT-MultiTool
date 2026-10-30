using Microsoft.Win32;

namespace ITMultiTool.Services;

/// <summary>
/// Blocks USB mass-storage devices by disabling the USBSTOR driver and setting the
/// "Removable Disks: Deny read/write/execute access" policies.
/// </summary>
public static class UsbStorageService
{
    private const string UsbStorKey = @"SYSTEM\CurrentControlSet\Services\USBSTOR";
    private const string RemovableDisksPolicyKey =
        @"SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices\{53f5630d-b6bf-11d0-94f2-00a0c91efb8b}";

    private const int StartManual = 3;
    private const int StartDisabled = 4;

    private static readonly string[] DenyValues = ["Deny_Read", "Deny_Write", "Deny_Execute"];

    public static bool IsLocked()
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

        using (var key = hklm.OpenSubKey(UsbStorKey))
        {
            if (key?.GetValue("Start") as int? == StartDisabled)
                return true;
        }

        using (var key = hklm.OpenSubKey(RemovableDisksPolicyKey))
        {
            return key?.GetValue("Deny_Read") as int? == 1;
        }
    }

    public static void Lock()
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

        // New devices: the mass-storage driver won't load.
        using (var key = hklm.OpenSubKey(UsbStorKey, writable: true)
                   ?? throw new InvalidOperationException("USBSTOR service key not found."))
            key.SetValue("Start", StartDisabled, RegistryValueKind.DWord);

        // Devices whose driver is already loaded: access is denied by policy.
        using (var key = hklm.CreateSubKey(RemovableDisksPolicyKey))
        {
            foreach (var name in DenyValues)
                key.SetValue(name, 1, RegistryValueKind.DWord);
        }
    }

    public static void Unlock()
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

        using (var key = hklm.OpenSubKey(UsbStorKey, writable: true)
                   ?? throw new InvalidOperationException("USBSTOR service key not found."))
            key.SetValue("Start", StartManual, RegistryValueKind.DWord);

        using (var key = hklm.OpenSubKey(RemovableDisksPolicyKey, writable: true))
        {
            if (key is not null)
            {
                foreach (var name in DenyValues)
                    key.DeleteValue(name, throwOnMissingValue: false);
            }
        }
    }
}
