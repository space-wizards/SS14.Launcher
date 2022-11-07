# SS14.Launcher

This is the launcher you should be using to connect to SS14 servers. Server browser, content downloads, account management. It's got it all!

# Development

Useful environment variables for development:
* `SS14_LAUNCHER_APPDATA_NAME=launcherTest` to change the user data directories the launcher stores its data in. This can be useful to avoid breaking your "normal" SS14 launcher data while developing something.
* `SS14_LAUNCHER_OVERRIDE_AUTH=https://.../` to change the auth API URL to test against a local dev version of the API.
