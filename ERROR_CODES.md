# Launcher error codes

Codes look like `SS14L-<AREA>-<NNN>`:

| Area | Covers |
|---|---|
| `NET` | Network and connection problems |
| `UPD` | Updating and downloading the game |
| `AUTH` | Accounts and signing in |
| `CLI` | Starting the game client |
| `DB` | The launcher's local databases |
| `IO` | Files and folders |
| `GEN` | Anything else |

Codes should NEVER be renumbered or reused. The source of truth is [`ErrorCodes.cs`](SS14.Launcher/ErrorCodes.cs).

## NET

| Code | Meaning | Usual cause |
|---|---|---|
| `SS14L-NET-001` | HTTP error contacting a server | The server or a download host returned an error or is down |
| `SS14L-NET-002` | The server sent a response that could not be read | Broken or incompatible server |
| `SS14L-NET-003` | The server sent an empty response | Broken server |
| `SS14L-NET-004` | The server address is not valid | A typo in the address |
| `SS14L-NET-005` | The server gave an invalid address to connect to | Broken server configuration |
| `SS14L-NET-006` | A request timed out | Slow or unstable connection, or the server is overloaded |
| `SS14L-NET-007` | The host name could not be resolved | No internet, DNS problems, or a mistyped address |
| `SS14L-NET-008` | The connection was refused or the network is unreachable | Server offline, firewall, or no internet |
| `SS14L-NET-009` | A secure connection could not be established | Wrong system clock, certificate problem, or antivirus interference |
| `SS14L-NET-010` | The server list could not be fetched from any hub | No internet, or the hubs are down |
| `SS14L-NET-011` | Some hubs did not respond when fetching the server list | One hub is down|
| `SS14L-NET-012` | A download was cut off or failed partway through | Connection dropped during a download |
| `SS14L-NET-013` | A download server returned an error status | Download host is down or the file was removed |
| `SS14L-NET-014` | No network connection was detected | Offline, airplane mode, or a disabled network adapter |

## UPD

| Code | Meaning | Usual cause |
|---|---|---|
| `SS14L-UPD-001` | The content version has no engine dependency | Corrupt content database |
| `SS14L-UPD-002` | The content manifest has an unknown header | Server is incompatible or serving something that is not a manifest |
| `SS14L-UPD-003` | The content manifest does not match its hash | Corrupt or tampered download |
| `SS14L-UPD-004` | A compressed blob decompressed to the wrong size | Corrupt download |
| `SS14L-UPD-005` | A downloaded blob does not match its hash | Corrupt download or a bad mirror |
| `SS14L-UPD-006` | The download server did not say which protocol it supports | Broken download server |
| `SS14L-UPD-007` | The download server sent unreadable protocol version headers | Broken download server |
| `SS14L-UPD-008` | The download server does not support a download protocol this launcher knows | Launcher is too old or too new for that server |
| `SS14L-UPD-009` | A downloaded content zip does not match its hash | Corrupt download |
| `SS14L-UPD-010` | The server did not provide any download information | Broken server configuration |
| `SS14L-UPD-011` | The required engine version is not in the engine manifest | The server uses an invalid engine version |
| `SS14L-UPD-012` | The required engine version is marked as insecure | The server must update to a newer engine |
| `SS14L-UPD-013` | There is no engine build for this platform | The server is advertising an incorrect engine version |
| `SS14L-UPD-014` | The required engine module is not in the module manifest | The server needs a module that does not exist |
| `SS14L-UPD-015` | No suitable engine module version is in the manifest | Module version mismatch |
| `SS14L-UPD-016` | Engine module version redirects loop forever | Broken module manifest |
| `SS14L-UPD-017` | An engine module version redirect points at a missing version | Broken module manifest |
| `SS14L-UPD-018` | The required engine module version is marked as insecure | The server must update to a newer module |
| `SS14L-UPD-019` | There is no engine module build for this platform | The module does not support this operating system or CPU |
| `SS14L-UPD-020` | An engine module signature could not be verified | Corrupt download or tampering |
| `SS14L-UPD-021` | Downloaded data is corrupt or incomplete | Interrupted or corrupt download; try again |
| `SS14L-UPD-022` | The content database uses an unknown compression scheme | Corrupt content database |
| `SS14L-UPD-023` | The content manifest could not be parsed | Corrupt or incompatible manifest |
| `SS14L-UPD-024` | An update is already running | Two launches at once |
| `SS14L-UPD-025` | An unexpected failure while updating | See the exception type shown with the error |
| `SS14L-UPD-026` | The resource manifest inside the content is invalid | Broken server content |

## AUTH

| Code | Meaning | Usual cause |
|---|---|---|
| `SS14L-AUTH-001` | Sign-in: invalid credentials | Wrong username, email or password |
| `SS14L-AUTH-002` | Sign-in: the account's email address is not confirmed | Confirm the email address first |
| `SS14L-AUTH-003` | Sign-in: two-factor authentication is required | Shouldn't really appear |
| `SS14L-AUTH-004` | Sign-in: the two-factor code is invalid | Wrong or expired code |
| `SS14L-AUTH-005` | Sign-in: the account is locked | Contact support |
| `SS14L-AUTH-006` | Sign-in: the authentication server gave an unknown reason | Newer server than launcher |
| `SS14L-AUTH-007` | The authentication server returned an unexpected status | Authentication server problem |
| `SS14L-AUTH-008` | The authentication server sent an invalid response | Authentication server problem |
| `SS14L-AUTH-009` | The authentication server could not be reached | No internet, firewall, or the server is down |
| `SS14L-AUTH-010` | Refreshing a sign-in token failed | Authentication server problem |
| `SS14L-AUTH-011` | Checking a sign-in token failed | Authentication server problem |
| `SS14L-AUTH-012` | An unexpected sign-in error | See the log |

## CLI

| Code | Meaning | Usual cause |
|---|---|---|
| `SS14L-CLI-001` | The game client could not be started (an error occurred) | See the exception in the log |
| `SS14L-CLI-002` | The game client process did not start | The operating system refused to start it |
| `SS14L-CLI-003` | The game client exited immediately with an error | Crash on startup |
| `SS14L-CLI-004` | The game loader was not found, or this platform is not supported | Damaged install, or a security program removed a file |
| `SS14L-CLI-005` | The engine version is not installed locally | Installed engine was deleted |
| `SS14L-CLI-006` | The file is not a content bundle (no metadata) | Wrong file |
| `SS14L-CLI-007` | The content bundle's metadata is invalid | Broken bundle |
| `SS14L-CLI-008` | The content bundle file could not be read | Corrupt file |
| `SS14L-CLI-009` | The game client's log files could not be created | Log folder is not writable |
| `SS14L-CLI-010` | The game loader could not be run (permission denied) | The loader file is not executable or the install is on a `noexec` mount (mostly Linux and macOS), or a security program blocked it |
| `SS14L-CLI-011` | The game loader cannot run on this system | Wrong CPU architecture, or a damaged loader file |
| `SS14L-CLI-012` | macOS could not start the game | Gatekeeper or quarantine blocked it, Rosetta is missing for an Intel build, or the app bundle is damaged |

## DB

| Code | Meaning | Usual cause |
|---|---|---|
| `SS14L-DB-001` | The settings database could not be opened | Locked or unreadable file |
| `SS14L-DB-002` | The settings database migration failed | Corrupt settings database |
| `SS14L-DB-003` | The content database could not be opened | Locked or unreadable file |
| `SS14L-DB-004` | The content database migration failed | Corrupt content database |
| `SS14L-DB-005` | The override assets database migration failed | Corrupt assets database |
| `SS14L-DB-006` | Saving settings failed | Disk full, read-only folder, or another launcher is using the file |
| `SS14L-DB-007` | The stored settings are corrupt | Corrupt settings database |
| `SS14L-DB-008` | A database is busy or locked by another process | Another launcher is using it |
| `SS14L-DB-009` | A database is full or read-only | Disk full or read-only folder |
| `SS14L-DB-010` | A database file is corrupt | Disk problem or an interrupted write |
| `SS14L-DB-011` | Clearing the installed server content failed | A game client is still running |
| `SS14L-DB-012` | Some other database error | See the log |
| `SS14L-DB-013` | A database file could not be opened | The data folder is missing, read-only or not writable |
| `SS14L-DB-014` | A database file system error | The data folder is on a network drive or a file system without the locking SQLite needs |

## IO

| Code | Meaning | Usual cause |
|---|---|---|
| `SS14L-IO-001` | The launcher's data folders could not be created | Folder not writable |
| `SS14L-IO-002` | A file is in use by another process | Another launcher, a running game, or an antivirus is already using this |
| `SS14L-IO-003` | Access to a file or folder was denied | Permissions or an antivirus |
| `SS14L-IO-004` | The disk is full | Free up space |
| `SS14L-IO-005` | Extracting an archive failed | Corrupt download or disk problem |
| `SS14L-IO-006` | Clearing the installed engines failed | A game client is still running |
| `SS14L-IO-007` | The launcher log file could not be written | Log folder is not writable |
| `SS14L-IO-008` | A temporary file could not be created | Temp folder not writable or disk full |
| `SS14L-IO-009` | A link or folder could not be opened with another program | No valid program to handle this was found |
| `SS14L-IO-010` | Some other file error | See the log |
| `SS14L-IO-011` | The file system is read-only | The data folder is on a read-only drive or mount |
| `SS14L-IO-012` | Too many files are open | Raise the open-file limit |
| `SS14L-IO-013` | A file path is too long | Use a shorter data folder path |

## GEN

| Code | Meaning | Usual cause |
|---|---|---|
| `SS14L-GEN-001` | An unexpected error | See the exception type and the log |
| `SS14L-GEN-002` | The launcher crashed with an unhandled error | See the log |
| `SS14L-GEN-003` | An unhandled error in a background task or interface handler | See the log |
| `SS14L-GEN-004` | A required runtime component (the Visual C++ redistributable) is missing | Install the VC++ 2015 redistributable |
| `SS14L-GEN-005` | This version of the operating system is not supported | Update the operating system |
| `SS14L-GEN-006` | The graphical interface could not start | No display, or missing system libraries such as X11 or fontconfig |
| `SS14L-GEN-007` | A required native library could not be loaded | A library such as libsodium, libzstd or sqlite3 is missing or built for another system |
| `SS14L-GEN-008` | The launcher is running from a temporary read-only location (macOS) | Move the launcher to the Applications folder and open it from there |

## Platform notes

Most codes can occur everywhere. The exceptions:

- Windows only: `SS14L-GEN-004`, `SS14L-GEN-005`.
- Mostly Linux and macOS: `SS14L-CLI-010`, `SS14L-CLI-011`, `SS14L-GEN-006`.
- macOS only: `SS14L-CLI-012`, `SS14L-GEN-008`.
