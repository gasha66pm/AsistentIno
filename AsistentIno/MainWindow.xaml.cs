using System;
using System.Windows;
using AsistentIno.ViewModels;

namespace AsistentIno
{
    public partial class MainWindow : Window
    {
        public MainWindow(ViewModels.MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
