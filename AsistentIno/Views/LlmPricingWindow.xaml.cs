using System.Globalization;
using System.Windows;
using AsistentIno.Models;
using AsistentIno.Services;

namespace AsistentIno.Views;

public partial class LlmPricingWindow : Window
{
    private readonly ConfigService _configService;
    private readonly LLMConfig _llm;

    public LlmPricingWindow(ConfigService configService, LLMConfig llm)
    {
        InitializeComponent();
        _configService = configService;
        _llm = llm;

        LlmNameTextBlock.Text = $"Cenovnik za: {llm.Name}";
        var pricing = _configService.GetPricing(llm);
        ModelTextBox.Text = pricing.Model;
        InputPriceTextBox.Text = pricing.InputPricePerMillion.ToString(CultureInfo.InvariantCulture);
        OutputPriceTextBox.Text = pricing.OutputPricePerMillion.ToString(CultureInfo.InvariantCulture);
        CachedInputPriceTextBox.Text = pricing.CachedInputPricePerMillion?.ToString(CultureInfo.InvariantCulture) ?? "";
        CacheCreationPriceTextBox.Text = pricing.CacheCreationPricePerMillion?.ToString(CultureInfo.InvariantCulture) ?? "";
        ReasoningBilledAsOutputCheckBox.IsChecked = pricing.ReasoningBilledAsOutput;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var pricing = new ModelPricing
        {
            Model = string.IsNullOrWhiteSpace(ModelTextBox.Text) ? _llm.Model : ModelTextBox.Text.Trim(),
            InputPricePerMillion = ParseDecimal(InputPriceTextBox.Text),
            OutputPricePerMillion = ParseDecimal(OutputPriceTextBox.Text),
            CachedInputPricePerMillion = ParseNullableDecimal(CachedInputPriceTextBox.Text),
            CacheCreationPricePerMillion = ParseNullableDecimal(CacheCreationPriceTextBox.Text),
            ReasoningBilledAsOutput = ReasoningBilledAsOutputCheckBox.IsChecked == true
        };

        _configService.SavePricing(_llm.Id, pricing);
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private static decimal ParseDecimal(string text) =>
        decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    private static decimal? ParseNullableDecimal(string text) =>
        string.IsNullOrWhiteSpace(text) ? null : ParseDecimal(text);
}
