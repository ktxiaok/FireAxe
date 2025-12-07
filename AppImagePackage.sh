mkdir -p ./FireAxe.GUI/FireAxe.AppDir/usr/bin/

# 清理构建内容
rm ./FireAxe-x86_64.AppImage
rm -rf ./FireAxe.GUI/FireAxe.AppDir/usr/bin/*
rm -rf ./FireAxe.GUI/bin/Release/net10.0/linux-x64/publish/* 

# 构建与打包
dotnet publish -c Release -r linux-x64 --self-contained true
cp ./FireAxe.GUI/bin/Release/net10.0/linux-x64/publish/* ./FireAxe.GUI/FireAxe.AppDir/usr/bin/
appimagetool ./FireAxe.GUI/FireAxe.AppDir
