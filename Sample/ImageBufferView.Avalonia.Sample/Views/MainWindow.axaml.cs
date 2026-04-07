using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ImageBufferView.Avalonia.Sample.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}