using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls
{
	public class InfoMessage : Label
	{
		public static readonly StyledProperty<int> IconSizeProperty =
			AvaloniaProperty.Register<ContentArea, int>(nameof(IconSize), 20);

		public int IconSize
		{
			get => GetValue(IconSizeProperty);
			set => SetValue(IconSizeProperty, value);
		}
	}
}