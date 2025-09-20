using Serilog;
using SteamDatabase.ValvePak;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace FireAxe
{
    public abstract class VpkAddon : AddonNode
    {
        private int _vpkPriority = 0;

        private readonly ObservableCollection<string> _conflictIgnoringFiles = new();
        private readonly ReadOnlyObservableCollection<string> _conflictIgnoringFilesReadOnly;
        private readonly HashSet<string> _conflictIgnoringFileSet = new HashSet<string>();

        internal readonly ObservableCollection<string> _conflictingFiles = new();
        private readonly ReadOnlyObservableCollection<string> _conflictingFilesReadOnly;
        internal readonly ObservableCollection<Guid> _conflictingAddonIds = new();
        private readonly ReadOnlyObservableCollection<Guid> _conflictingAddonIdsReadOnly;

        private WeakReference<VpkAddonInfo?> _addonInfo = new(null);

        protected VpkAddon()
        {
            _conflictIgnoringFilesReadOnly = new(_conflictIgnoringFiles);
            _conflictingFilesReadOnly = new(_conflictingFiles);
            _conflictingAddonIdsReadOnly = new(_conflictingAddonIds);
        }

        public int VpkPriority
        {
            get => _vpkPriority;
            set
            {
                if (NotifyAndSetIfChanged(ref _vpkPriority, value))
                {
                    Root.RequestSave = true;
                }
            }
        }

        public ReadOnlyObservableCollection<string> ConflictIgnoringFiles => _conflictIgnoringFilesReadOnly;

        public ReadOnlyObservableCollection<string> ConflictingFiles => _conflictingFilesReadOnly;

        public ReadOnlyObservableCollection<Guid> ConflictingAddonIds => _conflictingAddonIdsReadOnly;

        public abstract string? FullVpkFilePath
        {
            get;
        }

        public override Type SaveType => typeof(VpkAddonSave);

        public override bool RequireFile => true;

        protected override long? GetFileSize()
        {
            var path = FullVpkFilePath;
            if (path == null)
            {
                return null;
            }

            try
            {
                if (File.Exists(path))
                {
                    return new FileInfo(path).Length;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during VpkAddon.GetFileSize.");
            }

            return null;
        }

        protected override Task<byte[]?> DoGetImageAsync(CancellationToken cancellationToken)
        {
            string? vpkPath = FullVpkFilePath;
            if (vpkPath == null)
            {
                return Task.FromResult<byte[]?>(null);
            }
            return Task.Run(() =>
            {
                if (TryCreatePackage(vpkPath, out var pak))
                {
                    using (pak)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return VpkUtils.GetAddonImage(pak);
                    }
                }
                return null;
            }, cancellationToken);
        }

        public VpkAddonInfo? RetrieveInfo()
        {
            if (!_addonInfo.TryGetTarget(out var addonInfo))
            {
                if (TryCreatePackage(FullVpkFilePath, out var pak))
                {
                    using (pak)
                    {
                        addonInfo = VpkUtils.GetAddonInfo(pak);
                        if (addonInfo != null)
                        {
                            _addonInfo.SetTarget(addonInfo);
                        }
                    }
                }
            }
            return addonInfo;
        }

        public override void ClearCaches()
        {
            base.ClearCaches();

            _addonInfo.SetTarget(null);
        }

        public bool AddConflictIgnoringFile(string file)
        {
            ArgumentNullException.ThrowIfNull(file);

            bool result = _conflictIgnoringFileSet.Add(file);
            if (!result)
            {
                return false;
            }
            _conflictIgnoringFiles.Add(file);
            Root.RequestSave = true;

            return result;
        }

        public bool RemoveConflictIgnoringFile(string file)
        {
            ArgumentNullException.ThrowIfNull(file);

            bool result = _conflictIgnoringFileSet.Remove(file);
            if (!result)
            {
                return false;
            }
            _conflictIgnoringFiles.Remove(file);
            Root.RequestSave = true;

            return result;
        }

        public void ClearConflictIgnoringFiles()
        {
            _conflictIgnoringFileSet.Clear();
            _conflictIgnoringFiles.Clear();
            Root.RequestSave = true;
        }

        public bool ContainsConflictIgnoringFile(string file)
        {
            ArgumentNullException.ThrowIfNull(file);

            return _conflictIgnoringFileSet.Contains(file);
        }

        protected override void OnCreateSave(AddonNodeSave save)
        {
            base.OnCreateSave(save);
            var save1 = (VpkAddonSave)save;
            save1.VpkPriority = VpkPriority;
            save1.ConflictIgnoringFiles = [.. ConflictIgnoringFiles];
        }

        protected override void OnLoadSave(AddonNodeSave save)
        {
            base.OnLoadSave(save);
            var save1 = (VpkAddonSave)save;
            VpkPriority = save1.VpkPriority;
            ClearConflictIgnoringFiles();
            foreach (var file in save1.ConflictIgnoringFiles)
            {
                AddConflictIgnoringFile(file);
            }
        }

        private static bool TryCreatePackage(string? path, [NotNullWhen(true)] out Package? pak)
        {
            pak = null;
            if (path == null)
            {
                return false;
            }
            try
            {
                pak = new Package();
                pak.Read(path);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Exception in VpkAddon.TryCreatePackage.");
                if (pak != null)
                {
                    pak.Dispose();
                    pak = null;
                }
                return false;
            }
        }
    }
}
