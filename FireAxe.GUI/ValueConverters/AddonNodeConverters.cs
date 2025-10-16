using System;
using Avalonia.Data.Converters;
using FireAxe.ViewModels;

namespace FireAxe.ValueConverters;

public static class AddonNodeConverters
{
    public static IValueConverter ToSimpleViewModel { get; } = new FuncValueConverter<AddonNode?, AddonNodeSimpleViewModel?>(addon =>
    {
        if (addon is null)
        {
            return null;
        }
        return new AddonNodeSimpleViewModel(addon);
    });
}