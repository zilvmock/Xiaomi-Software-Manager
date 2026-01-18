using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace xsm.Converters;

public sealed class ContrastBrushConverter : IValueConverter
{
	private static readonly IBrush LightBrush = new SolidColorBrush(Colors.White);
	private static readonly IBrush DarkBrush = new SolidColorBrush(Colors.Black);

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is ISolidColorBrush solidBrush)
		{
			return GetContrastBrush(solidBrush.Color);
		}

		if (value is Color color)
		{
			return GetContrastBrush(color);
		}

		return DarkBrush;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}

	private static IBrush GetContrastBrush(Color color)
	{
		if (color.A < 255)
		{
			color = BlendWithBackground(color, Colors.White);
		}

		var luminance = GetRelativeLuminance(color);
		return luminance < 0.5 ? LightBrush : DarkBrush;
	}

	private static Color BlendWithBackground(Color foreground, Color background)
	{
		var alpha = foreground.A / 255.0;
		var invAlpha = 1 - alpha;

		return Color.FromRgb(
			(byte)Math.Round((foreground.R * alpha) + (background.R * invAlpha)),
			(byte)Math.Round((foreground.G * alpha) + (background.G * invAlpha)),
			(byte)Math.Round((foreground.B * alpha) + (background.B * invAlpha)));
	}

	private static double GetRelativeLuminance(Color color)
	{
		var r = SrgbToLinear(color.R / 255.0);
		var g = SrgbToLinear(color.G / 255.0);
		var b = SrgbToLinear(color.B / 255.0);
		return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
	}

	private static double SrgbToLinear(double channel)
	{
		return channel <= 0.03928
			? channel / 12.92
			: Math.Pow((channel + 0.055) / 1.055, 2.4);
	}
}
