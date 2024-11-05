using System;

namespace L4D2AddonAssistant
{
    public class LocalVpkAddon : VpkAddon
    {
        private Guid _vpkGuid = Guid.Empty;

        public LocalVpkAddon(AddonRoot root, AddonGroup? group) : base(root, group)
        {

        }

        public override string? FullVpkFilePath => FullFilePath;

        public override string FileExtension => ".vpk";

        public override Type SaveType => typeof(LocalVpkAddonSave);

        public Guid VpkGuid
        {
            get => _vpkGuid;
            set
            {
                if (NotifyAndSetIfChanged(ref _vpkGuid, value))
                {
                    Root.RequestSave = true;
                }
            }
        }

        protected override void OnCreateSave(AddonNodeSave save)
        {
            base.OnCreateSave(save);

            var save1 = (LocalVpkAddonSave)save;
            save1.VpkGuid = VpkGuid;
        }

        protected override void OnLoadSave(AddonNodeSave save)
        {
            base.OnLoadSave(save);

            var save1 = (LocalVpkAddonSave)save;
            VpkGuid = save1.VpkGuid;
        }

        public void ValidateVpkGuid()
        {
            if (VpkGuid == Guid.Empty)
            {
                GenerateVpkGuid();
            }
        }

        public void GenerateVpkGuid()
        {
            VpkGuid = Guid.NewGuid();
        }
    }
}
