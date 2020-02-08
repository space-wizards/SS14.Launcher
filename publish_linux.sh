#!/bin/sh

cd "$(dirname "$0")"

./download_net_runtime.py linux

# Clear out previous build.
rm -r **/bin bin/
rm SS14.Launcher_Linux.zip

dotnet publish SS14.Launcher/SS14.Launcher.csproj -c Release --no-self-contained -r linux-x64 /nologo

# Create intermediate directories.
mkdir -p bin/publish/Linux/bin
mkdir -p bin/publish/Linux/dotnet

cp PublishFiles/SS14.Launcher PublishFiles/SS14.desktop bin/publish/Linux/
cp SS14.Launcher/bin/Release/netcoreapp3.0/linux-x64/publish/* bin/publish/Linux/bin/
cp -r Dependencies/dotnet/linux/* bin/publish/Linux/dotnet/

cd bin/publish/Linux
zip -r ../../../SS14.Launcher_Linux.zip *
