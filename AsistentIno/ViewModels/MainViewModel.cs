using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using AsistentIno.Models;
using AsistentIno.Services;

namespace AsistentIno.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly FileService _fileService;
    private readonly IArduinoCliService _arduinoCliService;
    private readonly ToolRegistry _toolRegistry;
    private readonly LLMProviderFactory _providerFactory;
    private readonly INotificationService? _notificationService;

    private CancellationTokenSource? _agentCancellation;
    private TaskCompletionSource<InteractiveAskResponse>? _pendingInteractiveAsk;
    private InteractiveAskRequest? _pendingInteractiveRequest;
    private string _selectedFilePath = "";
    private string _selectedFileContent = "";
    private string _userMessage = "";
    private string _interactivePrompt = "";
    private MessageAttachment? _pendingAttachment;
    private bool _isAgentBusy;
    private bool _isWaitingForInteractiveAnswer;
    private AgentConfig? _selectedAgent;
    private string _statusMessage = "";
    private string _dataFolder = "";

    private bool _isSyntaxHighlightingEnabled = true;

    public bool IsSyntaxHighlightingEnabled
    {
        get => _isSyntaxHighlightingEnabled;
        set
        {
            if (_isSyntaxHighlightingEnabled == value)
                return;

            _isSyntaxHighlightingEnabled = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> OpenFiles { get; } = [];
    public ObservableCollection<AgentMessage> AgentMessages { get; } = [];
    public ObservableCollection<AgentConfig> AvailableAgents { get; } = [];

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set
        {
            SetProperty(ref _selectedFilePath, value);
            LoadFileContent();
        }
    }
    public string DataFolder
    {
        get => _dataFolder;
        private set => SetProperty(ref _dataFolder, value);
    }

    public string SelectedFileContent
    {
        get => _selectedFileContent;
        set => SetProperty(ref _selectedFileContent, value);
    }

    public string UserMessage
    {
        get => _userMessage;
        set
        {
            if (SetProperty(ref _userMessage, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public MessageAttachment? PendingAttachment
    {
        get => _pendingAttachment;
        private set
        {
            SetProperty(ref _pendingAttachment, value);
            OnPropertyChanged(nameof(HasPendingAttachment));
        }
    }

    public bool HasPendingAttachment => PendingAttachment is not null;

    public bool IsAgentBusy
    {
        get => _isAgentBusy;
        private set
        {
            if (SetProperty(ref _isAgentBusy, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsWaitingForInteractiveAnswer
    {
        get => _isWaitingForInteractiveAnswer;
        private set
        {
            if (SetProperty(ref _isWaitingForInteractiveAnswer, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public string InteractivePrompt
    {
        get => _interactivePrompt;
        private set => SetProperty(ref _interactivePrompt, value);
    }

    public AgentConfig? SelectedAgent
    {
        get => _selectedAgent;
        set => SetProperty(ref _selectedAgent, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand SelectFolderCommand { get; }
    public ICommand SendMessageCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand AttachFileCommand { get; }
    public ICommand RemoveAttachmentCommand { get; }

    public ICommand ClearHistoryCommand { get; }
    public ICommand SaveFileCommand { get; }

    public MainViewModel()
    {
        // try to obtain app-wide notification service (optional)
        _notificationService = App.Services?.GetService(typeof(INotificationService)) as INotificationService;

        _toolRegistry = new ToolRegistry(_fileService, _arduinoCliService, _notificationService)
        {
            InteractiveAskHandler = AskUserAsync
        };
        if (_notificationService is not null)
            _notificationService.NotificationRaised += (s, ev) => System.Windows.Application.Current.Dispatcher.Invoke(() => StatusMessage = ev.Message);
        _toolRegistry.StatusChanged += OnToolRegistryStatusChanged;
        _providerFactory = new LLMProviderFactory(_toolRegistry, _notificationService);

        SelectFolderCommand = new RelayCommand(SelectFolder);
        SendMessageCommand = new RelayCommand(
            () => _ = SendMessage(),
            () => !string.IsNullOrWhiteSpace(UserMessage) && (!IsAgentBusy || IsWaitingForInteractiveAnswer));
        CancelCommand = new RelayCommand(CancelAgent, () => IsAgentBusy);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        ClearHistoryCommand = new RelayCommand(ClearHistory);
        AttachFileCommand = new RelayCommand(AttachFile, () => !IsAgentBusy || IsWaitingForInteractiveAnswer);
        RemoveAttachmentCommand = new RelayCommand(() => PendingAttachment = null, () => HasPendingAttachment);
        SaveFileCommand = new RelayCommand(SaveFile, () => !string.IsNullOrWhiteSpace(SelectedFilePath));
        LoadAgents();
        _dataFolder = _fileService.CurrentFolder;
    }

    // DI constructor
    public MainViewModel(ConfigService configService, FileService fileService, IArduinoCliService arduinoCliService, ToolRegistry toolRegistry, LLMProviderFactory providerFactory, INotificationService? notificationService = null)
    {
        _configService = configService;
        _fileService = fileService;
        _arduinoCliService = arduinoCliService;
        _toolRegistry = toolRegistry;
        _providerFactory = providerFactory;
        _notificationService = notificationService;

        _toolRegistry.InteractiveAskHandler = AskUserAsync;
        _toolRegistry.StatusChanged += OnToolRegistryStatusChanged;

        if (_notificationService is not null)
            _notificationService.NotificationRaised += (s, ev) => System.Windows.Application.Current.Dispatcher.Invoke(() => StatusMessage = ev.Message);

        SelectFolderCommand = new RelayCommand(SelectFolder);
        SendMessageCommand = new RelayCommand(
            () => _ = SendMessage(),
            () => !string.IsNullOrWhiteSpace(UserMessage) && (!IsAgentBusy || IsWaitingForInteractiveAnswer));
        CancelCommand = new RelayCommand(CancelAgent, () => IsAgentBusy);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        ClearHistoryCommand = new RelayCommand(ClearHistory);
        AttachFileCommand = new RelayCommand(AttachFile, () => !IsAgentBusy || IsWaitingForInteractiveAnswer);
        RemoveAttachmentCommand = new RelayCommand(() => PendingAttachment = null, () => HasPendingAttachment);
        SaveFileCommand = new RelayCommand(SaveFile, () => !string.IsNullOrWhiteSpace(SelectedFilePath));

        LoadAgents();
        _dataFolder = _fileService.CurrentFolder;
        if (string.IsNullOrEmpty(_dataFolder))
        {
            _dataFolder = configService.CurrentConfig.LastOpenedFolder;
            _fileService.SetCurrentFolder(_dataFolder);
        }
        if (!string.IsNullOrEmpty(_dataFolder))
        {
            foreach (var file in _fileService.GetCodeFiles())
                OpenFiles.Add(file);
            SelectedFilePath = OpenFiles.FirstOrDefault() ?? "";
        }

    }

    private void OnToolRegistryStatusChanged(string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => StatusMessage = message);
    }

    private void LoadAgents()
    {
        var previouslySelectedId = SelectedAgent?.Id;

        _configService.LoadConfig();
        AvailableAgents.Clear();
        foreach (var agent in _configService.CurrentConfig.Agents)
            AvailableAgents.Add(agent);

        SelectedAgent = (previouslySelectedId is not null
            ? AvailableAgents.FirstOrDefault(a => a.Id == previouslySelectedId)
            : null) ?? AvailableAgents.FirstOrDefault();
    }

    private void SelectFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        _fileService.SetCurrentFolder(dialog.SelectedPath);
        _configService.SetLastOpenedFolder(dialog.SelectedPath);
        OpenFiles.Clear();
        foreach (var file in _fileService.GetCodeFiles())
            OpenFiles.Add(file);
        SelectedFilePath = OpenFiles.FirstOrDefault() ?? "";
        DataFolder = _fileService.CurrentFolder;
    }

    private void AttachFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Izaberite fajl za prilaganje",
            Filter = "Svi fajlovi (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            PendingAttachment = AttachmentService.CreateFromFile(dialog.FileName);
        }
        catch (Exception ex)
        {
            AddMessage($"Greška pri prilaganju fajla: {ex.Message}", "assistant");
        }
    }

    private void LoadFileContent()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
            return;

        try
        {
            SelectedFileContent = _fileService.ReadFile(SelectedFilePath);
            if (SelectedFilePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                IsSyntaxHighlightingEnabled = false;
            else
                IsSyntaxHighlightingEnabled = true;
        }
        catch (Exception ex)
        {
            SelectedFileContent = $"Greška pri učitavanju fajla: {ex.Message}";
        }
    }

    private void SaveFile()
    {
        if (string.IsNullOrWhiteSpace(_fileService.CurrentFolder))
        {
            System.Windows.MessageBox.Show(
                "Workspace folder nije izabran. Odaberite folder pre snimanja fajla.",
                "Upozorenje",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            StatusMessage = "Workspace folder nije izabran.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedFilePath))
            return;

        try
        {
            _fileService.WriteFile(SelectedFilePath, SelectedFileContent);
            StatusMessage = $"Fajl snimljen: {System.IO.Path.GetFileName(SelectedFilePath)}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Greška pri snimanju fajla: {ex.Message}",
                "Greška",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            StatusMessage = $"Greška pri snimanju fajla: {ex.Message}";
        }
    }

    private async Task SendMessage()
    {
        var text = UserMessage.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Dok interactive.ask čeka odgovor, isti textbox završava postojeći tool poziv.
        if (_pendingInteractiveAsk is not null && _pendingInteractiveRequest is not null)
        {
            AddMessage(text, "user");
            UserMessage = "";

            var response = CreateInteractiveResponse(_pendingInteractiveRequest, text);
            _pendingInteractiveAsk.TrySetResult(response);
            return;
        }

        if (SelectedAgent is null)
            return;

        var llm = _configService.CurrentConfig.LLMs.FirstOrDefault(x => x.Id == SelectedAgent.LlmId);
        if (llm is null)
        {
            AddMessage("Agent nema izabran važeći LLM.", "assistant");
            return;
        }

        var provider = _providerFactory.GetProvider(llm.Processor);
        if (provider is null)
        {
            AddMessage("Procesor nije podržan.", "assistant");
            return;
        }

        IsAgentBusy = true;
        _agentCancellation = new CancellationTokenSource();
        var attachment = PendingAttachment;
        try
        {
            var history = AgentMessages.ToList();
            AddMessage(text, "user", attachment);
            UserMessage = "";
            PendingAttachment = null;
            StatusMessage = "Šaljem poruku agentu...";

            var response = await provider.SendMessageAsync(
                llm,
                SelectedAgent,
                history,
                text,
                attachment,
                _agentCancellation.Token);

            if (response.Success)
            {
                AddMessage(response.Content, "assistant");
                StatusMessage = "Agent je završio odgovor.";
                var tokenInfo = response.TokenInfo ?? new TokenInfo
                {
                    Model = llm.Model,
                    InputTokens = response.InputTokens,
                    OutputTokens = response.OutputTokens,
                    CachedInputTokens = response.CacheTokens
                };
                _configService.RegisterUsage(llm, tokenInfo);

                OpenFiles.Clear();
                foreach (var file in _fileService.GetCodeFiles())
                    OpenFiles.Add(file);
                SelectedFilePath = OpenFiles.FirstOrDefault() ?? "";
            }
            else
            {
                AddMessage($"Greška: {response.Error}", "assistant");
                StatusMessage = $"Greška: {response.Error}";
            }
        }
        catch (OperationCanceledException)
        {
            AddMessage("Izvršavanje agenta je otkazano.", "assistant");
            StatusMessage = "Izvršavanje agenta je otkazano.";
        }
        catch (Exception ex)
        {
            AddMessage($"Greška: {ex.Message}", "assistant");
            StatusMessage = $"Greška: {ex.Message}";
        }
        finally
        {
            CancelPendingInteractiveAsk();
            _agentCancellation?.Dispose();
            _agentCancellation = null;
            IsAgentBusy = false;
        }
    }

    private async Task<InteractiveAskResponse> AskUserAsync(
        InteractiveAskRequest request,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return await Task.FromCanceled<InteractiveAskResponse>(cancellationToken);

        var completion = new TaskCompletionSource<InteractiveAskResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_pendingInteractiveAsk is not null)
                throw new InvalidOperationException("Već postoji interactive.ask koji čeka odgovor korisnika.");

            _pendingInteractiveRequest = request;
            _pendingInteractiveAsk = completion;
            InteractivePrompt = request.RequiresApproval
                ? "Agent čeka dozvolu. Odgovorite u polju ispod (npr. Da/Ne)."
                : "Agent čeka vaš odgovor u polju ispod.";
            IsWaitingForInteractiveAnswer = true;
            AddMessage(BuildInteractiveQuestion(request), "assistant");
        });

        using var registration = cancellationToken.Register(() =>
            completion.TrySetCanceled(cancellationToken));

        try
        {
            return await completion.Task;
        }
        finally
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (ReferenceEquals(_pendingInteractiveAsk, completion))
                {
                    _pendingInteractiveAsk = null;
                    _pendingInteractiveRequest = null;
                    InteractivePrompt = "";
                    IsWaitingForInteractiveAnswer = false;
                }
            });
        }
    }

    private static string BuildInteractiveQuestion(InteractiveAskRequest request)
    {
        var text = new StringBuilder();
        text.AppendLine(request.RequiresApproval
            ? $"Potrebna je vaša dozvola: {request.Question}"
            : request.Question);

        if (!string.IsNullOrWhiteSpace(request.Details))
        {
            text.AppendLine();
            text.AppendLine(request.Details);
        }

        if (request.Options.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Ponuđeni odgovori:");
            foreach (var option in request.Options)
                text.AppendLine($"• {option}");
        }

        if (request.RequiresApproval)
        {
            text.AppendLine();
            text.Append("Odgovorite sa „Da/Dozvoli“ ili „Ne/Odbij“.");
        }
        else if (request.AllowFreeText)
        {
            text.AppendLine();
            text.Append("Unesite odgovor u postojeće polje za poruku.");
        }

        return text.ToString().TrimEnd();
    }

    private static InteractiveAskResponse CreateInteractiveResponse(
        InteractiveAskRequest request,
        string answer)
    {
        if (!request.RequiresApproval)
        {
            return new InteractiveAskResponse
            {
                Approved = true,
                Answer = answer,
                Cancelled = false
            };
        }

        bool approved = ParseApproval(answer);
        return new InteractiveAskResponse
        {
            Approved = approved,
            Answer = answer,
            Cancelled = false
        };
    }

    private static bool ParseApproval(string answer)
    {
        var normalized = answer.Trim().ToLowerInvariant();
        return normalized is "da" or "dozvoli" or "odobri" or "odobreno" or "yes" or "y" or "ok";
    }

    private void CancelAgent()
    {
        if (_pendingInteractiveAsk is not null)
        {
            _pendingInteractiveAsk.TrySetResult(new InteractiveAskResponse
            {
                Approved = false,
                Answer = "Korisnik je otkazao interaktivni zahtev.",
                Cancelled = true
            });
        }

        _agentCancellation?.Cancel();
    }

    private void CancelPendingInteractiveAsk()
    {
        _pendingInteractiveAsk?.TrySetCanceled();
        _pendingInteractiveAsk = null;
        _pendingInteractiveRequest = null;
        InteractivePrompt = "";
        IsWaitingForInteractiveAnswer = false;
    }

    private void ClearHistory()
    {
        AgentMessages.Clear();
    }
    private void OpenSettings()
    {
        var window = new Views.SettingsWindow(_configService, _arduinoCliService)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
        LoadAgents();
    }

    private void AddMessage(string content, string role, MessageAttachment? attachment = null)
    {
        var displayContent = content;
        if (attachment is not null)
        {
            displayContent = attachment.IsImage
                ? $"{content}\n\n[Slika: {attachment.FileName}]"
                : $"{content}\n\n[Prilog: {attachment.FileName}]";
        }

        AgentMessages.Add(new AgentMessage
        {
            Content = displayContent,
            Role = role,
            Timestamp = DateTime.Now,
            Attachments = attachment is null ? [] : [attachment]
        });
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}
