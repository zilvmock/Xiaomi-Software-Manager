using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace xsm.Converters;

public sealed class TintBrushConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (!TryGetColor(value, out var color))
		{
			return value;
		}

		var amount = GetAmount(parameter);
		var tinted = Tint(color, amount);
		return new SolidColorBrush(tinted);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}

	private static bool TryGetColor(object? value, out Color color)
	{
		switch (value)
		{
			case ISolidColorBrush solidBrush:
				color = solidBrush.Color;
				return true;
			case Color directColor:
				color = directColor;
				return true;
			default:
				color = default;
				return false;
		}
	}

	private static double GetAmount(object? parameter)
	{
		if (parameter is double direct)
		{
			return Clamp01(direct);
		}

		if (parameter is string text &&
			double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
		{
			return Clamp01(parsed);
		}

		return 0.2;
	}

	private static Color Tint(Color color, double amount)
	{
		return Color.FromArgb(
			color.A,
			TintChannel(color.R, amount),
			TintChannel(color.G, amount),
			TintChannel(color.B, amount));
	}

	private static byte TintChannel(byte channel, double amount)
	{
		var value = channel + ((255 - channel) * amount);
		return (byte)Math.Round(value);
	}

	private static double Clamp01(double value)
	{
		if (value < 0)
		{
			return 0;
		}

		return value > 1 ? 1 : value;
	}
}
