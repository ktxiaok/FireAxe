using System;
using System.Collections.Generic;
using System.Globalization;
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

    public static IMultiValueConverter IdToSimpleViewModel { get; } = new IdToSimpleViewModelConverter();

    private class IdToSimpleViewModelConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count != 2)
            {
                return null;
            }

            var addonRoot = values[0] as AddonRoot;
            if (addonRoot is null)
            {
                return null;
            }
            if (!addonRoot.IsValid)
            {
                return null;
            }

            var id = values[1] as Guid?;
            if (id is null)
            {
                return null;
            }

            if (id.Value == Guid.Empty)
            {
                return null;
            }

            return new AddonNodeSimpleViewModel(addonRoot, id.Value);
        }
    }

    public static IValueConverter ToListItemViewModel { get; } =
        new FuncValueConverter<AddonNode?, AddonNodeListItemViewModel?>(addon => addon is null ? null : new AddonNodeListItemViewModel(addon));

    public static IValueConverter ToViewModel { get; } =
        new FuncValueConverter<AddonNode?, AddonNodeViewModel?>(addon => addon is null ? null : AddonNodeViewModel.Create(addon));
}