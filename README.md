![FireAxe Logo](FireAxe.GUI/Assets/AppLogo.ico)
# FireAxe
**English** | **[简体中文](README.zh-Hans.md)**

[Downloads](https://github.com/ktxiaok/FireAxe/releases)

FireAxe (formerly L4D2AddonAssistant) is an open-source GUI tool for managing Left 4 Dead 2 addons. It has the following features:
- Organize addons hierarchically through the concept of groups
- Support downloading workshop items and collections
- Addon enablement state management
- Addon tag management
- VPK conflict check system
- Addon priority management
- Addon dependency management
- Addon problem check system
- Support import and export of addons 
- Local VPK files management
- VPK information display
- Workshop item information display
- Support search and filters of addons
- ...

This project is currently in early development. More features will be added in the future.

![Main Window](Images/MainWindow.png)
![Main Window and Download List Window](Images/MainWindowAndDownloadListWindow.png)
![VPK Conflict List Window](Images/VpkConflictListWindow.png)
## Getting Started
- Before starting, you need to have a directory where addons are stored. (NOTE: This directory is NOT "left4dead2/addons" or "left4dead2/addons/workshop"!)
- Open FireAxe and click the menu item "File -> Open Directory" to open the directory.
- Click the menu item "File -> Settings" and set the game path.
- If you have local VPK files, put them into the directory. Then click the menu item "File -> Import" to import them.
- If you want workshop items, right click to open the context menu and click the menu item "New -> Workshop Addon". Then write the item id. FireAxe will download files you need automatically.
- You can create some groups to organize addons.
- Enable the addons you want and then click the menu item "Operations -> Push" to push the enablement state to the game.
- You can click the check button to keep the status up to date.
