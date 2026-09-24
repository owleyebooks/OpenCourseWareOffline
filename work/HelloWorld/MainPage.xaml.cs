namespace HelloWorld;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	// TEMPORARY control diagnostic (screenshot-run branch only): logs MAUI-side
	// bounds so the CI verdict does not depend on flaky screenshots. Remove
	// before any durable integration.
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		try
		{
			await Task.Delay(3000);
			foreach (var v in new VisualElement[] { HwLabel, HwButton })
				System.Console.WriteLine(
					$"HWLAYOUT: {v.GetType().Name} x={v.X} y={v.Y} w={v.Width} h={v.Height} visible={v.IsVisible}");
			System.Console.WriteLine("HWLAYOUT: done");
		}
		catch (System.Exception ex)
		{
			System.Console.WriteLine("HWLAYOUT: EXCEPTION " + ex.GetType().Name);
		}
	}
}
