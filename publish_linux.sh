#!/bin/bash

cd "$(dirname "$0")"

./download_net_runtime.py linux

# Clear out previous build.
rm -r **/bin bin/publish/Linux
rm SS14.Launcher_Linux.zip

dotnet publish SS14.Launcher/SS14.Launcher.csproj /p:FullRelease=True -c Release --no-self-contained -r linux-x64 /nologo /p:RobustILLink=true
dotnet publish SS14.Loader/SS14.Loader.csproj -c Release --no-self-contained -r linux-x64 /nologo

# Create intermediate directories.
mkdir -p bin/publish/Linux/bin
mkdir -p bin/publish/Linux/bin/loader
mkdir -p bin/publish/Linux/dotnet

cp PublishFiles/SS14.Launcher PublishFiles/SS14.desktop bin/publish/Linux/
cp SS14.Launcher/bin/Release/net9.0/linux-x64/publish/* bin/publish/Linux/bin/
cp SS14.Loader/bin/Release/net9.0/linux-x64/publish/* bin/publish/Linux/bin/loader
cp -r Dependencies/dotnet/linux/* bin/publish/Linux/dotnet/

cd bin/publish/Linux
zip -r ../../../SS14.Launcher_Linux.zip *
