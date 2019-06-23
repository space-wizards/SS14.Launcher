using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SS14.Launcher
{
    internal static class Program
    {
        private const string JenkinsBaseUrl = "https://builds.spacestation14.io/jenkins";
        private const string JenkinsJobName = "SS14 Content";
        private const string CurrentLauncherVersion = "1";

        private static HttpClient _httpClient;

        public static async Task Main()
        {
            _fixTlsVersions();
            _httpClient = new HttpClient();

            await BrickIfOutdated();

            var dataDir = GetUserDataDir();
            // Ensure data dir exists.
            Directory.CreateDirectory(dataDir);

            await RunUpdate(dataDir);

            LaunchClient();
        }

        private static async Task BrickIfOutdated()
        {
            // Brick the launcher if it's an old version.
            var launcherVersionUri =
                new Uri("https://builds.spacestation14.io/jenkins/userContent/current_launcher_version.txt");
            var versionRequest = await _httpClient.GetAsync(launcherVersionUri);
            var version = (await versionRequest.Content.ReadAsStringAsync()).Trim();
            if (version != CurrentLauncherVersion)
            {
                Console.WriteLine("This launcher is out of date. Download a new one.");
                Environment.Exit(1);
            }
        }

        private static void LaunchClient()
        {
            var binPath = GetClientBinPath();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "mono",
                    Arguments = "Robust.Client.exe",
                    WorkingDirectory = binPath,
                    UseShellExecute = false,
                });
                // I don't know why but .NET Core closes the child process on exit??
                process?.WaitForExit();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(binPath, "Robust.Client.exe"),
                });
                process?.WaitForExit();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // TODO: does this cause macOS to make a security warning?
                // If it does we'll have to manually launch the contents, which is simple enough.
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = "'Space Station 14.app'",
                    WorkingDirectory = binPath,
                });
                process?.WaitForExit();
            }
            else
            {
                throw new NotSupportedException("Unsupported platform.");
            }
        }

        private static async Task RunUpdate(string dataDir)
        {
            Console.WriteLine("Checking for update...");
            var jobUri = new Uri($"{JenkinsBaseUrl}/job/{Uri.EscapeUriString(JenkinsJobName)}/api/json");
            var jobDataResponse = await _httpClient.GetAsync(jobUri);
            if (!jobDataResponse.IsSuccessStatusCode)
            {
                throw new Exception($"Got bad status code {jobDataResponse.StatusCode} from Jenkins.");
            }

            var jobInfo = JsonConvert.DeserializeObject<JenkinsJobInfo>(
                await jobDataResponse.Content.ReadAsStringAsync());
            var latestBuildNumber = jobInfo.LastSuccessfulBuild.Number;

            var versionFile = Path.Combine(dataDir, "current_build");
            bool needUpdate;
            if (File.Exists(versionFile))
            {
                var buildNumber = int.Parse(File.ReadAllText(versionFile, Encoding.UTF8), CultureInfo.InvariantCulture);
                needUpdate = buildNumber != latestBuildNumber;
                if (needUpdate)
                {
                    Console.WriteLine("Current version ({0}) is out of date, updating to {1}.", buildNumber,
                        latestBuildNumber);
                }
            }
            else
            {
                Console.WriteLine("As it turns out, we don't have any version yet. Time to update.");
                // Version file doesn't exist, assume first run or whatever.
                needUpdate = true;
            }

            if (!needUpdate)
            {
                Console.WriteLine("No update needed!");
                return;
            }

            var binPath = GetClientBinPath();

            if (!Directory.Exists(binPath))
            {
                Directory.CreateDirectory(binPath);
            }
            else
            {
                ClearDirectory(binPath);
            }

            // Download new version and extract it.
            using (var stream = await _downloadArtifact(latestBuildNumber, GetBuildFilename()))
            {
                ExtractZipToDirectory(binPath, stream);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // .NET's zip extraction system doesn't seem to preserve +x.
                // Technically can't blame it because there's no "official" way to store that,
                // since zip files are DOS-centric.

                // Manually chmod +x the App bundle then.
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x '{Path.Combine("Space Station 14.app", "Contents", "MacOS", "SS14")}'",
                    WorkingDirectory = binPath,
                });
                process?.WaitForExit();
            }

            // Write version to disk.
            File.WriteAllText(versionFile, latestBuildNumber.ToString(CultureInfo.InvariantCulture), Encoding.UTF8);

            Console.WriteLine("Update done!");
        }

        private static async Task<Stream> _downloadArtifact(int buildNumber, string fileName)
        {
            var artifactUri
                = new Uri(
                    $"{JenkinsBaseUrl}/job/{Uri.EscapeUriString(JenkinsJobName)}/{buildNumber}/artifact/release/{Uri.EscapeUriString(fileName)}");

            var artifactResponse = await _httpClient.GetAsync(artifactUri);
            if (!artifactResponse.IsSuccessStatusCode)
            {
                throw new Exception($"Got bad status code {artifactResponse.StatusCode} from Jenkins.");
            }

            return await artifactResponse.Content.ReadAsStreamAsync();
        }

        private static void ExtractZipToDirectory(string directory, Stream zipStream)
        {
            using (var zipArchive = new ZipArchive(zipStream))
            {
                zipArchive.ExtractToDirectory(directory);
            }
        }

        private static void ClearDirectory(string directory)
        {
            var dirInfo = new DirectoryInfo(directory);
            foreach (var fileInfo in dirInfo.EnumerateFiles())
            {
                fileInfo.Delete();
            }

            foreach (var childDirInfo in dirInfo.EnumerateDirectories())
            {
                childDirInfo.Delete(true);
            }
        }

        [Pure]
        private static string GetBuildFilename()
        {
            string platform;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                platform = "Windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                platform = "Linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                platform = "macOS";
            }
            else
            {
                throw new NotSupportedException("Unsupported platform.");
            }

            return $"SS14.Client_{platform}_x64.zip";
        }

        [Pure]
        private static string GetClientBinPath()
        {
            return Path.Combine(GetUserDataDir(), "client_bin");
        }

        [Pure]
        private static string GetUserDataDir()
        {
            string appDataDir;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // SpecialFolder.ApplicationData is $XDG_CONFIG_HOME on Linux.
                // I'd like $XDG_DATA_HOME. SpecialFolder.LocalApplicationData is but eh.
                // Just do it manually not that hard.
                var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (xdgDataHome == null)
                {
                    var home = Environment.GetEnvironmentVariable("HOME");
                    if (home == null)
                    {
                        throw new Exception("$HOME should always be set on a POSIX system.");
                    }

                    appDataDir = Path.Combine(home, ".local", "share");
                }
                else
                {
                    appDataDir = xdgDataHome;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // TODO: Is SpecialFolder.ApplicationData incorrect on .NET Core on macOS?
                // I know it is on Mono and tries to follow the XDG base directory spec.
                var home = Environment.GetEnvironmentVariable("HOME");
                if (home == null)
                {
                    throw new Exception("$HOME should always be set on a POSIX system.");
                }

                appDataDir = Path.Combine(home, "Library", "Application Support");
            }
            else
            {
                appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }

            return Path.Combine(appDataDir, "Space Station 14", "launcher");
        }

        [Conditional("NET_FRAMEWORK")]
        private static void _fixTlsVersions()
        {
            // So, supposedly .NET Framework 4.7 is supposed to automatically select sane TLS versions.
            // Yet, it does not for some people. This causes it to try to connect to our servers with
            // SSL 3 or TLS 1.0, neither of which are accepted for security reasons.
            // (The minimum our servers accept is TLS 1.2)
            // So, ONLY on Windows (Mono is fine) and .NET Framework we manually tell it to use TLS 1.2
            // I assume .NET Core does not have this issue being disconnected from the OS and all that.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            }
        }
    }

#pragma warning disable 649
    [Serializable]
    internal class JenkinsJobInfo
    {
        public JenkinsBuildRef LastSuccessfulBuild;
    }

    [Serializable]
    internal class JenkinsBuildRef
    {
        public int Number;
        public string Url;
    }

#pragma warning restore 649
}