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
- Support reference addons to quickly switch between different play schemes
- ...

This project is currently in early development. More features will be added in the future.

![Main Window](Images/MainWindow.png)
![Main Window and Download List Window](Images/MainWindowAndDownloadListWindow.png)
![VPK Conflict List Window](Images/VpkConflictListWindow.png)
# Getting Started
- Before starting, you need to have a directory where addons are stored. (NOTE: This directory is NOT "left4dead2/addons" or "left4dead2/addons/workshop"!)
- Open FireAxe and click the menu item "File -> Open Directory" to open the directory.
- Click the menu item "File -> Settings" and set the game path.
- If you have local VPK files, put them into the directory. Then click the menu item "File -> Import" to import them.
- If you want workshop items, right click to open the context menu and click the menu item "New -> Workshop Addon". Then write the item id. FireAxe will download files you need automatically.
- You can create some groups to organize addons.
- Enable the addons you want and then click the menu item "Operations -> Push" to push the enablement state to the game.
- You can click the check button to keep the status up to date.

# Questions and Answers
## Why can't I see my addons in the game?
Please ensure that your addon itself is enabled and all groups containing the addon are enabled. Ensure you have clicked the push button.
If the game is already running before the push, you need to refresh the addons in the game menu after the push.

## What's the best way to migrate my subscribed Workshop addons to FireAxe?
You can use the Workshop VPK Finder in the Tools menu.

## Do I need to unsubscribe from the Workshop addons after migration?
Yes. FireAxe will not manage the subscribed Workshop addons.

## How to troubleshoot push failures?
Please ensure your file system is NTFS or any other file system capable of creating symbolic links.

## How does the priority system work?
For files with identical names existing across multiple VPKs, the game will ultimately load the one with the highest priority.
Each addon has its own individual priority and a global priority.
The global priority is the sum of the addon's own priority and the priorities of all groups containing the addon.
It is the global priority that ultimately takes effect.
For example, if you want to add a collection of new addons and override the current addons, you can place these addons into a group and assign the group a higher priority.

## How do I use reference addons?
A reference addon is a reference to another addon, with its own enablement state and priority.
For example, you can create reference addons based on a collection of addons, which essentially configures another play scheme and allows for quick switching between different schemes.

## What's the purpose of importing/exporting .addonroot files?
.addonroot is a file format used by FireAxe to store addon information (such as enablement state, Workshop ID, etc.).
For example, you can export a collection of Workshop addons as an .addonroot file and share it with friends.
Your friend can then import this file to get all the addons you exported just by a one click.

# License
Apache-2.0 license