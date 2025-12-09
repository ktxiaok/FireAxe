#!/bin/bash
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd) || exit
WORKSPACE=$(cd "$SCRIPT_DIR/../../.." && pwd) || exit

cd "$WORKSPACE" || exit 1
echo "Current Dir :$(pwd)"


AppDir="./Publish/Linux/AppImage/FireAxe.AppDir"
PubDir="./FireAxe.GUI/bin/Release/net10.0/linux-x64/publish"
Version="0.7.2"



# mkdir -p "$AppDir"/usr/bin/

# 清理构建内容
[ -e "./FireAxe-x86_64.AppImage" ] && rm ./FireAxe-x86_64.AppImage
[ -d "./FireAxe.GUI/FireAxe.AppDir/usr/bin" ] && rm -rf ./FireAxe.GUI/FireAxe.AppDir/usr/bin/*
[ -d "./FireAxe.GUI/bin/Release/net10.0/linux-x64/publish" ] && rm -rf ./FireAxe.GUI/bin/Release/net10.0/linux-x64/publish/*# 构建与打包



dotnet publish -c Release -r linux-x64 --self-contained true
cp "$PubDir"/* "$AppDir"/usr/bin/
appimagetool "$AppDir"
mv FireAxe-x86_64.AppImage FireAxe-x86_64_"$Version".AppImage
