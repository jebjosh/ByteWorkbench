using System.Windows;
using ByteWorkbench.ViewModels;

namespace ByteWorkbench;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
