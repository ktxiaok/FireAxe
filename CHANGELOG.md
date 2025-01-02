# v0.6.0
- change: rename L4D2AddonAssistant to FireAxe
- add: addon tag management
- add: add the ability to apply tags from workshop
- add: add some menu items to AddonNodeExplorerView
- fix: push will fail if there're files with the same name
- improve: improve performance
- improve: add version check when opening directory
- improve: auto remove WorkshopVpkFileNotLoadProblem when the download completes
- change: rename the menu item "File->Open" to "File->Open Directory"
- add: add menu item "File->Close Directory"
- bugfixes
# v0.5.1
- fix: too many download tasks will crash the program (issue #7)
- fix: cancelling a workshop collection creation will crash the program
- fix: avoid circular references in linked workshop collections
- add: moving addons display
- add: support Ctrl+X and Ctrl+V to move addons and support mouse side button to goto the parent group
# v0.5.0
- add: workshop collection creation
- add: auto set the name of the workshop addon after download
- add: auto redownload items (disabled by default)
- add: flat vpk list window
- add: open workshop page button
# v0.4.1
- improve: select all text when edit text
- fix: some workshop links can't be parsed
- improve: auto refresh images after downloading
- fix: add try/catch block on opening clipboard
- fix: "Single" strategy of enablement problem
# v0.4.0
- fix: renaming bug
- improve: change the deletion behavior from direct deletion to move to recycle bin (Windows only)
- add: show in the file explorer (Windows only)
- add: auto detect workshop item link in the clipboard
- add: vpk priority setting
# v0.3.0
- add: creation time display
- add: sort by creation time
- add: randomly select
- fix: fix some bugs
- add: download list window
- add: an error dialog box will pop up if the user opens the directory "left4dead2/addons"
# v0.2.0
- improve: AddonNodeView
- fix: WorkshopVpkAddon.FullVpkFilePath is sometimes not updated
- improve: WorkshopVpkAddonSectionView
- improve: check addons after import and check addons before push
- add: check for updates
- add: addon problems display