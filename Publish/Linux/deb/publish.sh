#!/bin/bash


Version="0.7.2"
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd) || exit
WORKSPACE=$(cd "$SCRIPT_DIR/../../.." && pwd) || exit
PubDir="./FireAxe.GUI/bin/Release/net10.0/linux-x64/publish"
DebDir="./Publish/Linux/deb"

cd "$WORKSPACE" || exit 1
echo "Current Dir :$(pwd)"


# Clean-up

rm -rf "$PubDir"
rm -rf ./staging_folder/

# update control version
if grep -q "^Version:" "$DebDir"/control; then

    sed -i "s/^Version:.*/Version: $Version/" "$DebDir"/control
    echo "✅ Version updated to $Version"

else

    echo "❌ No 'Version:' field found in $DebDir/control"

    exit 1

fi

# .NET publish
# self-contained is recommended, so final users won't need to install .NET
dotnet publish -c Release -r linux-x64 --self-contained true 
# Staging directory
mkdir staging_folder

# Debian control file
mkdir ./staging_folder/DEBIAN
cp "$DebDir"/control ./staging_folder/DEBIAN

# Starter script
mkdir ./staging_folder/usr
mkdir ./staging_folder/usr/bin
cp "$DebDir"/fireaxe ./staging_folder/usr/bin/fireaxe
chmod +x ./staging_folder/usr/bin/fireaxe # set executable permissions to starter script

# Other files
mkdir ./staging_folder/usr/lib
mkdir ./staging_folder/usr/lib/fireaxe
cp -f -a "$PubDir"/* ./staging_folder/usr/lib/fireaxe/ # copies all files from publish dir
chmod -R a+rX ./staging_folder/usr/lib/fireaxe/ # set read permissions to all files
chmod +x ./staging_folder/usr/lib/fireaxe/FireAxe # set executable permissions to main executable

# Desktop shortcut
mkdir ./staging_folder/usr/share
mkdir ./staging_folder/usr/share/applications
cp "$DebDir"/fireaxe.desktop ./staging_folder/usr/share/applications/fireaxe.desktop
# Desktop icon
# A 1024px x 1024px PNG, like VS Code uses for its icon
# mkdir ./staging_folder/usr/share/pixmaps
# cp "$DebDir"/FireAxe.png ./staging_folder/usr/share/pixmaps/myprogram.png

# Hicolor icons
mkdir ./staging_folder/usr/share/icons
mkdir ./staging_folder/usr/share/icons/hicolor
mkdir ./staging_folder/usr/share/icons/hicolor/scalable
mkdir ./staging_folder/usr/share/icons/hicolor/scalable/apps
cp "$DebDir"/FireAxe.png ./staging_folder/usr/share/icons/hicolor/scalable/apps/FireAxe.png

# Make .deb file
dpkg-deb --root-owner-group --build ./staging_folder/ ./fireaxe_"$Version"_amd64.deb
