using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Cedx.Core.Models;

namespace Cedx.App.Common;

// A meter is drawn only when both capacity and free space were actually reported.
public sealed class DiskMeterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var disk = value as StorageDevice;
        if (value is AssetRecord asset)
            disk = asset.LocalDisks.FirstOrDefault(d => d.DriveLetter.Equals("C:", StringComparison.OrdinalIgnoreCase));
        var known = disk?.TotalGb is > 0 && disk.FreeGb is >= 0 && disk.FreeGb <= disk.TotalGb &&
            double.IsFinite(disk.TotalGb.Value) && double.IsFinite(disk.FreeGb.Value);
        if (parameter as string == "Visibility") return known ? Visibility.Visible : Visibility.Collapsed;
        return known ? 100d * disk!.FreeGb!.Value / disk.TotalGb!.Value : 0d;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
