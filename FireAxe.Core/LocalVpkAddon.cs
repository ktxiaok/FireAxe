using System;
using System.ComponentModel;

namespace FireAxe;

public class LocalVpkAddon : VpkAddon
{
    protected LocalVpkAddon()
    {
        PropertyChanged += OnPropertyChanged;
    }

    public override string? VpkFilePath => FullFilePath;

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

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var name = e.PropertyName;
        if (name == nameof(FullFilePath))
        {
            NotifyChanged(nameof(VpkFilePath));
        }
    }
}
