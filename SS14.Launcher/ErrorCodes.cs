namespace SS14.Launcher;

/// <summary>
/// The catalog of all launcher error codes. See also <c>ERROR_CODES.md</c> in the repository root.
/// </summary>
/// <remarks>
/// Please NEVER EVER EVER reuse codes. If an error is removed or the code generating is
/// deprecated, just leave it be. Make a new one, I doubt we'll ever have 999 errors per area...
/// </remarks>
public static class ErrorCodes
{
    private const string Net =  "NET";  // Network
    private const string Upd =  "UPD";  // Updates and downloads
    private const string Auth = "AUTH"; // I wonder
    private const string Cli =  "CLI";  // Client, not command line interface
    private const string Db =   "DB";   // Database disk image is malformed
    private const string Io =   "IO";   // Files and folders
    private const string Gen =  "GEN";  // "I don't know, fuck it"

    // ---- NET ----

    /// <summary>HTTP error contacting a server.</summary>
    public static readonly ErrorCode NetHttp = new(Net, 1);

    /// <summary>The server sent a response that could not be read.</summary>
    public static readonly ErrorCode NetBadResponse = new(Net, 2);

    /// <summary>The server sent an empty response.</summary>
    public static readonly ErrorCode NetEmptyResponse = new(Net, 3);

    /// <summary>The server address is not a valid address.</summary>
    public static readonly ErrorCode NetBadServerAddress = new(Net, 4);

    /// <summary>The server gave an invalid address to connect to.</summary>
    public static readonly ErrorCode NetBadConnectAddress = new(Net, 5);

    /// <summary>A request timed out.</summary>
    public static readonly ErrorCode NetTimeout = new(Net, 6);

    /// <summary>The host name could not be resolved.</summary>
    public static readonly ErrorCode NetDnsFailed = new(Net, 7);

    /// <summary>The connection was refused or the network is unreachable.</summary>
    public static readonly ErrorCode NetConnectFailed = new(Net, 8);

    /// <summary>A secure connection could not be established.</summary>
    public static readonly ErrorCode NetTls = new(Net, 9);

    /// <summary>The server list could not be fetched from any hub.</summary>
    public static readonly ErrorCode NetServerListFailed = new(Net, 10);

    /// <summary>Some hubs did not respond when fetching the server list.</summary>
    public static readonly ErrorCode NetServerListPartial = new(Net, 11);

    /// <summary>A download was cut off or failed partway through.</summary>
    public static readonly ErrorCode NetDownloadInterrupted = new(Net, 12);

    /// <summary>A download server returned an error status.</summary>
    public static readonly ErrorCode NetDownloadHttp = new(Net, 13);

    /// <summary>No network connection was detected.</summary>
    public static readonly ErrorCode NetNoConnection = new(Net, 14);

    // ---- UPD ----

    /// <summary>The content version has no engine dependency.</summary>
    public static readonly ErrorCode UpdNoEngineDependency = new(Upd, 1);

    /// <summary>The content manifest has an unknown header.</summary>
    public static readonly ErrorCode UpdManifestHeader = new(Upd, 2);

    /// <summary>The content manifest does not match its hash.</summary>
    public static readonly ErrorCode UpdManifestHash = new(Upd, 3);

    /// <summary>A compressed blob decompressed to the wrong size.</summary>
    public static readonly ErrorCode UpdBlobSize = new(Upd, 4);

    /// <summary>A downloaded blob does not match its hash.</summary>
    public static readonly ErrorCode UpdBlobHash = new(Upd, 5);

    /// <summary>The download server did not say which download protocol it supports.</summary>
    public static readonly ErrorCode UpdProtocolHeadersMissing = new(Upd, 6);

    /// <summary>The download server sent unreadable protocol version headers.</summary>
    public static readonly ErrorCode UpdProtocolHeadersInvalid = new(Upd, 7);

    /// <summary>The download server does not support a download protocol this launcher knows.</summary>
    public static readonly ErrorCode UpdNoProtocol = new(Upd, 8);

    /// <summary>A downloaded content zip does not match its hash.</summary>
    public static readonly ErrorCode UpdZipHash = new(Upd, 9);

    /// <summary>The server did not provide any download information.</summary>
    public static readonly ErrorCode UpdNoDownloadInfo = new(Upd, 10);

    /// <summary>The required engine version is not in the engine manifest.</summary>
    public static readonly ErrorCode UpdEngineNotInManifest = new(Upd, 11);

    /// <summary>The required engine version is marked as insecure.</summary>
    public static readonly ErrorCode UpdEngineInsecure = new(Upd, 12);

    /// <summary>There is no engine build for this platform.</summary>
    public static readonly ErrorCode UpdNoEngineForPlatform = new(Upd, 13);

    /// <summary>The required engine module is not in the module manifest.</summary>
    public static readonly ErrorCode UpdModuleNotInManifest = new(Upd, 14);

    /// <summary>No suitable engine module version is in the manifest.</summary>
    public static readonly ErrorCode UpdNoSuitableModule = new(Upd, 15);

    /// <summary>Engine module version redirects loop forever.</summary>
    public static readonly ErrorCode UpdModuleRedirectLoop = new(Upd, 16);

    /// <summary>An engine module version redirect points at a missing version.</summary>
    public static readonly ErrorCode UpdModuleRedirectMissing = new(Upd, 17);

    /// <summary>The required engine module version is marked as insecure.</summary>
    public static readonly ErrorCode UpdModuleInsecure = new(Upd, 18);

    /// <summary>There is no engine module build for this platform.</summary>
    public static readonly ErrorCode UpdNoModuleForPlatform = new(Upd, 19);

    /// <summary>An engine module signature could not be verified.</summary>
    public static readonly ErrorCode UpdModuleSignature = new(Upd, 20);

    /// <summary>Downloaded data is corrupt or incomplete.</summary>
    public static readonly ErrorCode UpdCorruptData = new(Upd, 21);

    /// <summary>The content database uses an unknown compression scheme.</summary>
    public static readonly ErrorCode UpdUnknownCompression = new(Upd, 22);

    /// <summary>The content manifest could not be parsed.</summary>
    public static readonly ErrorCode UpdManifestParse = new(Upd, 23);

    /// <summary>An update is already running.</summary>
    public static readonly ErrorCode UpdAlreadyRunning = new(Upd, 24);

    /// <summary>An unexpected failure while updating.</summary>
    public static readonly ErrorCode UpdUnexpected = new(Upd, 25);

    /// <summary>The resource manifest inside the content is invalid.</summary>
    public static readonly ErrorCode UpdResourceManifest = new(Upd, 26);

    // ---- AUTH ----

    /// <summary>Sign-in: invalid credentials.</summary>
    public static readonly ErrorCode AuthInvalidCredentials = new(Auth, 1);

    /// <summary>Sign-in: the account's email address is not confirmed.</summary>
    public static readonly ErrorCode AuthUnconfirmed = new(Auth, 2);

    /// <summary>Sign-in: two-factor authentication is required.</summary>
    public static readonly ErrorCode AuthTfaRequired = new(Auth, 3);

    /// <summary>Sign-in: the two-factor code is invalid.</summary>
    public static readonly ErrorCode AuthTfaInvalid = new(Auth, 4);

    /// <summary>Sign-in: the account is locked.</summary>
    public static readonly ErrorCode AuthLocked = new(Auth, 5);

    /// <summary>Sign-in: the authentication server gave an unknown reason.</summary>
    public static readonly ErrorCode AuthUnknownDeny = new(Auth, 6);

    /// <summary>The authentication server returned an unexpected status.</summary>
    public static readonly ErrorCode AuthBadStatus = new(Auth, 7);

    /// <summary>The authentication server sent an invalid response.</summary>
    public static readonly ErrorCode AuthBadResponse = new(Auth, 8);

    /// <summary>The authentication server could not be reached.</summary>
    public static readonly ErrorCode AuthConnect = new(Auth, 9);

    /// <summary>Refreshing a sign-in token failed.</summary>
    public static readonly ErrorCode AuthRefresh = new(Auth, 10);

    /// <summary>Checking a sign-in token failed.</summary>
    public static readonly ErrorCode AuthTokenCheck = new(Auth, 11);

    /// <summary>An unexpected sign-in error.</summary>
    public static readonly ErrorCode AuthUnexpected = new(Auth, 12);

    // ---- CLI ----

    /// <summary>The game client could not be started (an error occurred).</summary>
    public static readonly ErrorCode CliStartFailed = new(Cli, 1);

    /// <summary>The game client process did not start.</summary>
    public static readonly ErrorCode CliNoProcess = new(Cli, 2);

    /// <summary>The game client exited immediately with an error.</summary>
    public static readonly ErrorCode CliExitedEarly = new(Cli, 3);

    /// <summary>The game loader was not found, or this platform is not supported.</summary>
    public static readonly ErrorCode CliNoLoader = new(Cli, 4);

    /// <summary>The engine version is not installed locally.</summary>
    public static readonly ErrorCode CliEngineMissing = new(Cli, 5);

    /// <summary>The file is not a content bundle (no metadata).</summary>
    public static readonly ErrorCode CliNotBundle = new(Cli, 6);

    /// <summary>The content bundle's metadata is invalid.</summary>
    public static readonly ErrorCode CliBundleMetadata = new(Cli, 7);

    /// <summary>The content bundle file could not be read.</summary>
    public static readonly ErrorCode CliBundleUnreadable = new(Cli, 8);

    /// <summary>The game client's log files could not be created.</summary>
    public static readonly ErrorCode CliLogFiles = new(Cli, 9);

    /// <summary>The game loader could not be run (permission denied).</summary>
    public static readonly ErrorCode CliNotExecutable = new(Cli, 10);

    /// <summary>The game loader cannot run on this system (wrong architecture or format).</summary>
    public static readonly ErrorCode CliCannotRun = new(Cli, 11);

    /// <summary>macOS could not start the game.</summary>
    public static readonly ErrorCode CliMacLaunch = new(Cli, 12);

    // ---- DB ----

    /// <summary>The settings database could not be opened.</summary>
    public static readonly ErrorCode DbOpenSettings = new(Db, 1);

    /// <summary>The settings database migration failed.</summary>
    public static readonly ErrorCode DbMigrateSettings = new(Db, 2);

    /// <summary>The content database could not be opened.</summary>
    public static readonly ErrorCode DbOpenContent = new(Db, 3);

    /// <summary>The content database migration failed.</summary>
    public static readonly ErrorCode DbMigrateContent = new(Db, 4);

    /// <summary>The override assets database migration failed.</summary>
    public static readonly ErrorCode DbMigrateAssets = new(Db, 5);

    /// <summary>Saving settings failed.</summary>
    public static readonly ErrorCode DbSaveSettings = new(Db, 6);

    /// <summary>The stored settings are corrupt.</summary>
    public static readonly ErrorCode DbSettingsCorrupt = new(Db, 7);

    /// <summary>A database is busy or locked by another process.</summary>
    public static readonly ErrorCode DbBusy = new(Db, 8);

    /// <summary>A database is full or read-only.</summary>
    public static readonly ErrorCode DbFull = new(Db, 9);

    /// <summary>A database file is corrupt.</summary>
    public static readonly ErrorCode DbCorrupt = new(Db, 10);

    /// <summary>Clearing the installed server content failed.</summary>
    public static readonly ErrorCode DbClearContent = new(Db, 11);

    /// <summary>Some other database error.</summary>
    public static readonly ErrorCode DbOther = new(Db, 12);

    /// <summary>A database file could not be opened (missing folder, read-only or not writable).</summary>
    public static readonly ErrorCode DbCannotOpen = new(Db, 13);

    /// <summary>A database file system error, such as locking not being supported on this drive.</summary>
    public static readonly ErrorCode DbIoError = new(Db, 14);

    // ---- IO ----

    /// <summary>The launcher's data folders could not be created.</summary>
    public static readonly ErrorCode IoCreateDirs = new(Io, 1);

    /// <summary>A file is in use by another process.</summary>
    public static readonly ErrorCode IoInUse = new(Io, 2);

    /// <summary>Access to a file or folder was denied.</summary>
    public static readonly ErrorCode IoAccessDenied = new(Io, 3);

    /// <summary>The disk is full.</summary>
    public static readonly ErrorCode IoDiskFull = new(Io, 4);

    /// <summary>Extracting an archive failed.</summary>
    public static readonly ErrorCode IoExtract = new(Io, 5);

    /// <summary>Clearing the installed engines failed.</summary>
    public static readonly ErrorCode IoClearEngines = new(Io, 6);

    /// <summary>The launcher log file could not be written.</summary>
    public static readonly ErrorCode IoLogWrite = new(Io, 7);

    /// <summary>A temporary file could not be created.</summary>
    public static readonly ErrorCode IoTempFile = new(Io, 8);

    /// <summary>A link or folder could not be opened with another program.</summary>
    public static readonly ErrorCode IoOpenExternal = new(Io, 9);

    /// <summary>Some other file error.</summary>
    public static readonly ErrorCode IoOther = new(Io, 10);

    /// <summary>The file system is read-only.</summary>
    public static readonly ErrorCode IoReadOnly = new(Io, 11);

    /// <summary>Too many files are open.</summary>
    public static readonly ErrorCode IoTooManyFiles = new(Io, 12);

    /// <summary>A file path is too long.</summary>
    public static readonly ErrorCode IoPathTooLong = new(Io, 13);

    // ---- GEN ----

    /// <summary>An unexpected error.</summary>
    public static readonly ErrorCode GenUnexpected = new(Gen, 1);

    /// <summary>The launcher crashed with an unhandled error.</summary>
    public static readonly ErrorCode GenCrash = new(Gen, 2);

    /// <summary>An unhandled error in a background task or interface handler.</summary>
    public static readonly ErrorCode GenBackground = new(Gen, 3);

    /// <summary>A required runtime component (the Visual C++ redistributable) is missing.</summary>
    public static readonly ErrorCode GenMissingRuntime = new(Gen, 4);

    /// <summary>This version of the operating system is not supported.</summary>
    public static readonly ErrorCode GenUnsupportedOs = new(Gen, 5);

    /// <summary>The graphical interface could not start (no display, or missing system libraries).</summary>
    public static readonly ErrorCode GenNoGraphics = new(Gen, 6);

    /// <summary>A required native library could not be loaded.</summary>
    public static readonly ErrorCode GenMissingLibrary = new(Gen, 7);

    /// <summary>The launcher is running from a temporary read-only location (macOS).</summary>
    public static readonly ErrorCode GenTranslocated = new(Gen, 8);
}
