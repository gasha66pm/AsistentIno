using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AsistentIno.ViewModels;
using ICSharpCode.AvalonEdit.Search;

namespace AsistentIno;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private SearchPanel? _searchPanel;
    private int _currentMatchIndex = 0;
    private List<int> _matchPositions = new();

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.AgentMessages.CollectionChanged += AgentMessages_CollectionChanged;
        Closed += MainWindow_Closed;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        // Bind Ctrl+F
        this.PreviewKeyDown += MainWindow_PreviewKeyDown;

        // Initialize SearchPanel for AvalonEdit
        _searchPanel = SearchPanel.Install(CodeEditor);
        _searchPanel.Visibility = Visibility.Collapsed;
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

    private void MainWindow_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
    {
        // Ctrl+F - Otvori Find panel
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            FindPanel.Visibility = Visibility.Visible;
            FindTextBox.Focus();
            FindTextBox.SelectAll();
            e.Handled = true;
        }

        // ESC - Zatvori Find panel
        if (e.Key == Key.Escape && FindPanel.Visibility == Visibility.Visible)
        {
            FindPanel.Visibility = Visibility.Collapsed;
            CodeEditor.Focus();
            e.Handled = true;
        }
    }

    private void FindTextBox_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                FindPrevious_Click(null, null);
            }
            else
            {
                FindNext_Click(null, null);
            }
            e.Handled = true;
        }
    }

    private void FindTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        PerformSearch();
    }

    private void PerformSearch()
    {
        string searchText = FindTextBox.Text;

        if (string.IsNullOrEmpty(searchText))
        {
            MatchCountLabel.Text = "";
            _matchPositions.Clear();
            _currentMatchIndex = 0;
            return;
        }

        _matchPositions.Clear();
        var document = CodeEditor.Document;
        int offset = 0;

        while (offset < document.TextLength)
        {
            int index = document.Text.IndexOf(searchText, offset, StringComparison.CurrentCultureIgnoreCase);
            if (index == -1)
                break;

            _matchPositions.Add(index);
            offset = index + searchText.Length;
        }

        _currentMatchIndex = 0;
        UpdateMatchCount();

        if (_matchPositions.Count > 0)
        {
            HighlightMatch(0);
        }
    }

    private void HighlightMatch(int matchIndex)
    {
        if (matchIndex < 0 || matchIndex >= _matchPositions.Count)
            return;

        string searchText = FindTextBox.Text;
        int position = _matchPositions[matchIndex];

        CodeEditor.Select(position, searchText.Length);
        CodeEditor.ScrollToLine(CodeEditor.Document.GetLineByOffset(position).LineNumber);
    }

    private void UpdateMatchCount()
    {
        if (_matchPositions.Count == 0)
        {
            MatchCountLabel.Text = "Nema rezultata";
        }
        else
        {
            MatchCountLabel.Text = $"{_currentMatchIndex + 1}/{_matchPositions.Count}";
        }
    }

    private void FindNext_Click(object? sender, RoutedEventArgs? e)
    {
        if (_matchPositions.Count == 0)
            return;

        _currentMatchIndex = (_currentMatchIndex + 1) % _matchPositions.Count;
        HighlightMatch(_currentMatchIndex);
        UpdateMatchCount();
    }

    private void FindPrevious_Click(object? sender, RoutedEventArgs? e)
    {
        if (_matchPositions.Count == 0)
            return;

        _currentMatchIndex = (_currentMatchIndex - 1 + _matchPositions.Count) % _matchPositions.Count;
        HighlightMatch(_currentMatchIndex);
        UpdateMatchCount();
    }

    private void CloseFindPanel_Click(object? sender, RoutedEventArgs e)
    {
        FindPanel.Visibility = Visibility.Collapsed;
        CodeEditor.Focus();
    }
}