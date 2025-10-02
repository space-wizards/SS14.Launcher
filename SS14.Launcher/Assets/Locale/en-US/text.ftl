## Strings for the drop-down window to manage your active account

account-drop-down-none-selected = No account selected
account-drop-down-not-logged-in = Not logged in
account-drop-down-log-out = Log out
account-drop-down-log-out-of = Log out of { $name }
account-drop-down-switch-account = Switch account:
account-drop-down-select-account = Select account:
account-drop-down-add-account = Add account

## Localization for the "add favorite server" dialog window

add-favorite-window-title = Add Favorite Server
add-favorite-window-address-invalid = Address is invalid
add-favorite-window-label-name = Name:
add-favorite-window-label-address = Address:
# 'Example' name shown as a watermark in the name input box
add-favorite-window-example-name = Honk Station

## Strings for the "connecting" menu that appears when connecting to a server.

connecting-title-connecting = Connecting…
connecting-title-content-bundle = Loading…
connecting-cancel = Cancel
connecting-status-none = Starting connection…
connecting-status-update-error =
    There was an error while downloading server content. If this persists try some of the following:
    - Try connecting to another game server to see if the problem persists.
    - Try disabling or enabling software such as VPNs, if you have any.

    If you are still having issues, first try contacting the server you are attempting to join before asking for support on the Official Space Station 14 Discord or Forums.

    Technical error: { $err }
connecting-status-update-error-no-engine-for-platform = This game is using an older version that does not support your current platform. Please try another server or try again later.
connecting-status-update-error-no-module-for-platform = This game requires additional functionality that is not yet supported on your current platform. Please try another server or try again later.
connecting-status-update-error-unknown = Unknown
connecting-status-updating = Updating: { $status }
connecting-status-connecting = Fetching connection info from server…
connecting-status-connection-failed = Failed to connect to server!
connecting-status-starting-client = Starting client…
connecting-status-not-a-content-bundle = File is not a valid content bundle!
connecting-status-client-crashed = Client seems to have crashed while starting. If this persists, please ask on Discord or GitHub for support.
connecting-update-status-checking-client-update = Checking for server content update…
connecting-update-status-downloading-engine = Downloading server content…
connecting-update-status-downloading-content = Downloading server content…
connecting-update-status-fetching-manifest = Fetching server manifest…
connecting-update-status-verifying = Verifying download integrity…
connecting-update-status-culling-engine = Clearing old content…
connecting-update-status-culling-content = Clearing old server content…
connecting-update-status-ready = Update done!
connecting-update-status-checking-engine-modules = Checking for additional dependencies…
connecting-update-status-downloading-engine-modules = Downloading extra dependencies…
connecting-update-status-committing-download = Synchronizing to disk…
connecting-update-status-loading-into-db = Storing assets in database…
connecting-update-status-loading-content-bundle = Loading content bundle…
connecting-update-status-unknown = You shouldn't see this

connecting-privacy-policy-text = This server requires that you accept its privacy policy before connecting.
connecting-privacy-policy-text-version-changed = This server has updated its privacy policy since the last time you played. You must accept the new version before connecting.
connecting-privacy-policy-view = View privacy policy
connecting-privacy-policy-accept = Accept (continue)
connecting-privacy-policy-decline = Decline (disconnect)

## Strings for the "direct connect" dialog window.

direct-connect-title = Direct Connect
direct-connect-text = Enter server address to connect:
direct-connect-connect = Connect
direct-connect-address-invalid = Address is invalid

## Strings for the "hub settings" dialog window.

hub-settings-title = Hub Settings
hub-settings-button-done = Done
hub-settings-button-cancel = Cancel
hub-settings-button-reset = Reset
hub-settings-button-reset-tooltip = Reset to default settings
hub-settings-button-add-tooltip = Add hub
hub-settings-button-remove-tooltip = Remove hub
hub-settings-button-increase-priority-tooltip = Increase priority
hub-settings-button-decrease-priority-tooltip = Decrease priority
hub-settings-explanation = Here you can add extra hubs to fetch game servers from. You should only add hubs that you trust, as they can 'spoof' game servers from other hubs. The order of the hubs matters; if two hubs advertise the same game server the hub with the higher priority (higher in the list) will take precedence.
hub-settings-heading-default = Default
hub-settings-heading-custom = Custom
hub-settings-warning-invalid = Invalid hub (don't forget http(s)://)
hub-settings-warning-duplicate = Duplicate hubs

## Strings for the login screen

login-log-launcher = Log Launcher

## Error messages for login

login-error-invalid-credentials = Invalid login credentials
login-error-account-unconfirmed = The email address for this account still needs to be confirmed. Please confirm your email address before trying to log in
login-error-account-2fa-required = 2-factor authentication required
login-error-account-2fa-invalid = 2-factor authentication code invalid
login-error-account-account-locked = Account has been locked. Please contact us if you believe this to be in error.
login-error-unknown = Unknown error
login-errors-button-ok = Ok

## Strings for 2FA login

login-2fa-title = 2-factor authentication required
login-2fa-message = Please enter the authentication code from your app.
login-2fa-input-watermark = Authentication code
login-2fa-button-confirm = Confirm
login-2fa-button-recovery-code = Recovery code
login-2fa-button-cancel = Cancel

## Strings for the "login expired" view on login

login-expired-title = Login expired
login-expired-message =
    The session for this account has expired.
    Please re-enter your password.
login-expired-password-watermark = Password
login-expired-button-log-in = Log in
login-expired-button-log-out = Log out
login-expired-button-forgot-password = Forgot your password?

## Strings for the "forgot password" view on login

login-forgot-title = Forgot password?
login-forgot-message = If you've forgotten your password, you can enter the email address associated with your account here to receive a reset link.
login-forgot-email-watermark = Your email address
login-forgot-button-submit = Submit
login-forgot-button-back = Back to login
login-forgot-busy-sending = Sending email…
login-forgot-success-title = Reset email sent
login-forgot-success-message = A reset link has been sent to your email address.
login-forgot-error = Error

## Strings for the "login" view on login

login-login-title = Log in
login-login-username-watermark = Username or email
login-login-password-watermark = Password
login-login-show-password = Show Password
login-login-button-log-in = Log in
login-login-button-forgot = Forgot your password?
login-login-button-resend = Resend email confirmation
login-login-button-register = Don't have an account? Register!
login-login-busy-logging-in = Logging in…
login-login-error-title = Unable to log in

## Strings for the "register confirmation" view on login

login-confirmation-confirmation-title = Register confirmation
login-confirmation-confirmation-message = Please check your email to confirm your account. Once you have confirmed your account, press the button below to log in.
login-confirmation-button-confirm = I have confirmed my account
login-confirmation-button-cancel = Cancel
login-confirmation-busy = Logging in…

## Strings for the general main window layout of the launcher

main-window-title = Space Station 14 Launcher
main-window-header-link-discord = Discord
main-window-header-link-website = Website
main-window-out-of-date = Launcher out of date
main-window-out-of-date-desc =
    This launcher is out of date.
    Please download a new version from our website.
main-window-out-of-date-desc-steam =
    This launcher is out of date.
    Please allow Steam to update the game.
main-window-out-of-date-exit = Exit
main-window-out-of-date-download-manual = Download (manual)
main-window-early-access-title = Heads up!
main-window-early-access-desc = Space Station 14 is still very much in alpha. We hope you like what you see, and maybe even stick around, but make sure to keep your expectations modest for the time being.
main-window-early-access-accept = Got it!
main-window-intel-degrade-title = Intel 13th/14th Generation CPU detected.
main-window-intel-degrade-desc =
    The Intel 13th/14th generation CPUs are known to silently degrade permanently and die due to a microcode bug by Intel. We sadly can't tell if you are currently affected by this bug, so this warning appears for all users with these CPUs.

    We STRONGLY encourage you to update your motherboard's BIOS to the latest version to ensure prevention of further damage. If you are having stability issues/failing to start the game, downclock your CPU to get it stable again and use your warranty to ask about getting it replaced.

    We are not responsible to help with any issues that may arise from affected processors, unless you took the precautions and are sure your CPU is stable. This message will not appear again after you accept it.
main-window-intel-degrade-accept = I understand and have taken the necessary precautions.
main-window-rosetta-title = You are running the game using Rosetta 2!
main-window-rosetta-desc =
    You seem to be on an Apple Silicon Mac and are running the game using Rosetta 2. You may enjoy better performance and battery life by running the game natively.

    To do this, right click the launcher in Finder, select "Get Info" and uncheck "Open using Rosetta". After that, restart the launcher.

    If you are intentionally running the game using Rosetta 2, you can dismiss this message and it will not appear again. Although if you are doing this in an attempt to fix a problem, please make a bug report.
main-window-rosetta-accept = Continue
main-window-drag-drop-prompt = Drop to run game
main-window-busy-checking-update = Checking for launcher update…
main-window-busy-checking-login-status = Refreshing login status…
main-window-busy-checking-account-status = Checking account status
main-window-error-connecting-auth-server = Error connecting to authentication server
main-window-error-unknown = Unknown error occurred

## Long region names for server tag filters (shown in tooltip)

region-africa-central = Africa Central
region-africa-north = Africa North
region-africa-south = Africa South
region-antarctica = Antarctica
region-asia-east = Asia East
region-asia-north = Asia North
region-asia-south-east = Asia South East
region-central-america = Central America
region-europe-east = Europe East
region-europe-west = Europe West
region-greenland = Greenland
region-india = India
region-middle-east = Middle East
region-the-moon = The Moon
region-north-america-central = North America Central
region-north-america-east = North America East
region-north-america-west = North America West
region-oceania = Oceania
region-south-america-east = South America East
region-south-america-south = South America South
region-south-america-west = South America West

## Short region names for server tag filters (shown in filter check box)

region-short-africa-central = Africa Central
region-short-africa-north = Africa North
region-short-africa-south = Africa South
region-short-antarctica = Antarctica
region-short-asia-east = Asia East
region-short-asia-north = Asia North
region-short-asia-south-east = Asia South East
region-short-central-america = Central America
region-short-europe-east = Europe East
region-short-europe-west = Europe West
region-short-greenland = Greenland
region-short-india = India
region-short-middle-east = Middle East
region-short-the-moon = The Moon
region-short-north-america-central = NA Central
region-short-north-america-east = NA East
region-short-north-america-west = NA West
region-short-oceania = Oceania
region-short-south-america-east = SA East
region-short-south-america-south = SA South
region-short-south-america-west = SA West

## Strings for the "servers" tab

tab-servers-title = Servers
tab-servers-refresh = Refresh
filters = Filters ({ $filteredServers } / { $totalServers })
tab-servers-search-watermark = Search For Servers…
tab-servers-table-players = Players
tab-servers-table-name = Server Name
tab-servers-table-round-time = Time
tab-servers-list-status-error = There was an error fetching the master server lists. Maybe try refreshing?
tab-servers-list-status-partial-error = Failed to fetch some of the server lists. Ensure your hub configuration is correct and try refreshing.
tab-servers-list-status-updating-master = Fetching master server list…
tab-servers-list-status-none-filtered = No servers match your search or filter settings.
tab-servers-list-status-none = There are no public servers. Ensure your hub configuration is correct.

## Strings for the server filters menu

filters-title = Filters
filters-title-language = Language
filters-title-region = Region
filters-title-rp = Role-play level
filters-title-player-count = Player count
filters-title-18 = 18+
filters-title-hub = Hub
filters-18-yes = Yes
filters-18-yes-desc = Yes
filters-18-no = No
filters-18-no-desc = No
filters-player-count-hide-empty = Hide empty
filters-player-count-hide-empty-desc = Servers with no players will not be shown
filters-player-count-hide-full = Hide full
filters-player-count-hide-full-desc = Servers that are full will not be shown
filters-player-count-minimum = Minimum:
filters-player-count-minimum-desc = Servers with less players will not be shown
filters-player-count-maximum = Maximum:
filters-player-count-maximum-desc = Servers with more players will not be shown
filters-unspecified-desc = Unspecified
filters-unspecified = Unspecified

## Server roleplay levels for the filters menu

filters-rp-none = None
filters-rp-none-desc = None
filters-rp-low = Low
filters-rp-low-desc = Low
filters-rp-medium = Medium
filters-rp-medium-desc = Medium
filters-rp-high = High
filters-rp-high-desc = High

## Strings for entries in the server list (including home page)

server-entry-connect = Connect
server-entry-add-favorite = Favorite
server-entry-remove-favorite = Unfavorite
server-entry-offline = OFFLINE
server-entry-player-count =
    { $players } / { $max ->
        [0] ∞
       *[1] { $max }
    }
server-entry-round-time = { $hours ->
 [0] { $mins }M
*[1] { $hours }H { $mins }M
}
server-entry-fetching = Fetching…
server-entry-description-offline = Unable to contact server
server-entry-description-fetching = Fetching server status…
server-entry-description-error = Error while fetching server description
server-entry-description-none = No server description provided
server-entry-status-lobby = Lobby
server-fetched-from-hub = Fetched from { $hub }
server-entry-raise = Raise to top

## Strings for the "Development" tab
## These aren't shown to users so they're not very important

tab-development-title = { "[" }DEV]
tab-development-title-override = { "[" }DEV (override active!!!)]
tab-development-disable-signing = Disable Engine Signature Checks
tab-development-disable-signing-desc = { "[" }DEV ONLY] Disables verification of engine signatures. DO NOT ENABLE UNLESS YOU KNOW EXACTLY WHAT YOU'RE DOING.
tab-development-enable-engine-override = Enable engine override
tab-development-enable-engine-override-desc = Override path to load engine zips from (release/ in RobustToolbox)

## Strings for the "home" tab

tab-home-title = Home
tab-home-favorite-servers = Favorite Servers
tab-home-add-favorite = Add favorite
tab-home-refresh = Refresh
tab-home-direct-connect = Direct connect to server
tab-home-run-content-bundle = Run content bundle/replay
tab-home-go-to-servers-tab = Go to the servers tab
tab-home-favorites-guide = Mark servers as favorite for easy access here

## Strings for the "news" tab

tab-news-title = News
tab-news-recent-news = Recent News:
tab-news-pulling-news = Pulling news…

## Strings for the "options" tab

tab-options-title = Options
tab-options-flip = { "*" }flip
tab-options-clear-engines = Clear installed engines
tab-options-clear-content = Clear installed server content
tab-options-open-log-directory = Open log directory
tab-options-account-settings = Account Settings
tab-options-account-settings-desc = You can manage your account settings, such as changing email or password, through our website.
tab-options-compatibility-mode = Compatibility Mode
tab-options-compatibility-mode-desc = This forces the game to use a different graphics backend, which is less likely to suffer from driver bugs. Try this if you are experiencing graphical issues or crashes.
tab-options-log-client = Log Client
tab-options-log-client-desc = Enables logging of any game client output. Useful for developers.
tab-options-log-launcher = Log Launcher
tab-options-log-launcher-desc = Enables logging of the launcher. Useful for developers. (requires launcher restart)
tab-options-verbose-launcher-logging = Verbose Launcher Logging
tab-options-verbose-launcher-logging-desc = For when the developers are *very* stumped with your problem. (requires launcher restart)
tab-options-seasonal-branding = Seasonal Branding
tab-options-seasonal-branding-desc = Whatever temporally relevant icons and logos we can come up with.
tab-options-disable-signing = Disable Engine Signature Checks
tab-options-disable-signing-desc = { "[" }DEV ONLY] Disables verification of engine signatures. DO NOT ENABLE UNLESS YOU KNOW EXACTLY WHAT YOU'RE DOING.
tab-options-hub-settings = Hub Settings
tab-options-hub-settings-desc = Change what hub server or servers you would like to use to fetch the server list.
tab-options-desc-incompatible = This option is incompatible with your platform and has been disabled.

## For the language selection menu.

# Text on the button that opens the menu.
language-selector-label = Language
# "Save" button.
language-selector-save = Save
# "Cancel" button.
language-selector-cancel = Cancel
language-selector-help-translate = Want to help translate? You can!
language-selector-system-language = System language ({ $languageName })
# Used for contents of each language button.
language-selector-language = { $languageName } ({ $englishName })
