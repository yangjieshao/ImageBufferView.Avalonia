using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ImageBufferView.Avalonia.Sample.Views;

namespace ImageBufferView.Avalonia.Sample
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
#if DEBUG
            //this.AttachDeveloperTools();
            this.AttachDeveloperTools((options) =>
            {
                options.Gesture = new KeyGesture(Key.F11);
                options.AutoConnectFromDesignMode = true;
            });
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