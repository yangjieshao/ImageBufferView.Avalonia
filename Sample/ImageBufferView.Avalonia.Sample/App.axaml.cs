using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ImageBufferView.Avalonia.Sample.Views;
using Avalonia.Markup.Xaml;

namespace ImageBufferView.Avalonia.Sample
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
#if DEBUG
            this.AttachDeveloperTools();
            //this.AttachDeveloperTools((options) =>
            //{
            //    options.Gesture = new Avalonia.Input.KeyGesture(Avalonia.Input.Key.F11, Avalonia.Input.KeyModifiers.None);
            //    options.AutoConnectFromDesignMode = true;
            //});
#endif
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}