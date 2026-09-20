using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ACPAMS.Converters
{
    public class RiskLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string risk)
            {
                return risk.ToLower() switch
                {
                    "high" => new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                    "medium" => new SolidColorBrush(Color.FromRgb(180, 83, 9)),
                    "low" => new SolidColorBrush(Color.FromRgb(21, 128, 61)),
                    _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
                };
            }
            return new SolidColorBrush(Color.FromRgb(100, 116, 139));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class RiskLevelToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string risk)
            {
                return risk.ToLower() switch
                {
                    "high" => new SolidColorBrush(Colors.White),
                    "medium" => new SolidColorBrush(Colors.White),
                    "low" => new SolidColorBrush(Colors.White),
                    _ => new SolidColorBrush(Colors.Black)
                };
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class FlagSeverityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string action)
            {
                return action switch
                {
                    "NoFaceDetected" or "AltTabAttempt" or "WindowFocusLost" or "MultipleFacesDetected" => new SolidColorBrush(Color.FromRgb(255, 248, 248)),
                    "HeadTurnedLeft" or "HeadTurnedRight" or "HeadTiltedDown" => new SolidColorBrush(Color.FromRgb(255, 251, 235)),
                    "ClipboardAttempt" => new SolidColorBrush(Color.FromRgb(235, 255, 240)),
                    _ => System.Windows.Media.Brushes.Transparent
                };
            }
            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class InitialsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string name && !string.IsNullOrEmpty(name))
            {
                var parts = name.Split(' ');
                if (parts.Length >= 2)
                    return $"{parts[0][0]}{parts[1][0]}".ToUpper();
                if (parts.Length == 1 && parts[0].Length >= 2)
                    return parts[0][..2].ToUpper();
                if (parts.Length == 1 && parts[0].Length == 1)
                    return parts[0].ToUpper();
                return "?";
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class PercentageToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                return Math.Min(200, Math.Max(0, percentage));
            }
            if (value is int intPercentage)
            {
                return Math.Min(200, Math.Max(0, intPercentage));
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}