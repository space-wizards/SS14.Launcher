## Strings for error codes (SS14L-<AREA>-<NNN>): the code line, the error overlays and the hints.
## The codes themselves are never translated.

## The line showing the error code.

error-code-label = Error code: { $code }
error-code-label-exit-code = Error code: { $code } (exit code { $exit })
error-code-label-exit-signal = Error code: { $code } (exit code { $exit }, signal { $signal })
error-code-help = Include this code when asking for help.

## Error overlays.

error-overlay-title-connect-failed = Connection failed
error-overlay-title-content-bundle-failed = Could not load the file
error-overlay-title-offline = Cannot reach the internet
error-overlay-desc-offline = The launcher could not reach its servers, so it can't check for updates or sign you in. You have not been signed out. Check your internet connection, then restart the launcher to carry on with your saved account.
error-overlay-title-translocated = Move the launcher to Applications
error-overlay-desc-translocated = The launcher is running from a temporary location, which could stop it from starting the game. Quit it, move it to your Applications folder and open it from there.
error-overlay-title-unexpected = Something went wrong
error-overlay-desc-unexpected = The launcher ran into an unexpected problem. You can keep using it, but if things stop working, restart the launcher.
error-overlay-title-save-settings = Could not save your settings
error-overlay-desc-save-settings = Your latest changes may be lost when you close the launcher.
error-overlay-title-clear-content = Could not clear server content
error-overlay-desc-clear-content = Close any running game clients and try again.
error-overlay-title-clear-engines = Could not clear installed engines
error-overlay-desc-clear-engines = Close any running game clients and try again.
error-overlay-title-open-external = Could not open that
error-overlay-desc-open-external = The launcher could not find a program to open this with.

## Hints: short advice shown above the error code. A code has a hint if there is a string named
## error-hint-<area>-<number> for it, for example error-hint-io-004 for SS14L-IO-004.
## Codes without one of their own use the generic hint for their area, error-hint-<area>, if there is one.

# Generic hints for codes that have no hint of their own
error-hint-net = A network issue occurred. Try again later.
error-hint-upd = Something went wrong while downloading the game. Try again later. If it keeps happening, ask for help and include the code below.
error-hint-auth = Something went wrong with your account. Try again later.
error-hint-cli = The game could not be started. Try again. If it keeps happening, ask for help and include the code below.
error-hint-db = The launcher had a problem with its stored data. Try again. If it keeps happening, ask for help and include the code below.

# Network
error-hint-net-004 = That server address doesn't look right. Check it for typos and try again.
error-hint-net-006 = The connection is slow or the server is overloaded. Try again in a moment.
error-hint-net-007 = The launcher could not find that server on the internet. Check your internet connection and the server address. A VPN or DNS problem can also cause this.
error-hint-net-008 = The server is not accepting connections, or something is blocking the connection. The server may be offline, or a firewall may be in the way. Try again later or try another server.
error-hint-net-009 = A secure connection could not be made. Check that your computer's date and time are correct. A VPN or antivirus that inspects connections can also cause this.
error-hint-net-012 = The download was cut off before it finished. Check your connection and try again.
error-hint-net-013 = The server that hosts the download refused the request. Try again later, or try another server.

# Account
error-hint-auth-001 = Check your username or email address and your password, then try again.
error-hint-auth-002 = Confirm your email address using the email you were sent, then try again.
error-hint-auth-003 = Enter the code from your authenticator app to finish signing in.
error-hint-auth-004 = That code was not accepted. Check your authenticator app and try again.
error-hint-auth-005 = If you think your account should not be locked, ask for help.
error-hint-auth-009 = The launcher could not reach the account server. Check your internet connection, or try again later.

# Database and settings
error-hint-db-006 = The launcher's settings could not be written. Check that its data folder is not read-only and that your disk is not full.
error-hint-db-008 = A launcher database is being used by another program. Close any other copies of the launcher and try again.
error-hint-db-009 = The launcher's database could not be written. Check that your disk is not full and that the folder is not read-only.
error-hint-db-010 = A launcher database file looks damaged. If this keeps happening, ask for help and include the code below.
error-hint-db-013 = The launcher could not open one of its database files. Check that its data folder exists and that you are allowed to write to it.
error-hint-db-014 = The drive holding the launcher's data does not support something the launcher needs. This often happens on network drives. Move the launcher's data to a local drive.

# Starting the game
error-hint-cli-004 = A file the launcher needs to start the game is missing. Reinstall the launcher. If you use antivirus software, check that it has not removed anything.
error-hint-cli-010 = The system refused to run the game. Check the permissions of the launcher's files, and that its folder is not on a drive mounted as non-executable (noexec). On Windows, your antivirus may be blocking it.
error-hint-cli-011 = The game cannot run on this system. The launcher may be built for a different processor. Download the version for your system, or reinstall.
error-hint-cli-012 = macOS would not open the game. Open System Settings, then Privacy & Security, and allow it if it was blocked. On Apple Silicon, an Intel build needs Rosetta.

# Downloads
error-hint-upd-003 = The download was damaged. Try again. If it keeps happening, go to Options, then Clear installed server content, and connect again.
error-hint-upd-005 = { error-hint-upd-003 }
error-hint-upd-009 = { error-hint-upd-003 }
error-hint-upd-021 = { error-hint-upd-003 }
error-hint-upd-022 = The game files stored by the launcher are damaged. Go to Options, then Clear installed server content, and connect again.

# Problems on the server's side
error-hint-upd-011 = This server uses an engine version that is no longer available. This is not a launcher issue, tell the servers staff.
error-hint-upd-012 = This server uses an engine version that is marked as insecure. This is not a launcher issue, tell the servers staff.
error-hint-upd-014 = This server needs a game component that is not available. This is not a launcher issue, tell the servers staff.
error-hint-upd-018 = This server uses a game component that is marked as insecure. This is not a launcher issue, tell the servers staff.

# Files and folders
error-hint-io-001 = The launcher could not create its data folders. Check that your user account is allowed to write to your user folder.
error-hint-io-002 = A file the launcher needs is being used by another program. Close any other copies of the launcher and the game, then try again.
error-hint-io-003 = The launcher is not allowed to use a file or folder. Check the folder's permissions. On Windows, also check that your antivirus is not blocking the launcher.
error-hint-io-004 = Your disk is full. Free up some space and try again. The temporary folder can also be full when the main disk is not.
error-hint-io-005 = The launcher could not unpack a downloaded file. A full disk or a damaged download can cause this, try again.
error-hint-io-006 = Some of the files may still be in use by a running game.
error-hint-io-007 = The launcher cannot write its log file. Check that its data folder is not read-only.
error-hint-io-008 = The launcher could not create a temporary file. Check that your temporary folder exists and has free space.
error-hint-io-009 = No program was found to open this. Check your default web browser or file manager.
error-hint-io-010 = A file operation failed. Try again, and check the launcher log if it keeps happening.
error-hint-io-011 = The launcher's data folder is on a read-only drive. Move it to a drive you can write to.
error-hint-io-012 = Too many files are open. Close other programs, or raise the limit on open files (ulimit -n).
error-hint-io-013 = A file path is too long. Use a shorter path for the launcher's data folder.
