using Hermes.Windows.Translation;
using System.IO;
using Hermes.Windows.Settings;

namespace Hermes.Tests.Translation;

public static class TranslationCoordinatorTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("translation coordinator ignores passive mouse drags without ctrl", IgnoresPassiveMouseDragsWithoutCtrl);
        suite.Add("translation coordinator retries per popup context", RetriesPerPopupContext);
        suite.Add("translation coordinator applies stream updates to the current popup", AppliesStreamUpdatesToCurrentPopup);
        suite.Add("translation coordinator candidate flow does not cancel its own request token", CandidateFlowDoesNotCancelItsOwnRequestToken);
        suite.Add("translation coordinator uses short passive settle delay", UsesShortPassiveSettleDelay);
        suite.Add("translation coordinator loading state shows active channel", LoadingStateShowsActiveChannel);
        suite.Add("translation coordinator cancels stale passive button evaluation", CancelsStalePassiveButtonEvaluation);
    }

    private static void IgnoresPassiveMouseDragsWithoutCtrl()
    {
        TestAssert.True(TranslationCoordinator.ShouldIgnorePassiveMouseGesture(
            ctrlDownAtStart: false,
            ctrlHeldDuringDrag: false,
            ctrlDownAtRelease: false));
        TestAssert.False(TranslationCoordinator.ShouldIgnorePassiveMouseGesture(
            ctrlDownAtStart: true,
            ctrlHeldDuringDrag: false,
            ctrlDownAtRelease: false));
        TestAssert.True(TranslationCoordinator.ShouldIgnorePassiveMouseGesture(
            ctrlDownAtStart: false,
            ctrlHeldDuringDrag: true,
            ctrlDownAtRelease: true));
    }

    private static void RetriesPerPopupContext()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Translation/TranslationCoordinator.cs"));
        TestAssert.Contains("_popupRequestContexts.TryGetValue(args.Popup, out var context)", code);
        TestAssert.Contains("TranslateSelectionAsync(context.Selection, context.Mode, args.Popup)", code);
    }

    private static void AppliesStreamUpdatesToCurrentPopup()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Translation/TranslationCoordinator.cs"));
        TestAssert.Contains("popup.AppendTranslationDelta", code);
        TestAssert.Contains("popup.CompleteStreamingTranslation", code);
        TestAssert.Contains("popup.SetError", code);
    }

    private static void CandidateFlowDoesNotCancelItsOwnRequestToken()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Translation/TranslationCoordinator.cs"));
        var start = code.IndexOf("private async Task TranslateCandidateAsync", StringComparison.Ordinal);
        var end = code.IndexOf("private async Task TranslateSelectionAsync", start, StringComparison.Ordinal);
        TestAssert.True(start >= 0);
        TestAssert.True(end > start);
        var candidateFlow = code[start..end];

        TestAssert.False(candidateFlow.Contains("_currentRequestCts = CancellationTokenSource.CreateLinkedTokenSource", StringComparison.Ordinal));
        TestAssert.False(candidateFlow.Contains("_currentRequestCts.Token", StringComparison.Ordinal));
        TestAssert.Contains("TranslateSelectionAsync(preReadSelection, preReadValidation, candidate.Mode, cancellationToken, popup)", candidateFlow);
        TestAssert.Contains("ReadForCandidateTriggerAsync", candidateFlow);
        TestAssert.Contains("candidate.ForegroundWindow", candidateFlow);
    }

    private static void UsesShortPassiveSettleDelay()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Translation/TranslationCoordinator.cs"));
        TestAssert.Contains("PassiveSelectionSettleDelay", code);
        TestAssert.False(code.Contains("Task.Delay(90", StringComparison.Ordinal));
    }

    private static void LoadingStateShowsActiveChannel()
    {
        var transmartSettings = new AppSettings();
        transmartSettings.Api.UseOpenAiForTranslation = false;
        transmartSettings.Api.OpenAi.Model = "gpt-4.1-mini";

        var transmartState = TranslationCoordinator.BuildLoadingStateTextForMode(transmartSettings, TranslationMode.Translate);
        TestAssert.Contains("(Tencent)", transmartState);

        var openAiSettings = new AppSettings();
        openAiSettings.Api.UseOpenAiForTranslation = true;
        openAiSettings.Api.OpenAi.Model = "gpt-4.1";

        var openAiState = TranslationCoordinator.BuildLoadingStateTextForMode(openAiSettings, TranslationMode.Translate);
        TestAssert.Contains("(gpt-4.1)", openAiState);

        var explainState = TranslationCoordinator.BuildLoadingStateTextForMode(transmartSettings, TranslationMode.Explain);
        TestAssert.Contains("(gpt-4.1-mini)", explainState);
    }

    private static void CancelsStalePassiveButtonEvaluation()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Translation/TranslationCoordinator.cs"));

        TestAssert.Contains("private CancellationTokenSource? _passiveButtonCts;", code);
        TestAssert.Contains("_passiveButtonCts?.Cancel();", code);
        TestAssert.Contains("ReferenceEquals(_passiveButtonCts, passiveButtonCts)", code);
        TestAssert.Contains("passiveButtonCts.Dispose();", code);
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file '{relativePath}'.");
    }
}
