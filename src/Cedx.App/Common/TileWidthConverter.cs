using System.Globalization;
using System.Windows.Data;

namespace Cedx.App.Common;

public sealed class TileWidthConverter : IValueConverter
{
    private const double MinimumTileWidth = 300d;
    private const double TileGap = 14d;
    private const double ViewPaddingAndScrollbar = 44d;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double actualWidth || double.IsNaN(actualWidth) || actualWidth <= 0)
        {
            return MinimumTileWidth;
        }

        var available = Math.Max(MinimumTileWidth, actualWidth - ViewPaddingAndScrollbar);
        var columns = Math.Max(1, (int)Math.Floor((available + TileGap) / (MinimumTileWidth + TileGap)));
        return Math.Floor((available - (TileGap * (columns - 1))) / columns);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
