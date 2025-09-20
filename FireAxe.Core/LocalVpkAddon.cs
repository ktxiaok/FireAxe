using System;

namespace FireAxe
{
    public class LocalVpkAddon : VpkAddon
    {
        protected LocalVpkAddon()
        {

        }

        public override string? FullVpkFilePath => FullFilePath;

        public override string FileExtension => ".vpk";

        public override Type SaveType => typeof(LocalVpkAddonSave);

        protected override void OnCreateSave(AddonNodeSave save)
        {
            base.OnCreateSave(save);

            var save1 = (LocalVpkAddonSave)save;
        }

        protected override void OnLoadSave(AddonNodeSave save)
        {
            base.OnLoadSave(save);

            var save1 = (LocalVpkAddonSave)save;
        }
    }
}
