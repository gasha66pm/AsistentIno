using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using AsistentIno.Models;
using AsistentIno.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AsistentIno.Views;

public partial class CompileWindow : Window
{
    private readonly IArduinoCliService _arduinoCli;
    private readonly FileService? _fileService;
    private readonly INotificationService _notificationService;
    // FileNameCombo is defined in XAML (ComboBox)

    public string SelectedFqbn => (BoardsCombo.SelectedItem as BoardProfile)?.Fqbn ?? string.Empty;
    public string FileName
    {
        get
        {
            if (FileNameCombo is not null)
                return (FileNameCombo.SelectedItem as FileAttr)?.FullPath ?? string.Empty;

            return string.Empty;
        }
    }
    public string Action => (ActionCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Compile";
    public string SelectedConnectedBoard => (ConnectedBoardsCombo.SelectedItem as string) ?? string.Empty;
    public string SelectedPort
    {
        get
        {
            // Get selected item or text
            var raw = (ConnectedBoardsCombo.SelectedItem as string) ?? ConnectedBoardsCombo.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            // common arduino-cli 'board list' lines start with port (e.g. COM3 or /dev/ttyUSB0)
            var parts = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : raw;
        }
    }

    public CompileWindow()
    {
        InitializeComponent();

        // resolve services from App DI container
        _arduinoCli = App.Services.GetRequiredService<IArduinoCliService>();
        _fileService = App.Services.GetService(typeof(FileService)) as FileService;
        _notificationService = App.Services.GetRequiredService<INotificationService>();

        Loaded += CompileWindow_Loaded;
    }

    private async void CompileWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        await LoadBoardsAsync();
        await LoadConnectedBoardsAsync();
        PopulateFileNameCombo();
    }

    private async Task LoadBoardsAsync()
    {
        try
        {
            StatusText.Text = "Učitavanje dostupnih board-ova...";
            var output = await _arduinoCli.GetBoardProfilesAsync();

            // naive split so that user can pick or edit fqbn; keep lines

                BoardsCombo.ItemsSource = output;
                BoardsCombo.DisplayMemberPath = "Name";

            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Ne mogu da učitam board-ove: " + ex.Message;
        }
    }

    private async Task LoadConnectedBoardsAsync()
    {
        try
        {
            var list = await _arduinoCli.ListBoardsAsync();
            ConnectedBoardsCombo.ItemsSource = list;
        }
        catch
        {
            // ignore
        }
    }



    private void PopulateFileNameCombo()
    {
        try
        {
            if (FileNameCombo is null)
                return;

            if (_fileService is null)
                return;

            var files = _fileService.GetCodeFiles()
                .Where(fn => string.Equals(System.IO.Path.GetExtension(fn), ".ino", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (files.Any())
            {
                var fileList = new List<FileAttr>();
                foreach (var file in files)
                {
                    fileList.Add(new FileAttr(Path.GetFileName(file), file));
                }
                FileNameCombo.ItemsSource = fileList;
                FileNameCombo.DisplayMemberPath = "FileName";
                FileNameCombo.SelectedItem = fileList.First();

            }


        }
        catch
        {
            // ignore
        }
    }

    private void ActionCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var action = (ActionCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString();
        ConnectedBoardsCombo.IsEnabled = string.Equals(action, "Upload", StringComparison.OrdinalIgnoreCase);
    }

    private async void Ok_Click(object sender, RoutedEventArgs e)
    {
        // if Compile chosen, try to run compile (best-effort)
        if (string.Equals(Action, "Compile", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                StatusText.Text = "Pokrećem kompilaciju...";
                await _arduinoCli.CompileAsync(FileName, SelectedFqbn);
                _notificationService.Notify("Kompajliranje završeno.");
            }
            catch (Exception ex)
            {
                _notificationService.Notify($"Greška pri kompajliranju: {ex.Message}");
                return;
            }
        }
        else
        {
            // if Upload chosen, try to run upload (best-effort)
            try
            {
                _notificationService.Notify("Pokrećem upload...");
                await _arduinoCli.UploadAsync(FileName, SelectedPort, SelectedFqbn, CancellationToken.None);
                _notificationService.Notify("Upload završen.");
            }
            catch (Exception ex)
            {
                _notificationService.Notify($"Greška pri upload-u: {ex.Message}");
                return;
            }
        }

        // Upload is not implemented here; caller can use SelectedConnectedBoard
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    internal record FileAttr(string FileName, string FullPath);
}
