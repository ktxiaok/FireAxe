using Serilog;
using SteamDatabase.ValvePak;
using System;
using System.Diagnostics.CodeAnalysis;

namespace L4D2AddonAssistant
{
    public abstract class VpkAddon : AddonNode
    {
        private WeakReference<byte[]> _image = new(null!);

        private WeakReference<VpkAddonInfo> _addonInfo = new(null!);

        public VpkAddon(AddonRoot root, AddonGroup? group) : base(root, group)
        {
            root.RegisterVpkAddon(this);
        }

        public abstract string? VpkFilePath
        {
            get;
        }

        public string? FullVpkFilePath
        {
            get
            {
                var path = VpkFilePath;
                if (path == null)
                {
                    return null;
                }
                return Path.Join(Root.DirectoryPath, path);
            }
        }

        public override Type SaveType => typeof(VpkAddonSave);

        public override bool RequireFile => true;

        public override byte[]? RetrieveImage()
        {
            _image.TryGetTarget(out var image);
            if (image == null)
            {
                if (TryCreatePackage(out var pak))
                {
                    using (pak)
                    {
                        image = VpkUtils.GetAddonImage(pak);
                        if (image != null)
                        {
                            _image.SetTarget(image);
                        }
                    }
                }
            }
            return image;
        }

        public override void InvalidateImage()
        {
            _image.SetTarget(null!);
        }

        public VpkAddonInfo? RetrieveInfo()
        {
            _addonInfo.TryGetTarget(out var addonInfo);
            if (addonInfo == null)
            {
                if (TryCreatePackage(out var pak))
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

        public void InvalidateInfo()
        {
            _addonInfo.SetTarget(null!);
        }

        private bool TryCreatePackage([NotNullWhen(true)] out Package? pak)
        {
            pak = null;
            var path = FullVpkFilePath;
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

        protected override void OnCreateSave(AddonNodeSave save)
        {
            base.OnCreateSave(save);
            var save1 = (VpkAddonSave)save;
        }

        protected override void OnLoadSave(AddonNodeSave save)
        {
            base.OnLoadSave(save);
            var save1 = (VpkAddonSave)save;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Root.UnregisterVpkAddon(this);
        }
    }
}
