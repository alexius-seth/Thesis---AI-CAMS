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

    public class CheatingScoreToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush LowBrush = new(Color.FromRgb(0x10, 0xB9, 0x81));    // #10B981 (Green)
        private static readonly SolidColorBrush MediumBrush = new(Color.FromRgb(0xF5, 0x9E, 0x0B)); // #F59E0B (Amber)
        private static readonly SolidColorBrush HighBrush = new(Color.FromRgb(0xEF, 0x44, 0x44));   // #EF4444 (Red)

        static CheatingScoreToBrushConverter()
        {
            LowBrush.Freeze();
            MediumBrush.Freeze();
            HighBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double score = 0;
            if (value is double d) score = d;
            else if (value is float f) score = f;
            else if (value is int i) score = i;
            else if (value is decimal dec) score = (double)dec;
            else if (value != null && double.TryParse(value.ToString(), out double parsed)) score = parsed;

            if (score < 30.0) return LowBrush;
            if (score < 70.0) return MediumBrush;
            return HighBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class CheatingScoreToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double score = 0;
            if (value is double d) score = d;
            else if (value is float f) score = f;
            else if (value is int i) score = i;
            else if (value is decimal dec) score = (double)dec;
            else if (value != null && double.TryParse(value.ToString(), out double parsed)) score = parsed;

            if (score < 30.0) return Color.FromRgb(0x10, 0xB9, 0x81);
            if (score < 70.0) return Color.FromRgb(0xF5, 0x9E, 0x0B);
            return Color.FromRgb(0xEF, 0x44, 0x44);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}