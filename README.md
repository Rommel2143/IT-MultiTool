# IT MultiTool

A small Windows Forms admin utility (C# / .NET 10) for common IT desk tasks.

| Tab | What it does | How |
| --- | --- | --- |
| **Wallpaper** | Change the desktop wallpaper (Fill / Fit / Stretch / Tile / Center / Span) | Copies the image to `C:\ProgramData\ITMultiTool\Wallpaper` and applies it with `SystemParametersInfo` |
| | Lock / unlock the wallpaper | `NoChangingWallPaper` policy (HKLM + HKCU) and the HKCU `Policies\System\Wallpaper` value that pins the image |
| **USB Storage** | Lock / unlock USB mass-storage devices | `USBSTOR` driver `Start` = 4 / 3, plus the *Removable Disks: Deny read/write/execute* policies |
| **Website Blocker** | Block / unblock websites, typed in or from built-in non-work lists | Adds `127.0.0.1` and `::1` entries (domain + `www.`) to a marked section of the hosts file and flushes the DNS cache, and mirrors the list into the Edge / Chrome / Brave `URLBlocklist` and Firefox `WebsiteFilter` policies so browsers can't bypass it |
| **Block Page** | Show the Philippines TRC Inc. block page when a blocked site is opened | See below |

### Block page portal

**Install / Update** on the *Block Page* tab:

1. Copies the app to `C:\Program Files\ITMultiTool`.
2. Creates a root certificate (`Philippines TRC Inc. Web Filter CA`) that only this PC trusts. Its private key stays in the
   machine store and cannot be exported. It is used to issue certificates for blocked HTTPS sites, so browsers show the block
   page instead of a certificate error.
3. Registers the `ITMultiTool Block Portal` scheduled task (runs as SYSTEM at startup, restarts on failure), which runs
   `ITMultiTool.exe --portal` to answer on `127.0.0.1` / `::1` ports 80 and 443.
4. Switches Edge, Chrome and Brave from their own `URLBlocklist` block page to the portal, turns off QUIC and the browsers'
   built-in secure DNS so requests follow the hosts file, and lets Firefox trust the Windows certificate store.

**Remove** undoes all of these steps. Blocked sites stay blocked, and the browsers' own block page is used again.
The portal writes its log to `C:\ProgramData\ITMultiTool\portal.log`. It needs ports 80 and 443 to be free; it can't
run alongside IIS or another local web server.

### Administrator password

The first time IT MultiTool opens, it asks you to create a password (at least 8 characters, with letters and a number or
symbol). After that, the password is required every time the app opens. After 5 wrong attempts the app closes. To change
it, click **Change password** in the header.

Only a salted PBKDF2-SHA256 hash (600,000 iterations) is stored, in `ITMultiTool.password.json` next to
`ITMultiTool.exe`. Every PC that runs the app from the same folder uses the same password. The block page install copies
the file to `C:\Program Files\ITMultiTool`, and changing the password updates that copy on the PC where you change it.

If the password is forgotten, someone with write access to the folder can delete `ITMultiTool.password.json`. The next
launch then asks for a new password. If the file exists but can't be read (network drop, damaged file), the app refuses
to open instead of offering to create a new password.

## Running from a shared folder

1. Publish a self-contained single file, so the PCs don't need the .NET runtime installed:

   ```bash
   dotnet publish src/ITMultiTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
   ```

   This produces a single `ITMultiTool.exe` of about 50 MB.

2. Copy `publish\ITMultiTool.exe` to the share, for example `\\server\IT\ITMultiTool\`.
3. Set the share permissions:
   - **IT staff:** Modify. Needed to create or change the password and to update the app.
   - **Everyone else:** no access, or Read only. **Never** give other users write access: anyone who can write to the
     folder can delete the password file or replace the program.
4. On each PC, open the app with its UNC path (`\\server\IT\ITMultiTool\ITMultiTool.exe`) and choose
   **Run as administrator**. Don't use a mapped drive letter: elevated programs can't see drives mapped in the normal
   session.
5. The first person to open it creates the password, and every PC uses it from then on.

Settings the app changes (wallpaper, USB, hosts file, browser policies, block page) are still made **on the PC where it
runs**. Open the app on each PC you want to manage.

## Requirements

- Windows 10 / 11
- .NET 10 Desktop Runtime (or publish self-contained)
- Administrator rights — the app manifest requests elevation automatically

## Build

```bash
dotnet build ITMultiTool.slnx -c Release
```

For the shared-folder build, see *Running from a shared folder* above.

## Notes

- Wallpaper settings and the HKCU lock values apply to the user account the app runs under. Run it from the
  user's own session (elevated with *Run as administrator*) rather than from a separate admin login.
- After locking USB storage, unplug and reconnect drives that were already attached.
- Domain Group Policy can overwrite these registry values on the next policy refresh.
- Website blocking only touches lines between the `IT-MultiTool blocked sites` markers; the rest of the hosts file is left
  unchanged. Browsers that cache DNS may need a restart.
