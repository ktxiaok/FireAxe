![FireAxe Logo](FireAxe.GUI/Assets/AppLogo.ico)
# FireAxe
**English** | **[简体中文](README.zh-Hans.md)**

[Downloads](https://github.com/ktxiaok/FireAxe/releases)

FireAxe (formerly L4D2AddonAssistant) is an open-source GUI tool for managing Left 4 Dead 2 addons. It has the following features:
- Organize addons hierarchically through the concept of groups
- Support downloading workshop items and collections
- Download GameMaps addons by URL, with automatic ZIP, RAR and 7z extraction and VPK import
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
- For GameMaps, choose "New -> GameMaps Addon" and enter a details URL (for example, `https://www.gamemaps.com/details/24878`) or item ID. Downloads appear in the download list, and all VPKs are extracted into the addon group automatically. Clipboard link detection also supports GameMaps. Use Download or Check to retry a failure. If Cloudflare blocks the request, try the proxy in Settings or download in a browser and import the local VPK.
- The GameMaps section below Workshop in Settings has separate auto-update and clipboard detection switches. Check detects file revisions and applies the global or per-addon auto-update policy. The recursive policy commands also support GameMaps. Use Update GameMaps Item in the addon details for a manual update. Updates retain the group and matching VPKs' names, enablement, tags and other settings, and synchronize added or removed VPKs.
- You can create some groups to organize addons.
- Enable the addons you want and then click the menu item "Operations -> Push" to push the enablement state to the game.
- You can click the check button to keep the status up to date.

# Workshop collection subscriptions

- Choose **New -> Workshop collection subscription** and enter a public Left 4 Dead 2 collection ID or URL. The existing Workshop Collection command remains a one-time import.
- Subscriptions have a stacked-card icon with a sync badge and support the usual group enablement, tags, priority, dependencies and references.
- As with Workshop mods, edit the collection ID in its details or paste another collection URL to change the source. This cancels the old request and immediately reads the new collection. Existing mods are reconciled only after a successful read, following download/removal permissions and preserving shared mods and their settings. An invalid or unavailable new collection leaves existing mods intact.
- Collections are checked when a library opens and, by default, every 30 minutes while FireAxe is running. Settings has separate controls for automatic synchronization, its interval, downloading new mods, removing obsolete mods and enabling new mods. Per-subscription three-state checkboxes override these defaults; an indeterminate box inherits Settings. Group enable strategies still apply.
- Right-click a subscription and select **Synchronize collection subscriptions**, or use the button in its details. Manual synchronization bypasses automatic scheduling and still respects download/removal permissions.
- Downloads and removals are enabled by default. Only Workshop mods created by that subscription and still in its group are removed. Manually added content, GameMaps addons and mods moved elsewhere are preserved. Manually deleted or moved mods are not repeatedly recreated while they remain in the remote collection.
- Removal uses the existing file mechanism: recycle bin on Windows, deletion on other platforms. References to removed mods follow the existing invalid-reference behavior. Existing mods retain their names, enablement, tags and priority; file updates follow each mod's Workshop update policy.
- Linked collections support cycle detection and deduplication. Failed, private or incomplete responses never count as empty collections. Source IDs and ownership survive saves, restarts and `.addonroot` import/export.
- These are local FireAxe subscriptions and do not change your Steam account subscriptions. Collection reads use Steam's official [GetCollectionDetails endpoint](https://partner.steamgames.com/doc/webapi/ISteamRemoteStorage#GetCollectionDetails). Enable the desired subscription and mods, then push to the game as usual.

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
