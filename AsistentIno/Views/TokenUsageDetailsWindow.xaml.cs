using System.Windows;
using AsistentIno.Models;
using AsistentIno.Services;

namespace AsistentIno.Views;

public partial class TokenUsageDetailsWindow : Window
{
    private readonly ConfigService _configService;
    private readonly LLMConfig _llm;

    public TokenUsageDetailsWindow(ConfigService configService, LLMConfig llm)
    {
        InitializeComponent();
        _configService = configService;
        _llm = llm;

        LlmNameTextBlock.Text = llm.Name;
        RefreshValues();
    }

    private void RefreshValues()
    {
        var usage = _configService.GetUsage(_llm.Id);
        InputTokensText.Text = usage.TotalInputTokens.ToString();
        OutputTokensText.Text = usage.TotalOutputTokens.ToString();
        CachedInputTokensText.Text = usage.TotalCachedInputTokens.ToString();
        CacheCreationTokensText.Text = usage.TotalCacheCreationTokens.ToString();
        ReasoningTokensText.Text = usage.TotalReasoningTokens.ToString();
        ToolUseTokensText.Text = usage.TotalToolUseTokens.ToString();
        TotalTokensText.Text = usage.TotalTokens.ToString();
        TotalCostText.Text = usage.TotalCost.ToString("0.000000");
        CallCountText.Text = usage.CallCount.ToString();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _configService.ResetUsage(_llm.Id);
        RefreshValues();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
