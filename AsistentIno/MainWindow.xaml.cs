using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
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

    private void MarkdownScrollViewer_PreviewMouseWheel(
    object sender,
    MouseWheelEventArgs e)
    {
        AgentMessagesScrollViewer.ScrollToVerticalOffset(
            AgentMessagesScrollViewer.VerticalOffset - e.Delta);

        e.Handled = true;
    }

    private void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
            return;

        var content = button.CommandParameter?.ToString();

        if (string.IsNullOrEmpty(content))
            return;

        System.Windows.Clipboard.SetText(content);
    }

    private void OpenCompileWindow_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Views.CompileWindow
        {
            Owner = this
        };

        var result = dlg.ShowDialog();
        if (result == true)
        {
            // TODO: handle post-compile/upload actions if needed
        }
    }
}