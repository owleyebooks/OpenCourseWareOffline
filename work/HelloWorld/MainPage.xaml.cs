namespace HelloWorld;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	// TEMPORARY time-series diagnostic (screenshot-run branch only): samples
	// MAUI-side bounds every 2s for 60s to determine whether the 2^24
	// corruption is transient startup state or permanent. Remove before any
	// durable integration.
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		try
		{
			var d = DeviceDisplay.Current.MainDisplayInfo;
			System.Console.WriteLine(
				$"HWLAYOUT2: display w={d.Width} h={d.Height} density={d.Density} orientation={d.Orientation} rate={d.RefreshRate}");
			for (int i = 0; i < 30; i++)
			{
				await Task.Delay(2000);
				foreach (var v in new VisualElement[] { HwLabel, HwButton })
					System.Console.WriteLine(
						$"HWLAYOUT2: t={i * 2 + 2}s {v.GetType().Name} x={v.X} y={v.Y} w={v.Width} h={v.Height} visible={v.IsVisible}");
			}
			System.Console.WriteLine("HWLAYOUT2: done");
		}
		catch (System.Exception ex)
		{
			System.Console.WriteLine("HWLAYOUT2: EXCEPTION " + ex.GetType().Name);
		}
	}
}
