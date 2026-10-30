# IT MultiTool

A small Windows Forms admin utility (C# / .NET 10) for common IT desk tasks.

| Tab | What it does | How |
| --- | --- | --- |
| **Wallpaper** | Change the desktop wallpaper (Fill / Fit / Stretch / Tile / Center / Span) | Copies the image to `C:\ProgramData\ITMultiTool\Wallpaper` and applies it with `SystemParametersInfo` |
| | Lock / unlock the wallpaper | `NoChangingWallPaper` policy (HKLM + HKCU) and the HKCU `Policies\System\Wallpaper` value that pins the image |
| **USB Storage** | Lock / unlock USB mass-storage devices | `USBSTOR` driver `Start` = 4 / 3, plus the *Removable Disks: Deny read/write/execute* policies |
| **Website Blocker** | Block / unblock websites | Adds `0.0.0.0` and `::` entries (domain + `www.`) to a marked section of the hosts file, then flushes the DNS cache |

## Requirements

- Windows 10 / 11
- .NET 10 Desktop Runtime (or publish self-contained)
- Administrator rights — the app manifest requests elevation automatically

## Build

```bash
dotnet build ITMultiTool.slnx -c Release
```

Single-file, self-contained executable:

```bash
dotnet publish src/ITMultiTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Notes

- Wallpaper settings and the HKCU lock values apply to the user account the app runs under. Run it from the
  user's own session (elevated with *Run as administrator*) rather than from a separate admin login.
- After locking USB storage, unplug and reconnect drives that were already attached.
- Domain Group Policy can overwrite these registry values on the next policy refresh.
- Website blocking only touches lines between the `IT-MultiTool blocked sites` markers; the rest of the hosts file is left
  unchanged. Browsers that cache DNS may need a restart.
