#!/usr/bin/env python3

import os
import sys
import urllib.request
import tarfile
import zipfile
import shutil
from typing import List, Optional

PLATFORM_WINDOWS = "windows"
PLATFORM_WINDOWS_ARM64 = "windows-arm64"
PLATFORM_LINUX = "linux"
PLATFORM_LINUX_ARM64 = "linux-arm64"
PLATFORM_MACOS = "mac"
PLATFORM_MACOS_ARM64 = "mac-arm64"

DOTNET_RUNTIME_VERSION = "9.0.9"

DOTNET_RUNTIME_DOWNLOADS = {
    PLATFORM_LINUX: "https://builds.dotnet.microsoft.com/dotnet/Runtime/9.0.9/dotnet-runtime-9.0.9-linux-x64.tar.gz",
    PLATFORM_LINUX_ARM64: "https://builds.dotnet.microsoft.com/dotnet/Runtime/9.0.9/dotnet-runtime-9.0.9-linux-arm64.tar.gz",
    PLATFORM_WINDOWS: "https://builds.dotnet.microsoft.com/dotnet/Runtime/9.0.9/dotnet-runtime-9.0.9-win-x64.zip",
    PLATFORM_WINDOWS_ARM64: "https://builds.dotnet.microsoft.com/dotnet/Runtime/9.0.9/dotnet-runtime-9.0.9-win-arm64.zip",
    PLATFORM_MACOS: "https://builds.dotnet.microsoft.com/dotnet/Runtime/9.0.9/dotnet-runtime-9.0.9-osx-x64.tar.gz",
    PLATFORM_MACOS_ARM64: "https://builds.dotnet.microsoft.com/dotnet/Runtime/9.0.9/dotnet-runtime-9.0.9-osx-arm64.tar.gz"
}

p = os.path.join


def main() -> None:
    update_netcore_runtime(sys.argv[1:])


def update_netcore_runtime(platforms: List[str]) -> None:
    runtime_cache = p("Dependencies/dotnet")
    version_file_path = p(runtime_cache, "VERSION")

    # Check if current version is fine.
    current_version: Optional[str]

    try:
        with open(version_file_path, "r") as f:
            current_version = f.read().strip()

    except FileNotFoundError:
        current_version = None

    if current_version != DOTNET_RUNTIME_VERSION and os.path.exists(runtime_cache):
        print("Cached Release .NET Core Runtime out of date/nonexistant, downloading new one..")
        shutil.rmtree(runtime_cache)
    os.makedirs(runtime_cache, exist_ok=True)

    with open(version_file_path, "w") as f:
        f.write(DOTNET_RUNTIME_VERSION)

    # Download missing runtimes if necessary.
    for platform in platforms:
        platform_runtime_cache = p(runtime_cache, platform)
        if not os.path.exists(platform_runtime_cache):
            os.mkdir(platform_runtime_cache)
            download_platform_runtime(platform_runtime_cache, platform)


def download_platform_runtime(dir: str, platform: str) -> None:
    print(f"Downloading .NET Core Runtime for platform {platform}.")
    download_file = p(dir, "download.tmp")
    download_url = DOTNET_RUNTIME_DOWNLOADS[platform]
    urllib.request.urlretrieve(download_url, download_file)

    if download_url.endswith(".tar.gz"):
        # this is a tar gz.
        with tarfile.open(download_file, "r:gz") as tar:
            tar.extractall(dir)
    elif download_url.endswith(".zip"):
        with zipfile.ZipFile(download_file) as zipF:
            zipF.extractall(dir)

    os.remove(download_file)


if __name__ == "__main__":
    main()
