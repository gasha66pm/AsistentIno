using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;
using AsistentIno.ViewModels;

namespace AsistentIno;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.AgentMessages.CollectionChanged += AgentMessages_CollectionChanged;
        Closed += MainWindow_Closed;
    }

    private void AgentMessages_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add)
            return;

        // Sačekati da ItemsControl iscrta novu poruku.
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() => AgentMessagesScrollViewer.ScrollToEnd()));
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _viewModel.AgentMessages.CollectionChanged -= AgentMessages_CollectionChanged;
        Closed -= MainWindow_Closed;
    }
}