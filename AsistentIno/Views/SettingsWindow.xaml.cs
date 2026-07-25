using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AsistentIno.Models;
using AsistentIno.Services;

namespace AsistentIno.Views;

public class LlmUsageRow
{
    public required LLMConfig Llm { get; init; }
    public required LlmTokenUsage Usage { get; init; }
    public string Name => Llm.Name;
}

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService = new();
    private AgentConfig? _currentAgent;
    private LLMConfig? _currentLlm;
    private ArduinoCliService _arduinoService;

    public SettingsWindow()
    {
        InitializeComponent();
        ReasoningCombo.ItemsSource = Enum.GetValues<ReasoningEffort>();
        ProcessorCombo.ItemsSource = Enum.GetValues<ProcessorType>();
        RefreshLists();
        DataFolderTextBox.Text = _configService.DataFolder;
        ArduinoCLIPathTextBox.Text = _configService.CurrentConfig.ArduinoCliPath;
        _arduinoService = App.Services.GetService(typeof(ArduinoCliService)) as ArduinoCliService;
        
    }

    private void RefreshLists()
    {
        AgentListBox.ItemsSource = null; AgentListBox.ItemsSource = _configService.CurrentConfig.Agents;
        LLMListBox.ItemsSource = null; LLMListBox.ItemsSource = _configService.CurrentConfig.LLMs;
        AgentLlmCombo.ItemsSource = null; AgentLlmCombo.ItemsSource = _configService.CurrentConfig.LLMs;
        UsageDataGrid.ItemsSource = null;
        UsageDataGrid.ItemsSource = _configService.CurrentConfig.LLMs
            .Select(llm => new LlmUsageRow { Llm = llm, Usage = _configService.GetUsage(llm.Id) })
            .ToList();
    }

    private void AgentListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentAgent = AgentListBox.SelectedItem as AgentConfig;
        if (_currentAgent is null) return;
        AgentNameTextBox.Text = _currentAgent.Name;
        SystemPromptTextBox.Text = _currentAgent.SystemPrompt;
        AgentLlmCombo.SelectedValue = _currentAgent.LlmId;
        ReasoningCombo.SelectedItem = _currentAgent.ReasoningEffort;
        foreach (var item in ToolsListBox.Items.OfType<System.Windows.Controls.CheckBox>())
            item.IsChecked = _currentAgent.EnabledTools.Contains(item.Content?.ToString() ?? "");
    }

    private void AddAgentButton_Click(object sender, RoutedEventArgs e)
    {
        var item = new AgentConfig { LlmId = _configService.CurrentConfig.LLMs.FirstOrDefault()?.Id ?? "" };
        _configService.AddAgent(item); RefreshLists(); AgentListBox.SelectedItem = item;
    }

    private void DeleteAgentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentAgent is null) return;
        _configService.RemoveAgent(_currentAgent.Id); _currentAgent = null; RefreshLists();
    }

    private void SaveAgentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentAgent is null) return;
        _currentAgent.Name = AgentNameTextBox.Text.Trim();
        _currentAgent.SystemPrompt = SystemPromptTextBox.Text;
        _currentAgent.LlmId = AgentLlmCombo.SelectedValue?.ToString() ?? "";
        _currentAgent.ReasoningEffort = ReasoningCombo.SelectedItem is ReasoningEffort effort ? effort : ReasoningEffort.None;
        _currentAgent.EnabledTools = ToolsListBox.Items.OfType<System.Windows.Controls.CheckBox>()
            .Where(x => x.IsChecked == true).Select(x => x.Content?.ToString() ?? "").Where(x => x.Length > 0).ToList();
        _configService.UpdateAgent(_currentAgent); RefreshLists(); AgentListBox.SelectedItem = _currentAgent;
    }

    private void LLMListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentLlm = LLMListBox.SelectedItem as LLMConfig;
        if (_currentLlm is null) return;
        LLMNameTextBox.Text = _currentLlm.Name;
        ModelTextBox.Text = _currentLlm.Model;
        EndpointTextBox.Text = _currentLlm.Endpoint;
        ApiKeyPasswordBox.Password = _currentLlm.ApiKey;
        ProcessorCombo.SelectedItem = _currentLlm.Processor;
    }

    private void AddLLMButton_Click(object sender, RoutedEventArgs e)
    {
        var item = new LLMConfig(); _configService.AddLLM(item); RefreshLists(); LLMListBox.SelectedItem = item;
    }

    private void DeleteLLMButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentLlm is null) return;
        if (!_configService.RemoveLLM(_currentLlm.Id))
        {
            System.Windows.MessageBox.Show("LLM koristi jedan ili više agenata.", "Brisanje nije moguće", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _currentLlm = null; RefreshLists();
    }

    private void SaveLLMButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentLlm is null) return;
        _currentLlm.Name = LLMNameTextBox.Text.Trim();
        _currentLlm.Model = ModelTextBox.Text.Trim();
        _currentLlm.Endpoint = EndpointTextBox.Text.Trim();
        _currentLlm.ApiKey = ApiKeyPasswordBox.Password;
        _currentLlm.Processor = ProcessorCombo.SelectedItem is ProcessorType processor ? processor : ProcessorType.OpenAI;
        _configService.UpdateLLM(_currentLlm); RefreshLists(); LLMListBox.SelectedItem = _currentLlm;
    }

    private void EditPricingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentLlm is null) return;
        var window = new LlmPricingWindow(_configService, _currentLlm) { Owner = this };
        window.ShowDialog();
    }

    private void RefreshUsageButton_Click(object sender, RoutedEventArgs e) => RefreshLists();

    private void DetailsUsageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: LlmUsageRow row }) return;
        var window = new TokenUsageDetailsWindow(_configService, row.Llm) { Owner = this };
        window.ShowDialog();
        RefreshLists();
    }

    private void BrowseDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = Directory.Exists(DataFolderTextBox.Text) ? DataFolderTextBox.Text : _configService.DataFolder
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        DataFolderTextBox.Text = dialog.SelectedPath;
    }

    private void SaveDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = DataFolderTextBox.Text.Trim();
        _configService.SetDataFolder(folder);
        DataFolderTextBox.Text = _configService.DataFolder;
        RefreshLists();
        System.Windows.MessageBox.Show("Folder za podatke je sačuvan.", "Podešavanja", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = DataFolderTextBox.Text.Trim();
        if (Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }

    private void BrowseArduinoCLIButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog();
        {
            dialog.Filter = "Executable Files (*.exe)|*.exe";
        }
        ;
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        ArduinoCLIPathTextBox.Text = dialog.FileName;
    }

    private void SaveArduinoCLIPathButton_Click(object sender, RoutedEventArgs e)
    {
        var path = ArduinoCLIPathTextBox.Text.Trim();
        _configService.SetArduinoCliPath(path);
        ArduinoCLIPathTextBox.Text = _configService.CurrentConfig.ArduinoCliPath;
        RefreshLists();
        System.Windows.MessageBox.Show("ArduinoCLI putanja je sačuvana. Restartujte aplikaciju za primenu promena.", "Podešavanja", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void TestArduinoCLIPathButton_Click(object sender, RoutedEventArgs e)
    {
        ArduinoStatusTextBox.Clear();
        if (_arduinoService is null)
        {
            ArduinoStatusTextBox.Text = "ArduinoCLI servis nije dostupan.";
            return;
        }
        var result = await _arduinoService.GetVersionAsync();
        ArduinoStatusTextBox.Text = result;
 
    }
}
