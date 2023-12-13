#!/bin/bash

cd "$(dirname "$0")"

./download_net_runtime.py mac

# Clear out previous build.
rm -r **/bin bin/publish/macOS
rm SS14.Launcher_macOS.zip

dotnet publish SS14.Launcher/SS14.Launcher.csproj /p:FullRelease=True -c Release --no-self-contained -r osx-x64 /nologo /p:RobustILLink=true
dotnet publish SS14.Loader/SS14.Loader.csproj -c Release --no-self-contained -r osx-x64 /nologo

# Create intermediate directories.
mkdir -p bin/publish/macOS

cp -r "PublishFiles/Space Station 14 Launcher.app" bin/publish/macOS

mkdir -p "bin/publish/macOS/Space Station 14 Launcher.app/Contents/Resources/dotnet/"
mkdir -p "bin/publish/macOS/Space Station 14 Launcher.app/Contents/Resources/bin/"
mkdir -p "bin/publish/macOS/Space Station 14 Launcher.app/Contents/Resources/bin/loader/Space Station 14.app/Contents/Resources/bin/"

cp -r Dependencies/dotnet/mac/* "bin/publish/macOS/Space Station 14 Launcher.app/Contents/Resources/dotnet/"
cp -r SS14.Launcher/bin/Release/net8.0/osx-x64/publish/* "bin/publish/macOS/Space Station 14 Launcher.app/Contents/Resources/bin/"
cp -r SS14.Loader/bin/Release/net8.0/osx-x64/publish/* "bin/publish/macOS/Space Station 14 Launcher.app/Contents/Resources/bin/loader/Space Station 14.app/Contents/Resources/bin/"
pushd bin/publish/macOS
zip -r ../../../SS14.Launcher_macOS.zip *
popd
