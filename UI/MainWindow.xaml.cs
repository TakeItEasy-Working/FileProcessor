using System.Windows;
using UI.ViewModels;

namespace UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnCardResize(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (sender is FrameworkElement thumb && thumb.DataContext is DisplayCardViewModel vm)
            {
                vm.CardWidth = Math.Max(300, vm.CardWidth + e.HorizontalChange);
            }
        }
    }
}