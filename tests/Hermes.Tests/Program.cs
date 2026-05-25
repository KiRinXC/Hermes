using Hermes.Windows.Infrastructure;
using Hermes.Windows.Input;
using Hermes.Windows.Selection;
using Hermes.Windows.Settings;
using Hermes.Windows.Translation;

var tests = new List<(string Name, Action Test)>
{
    ("settings defaults", SettingsDefaults),
    ("redacts API key", RedactsApiKey),
    ("validates English text", ValidatesEnglishText),
    ("rejects non-English text", RejectsNonEnglishText),
    ("builds responses URI", BuildsResponsesUri),
    ("extracts output_text", ExtractsOutputText),
    ("extracts output array text", ExtractsOutputArrayText),
    ("accepts high confidence selection candidate", AcceptsHighConfidenceCandidate),
    ("rejects low distance selection candidate", RejectsLowDistanceCandidate),
    ("rejects no ctrl selection candidate", RejectsNoCtrlSelectionCandidate),
    ("rejects released ctrl selection candidate", RejectsReleasedCtrlSelectionCandidate),
    ("rejects excluded app selection candidate", RejectsExcludedAppSelectionCandidate),
    ("rejects disabled automatic selection", RejectsDisabledAutomaticSelection),
    ("rejects expired selection candidate", RejectsExpiredCandidate),
    ("uses candidate pre-read text", UsesCandidatePreReadText),
    ("rejects stale candidate pre-read text", RejectsStaleCandidatePreReadText),
    ("rejects empty candidate pre-read text", RejectsEmptyCandidatePreReadText),
    ("validates hotkey gesture", ValidatesHotkeyGesture),
    ("rejects bare hotkey gesture", RejectsBareHotkeyGesture)
};

var failed = 0;
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"[PASS] {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"[FAIL] {name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Environment.ExitCode = 1;
}

static void SettingsDefaults()
{
    var settings = new AppSettings();
    AssertEqual("https://api.openai.com/v1", settings.Api.BaseUrl);
    AssertEqual("gpt-4.1-mini", settings.Api.Model);
    AssertEqual("Ctrl+Alt+E", settings.Triggers.Hotkey);
    AssertFalse(settings.Privacy.SaveHistory);
}

static void RedactsApiKey()
{
    var redacted = Redactor.RedactSecrets("api_key=sk-test-secret");
    AssertFalse(redacted.Contains("sk-test-secret", StringComparison.Ordinal));
}

static void ValidatesEnglishText()
{
    var result = SelectionTextValidator.Validate("Hello world", new AppSettings());
    AssertTrue(result.IsValid);
}

static void RejectsNonEnglishText()
{
    var result = SelectionTextValidator.Validate("你好世界", new AppSettings());
    AssertFalse(result.IsValid);
}

static void BuildsResponsesUri()
{
    var uri = OpenAiTranslationService.BuildResponsesUri("https://api.openai.com/v1");
    AssertEqual("https://api.openai.com/v1/responses", uri.ToString());
}

static void ExtractsOutputText()
{
    var text = OpenAiTranslationService.ExtractOutputText("""{"output_text":"你好"}""");
    AssertEqual("你好", text);
}

static void ExtractsOutputArrayText()
{
    var text = OpenAiTranslationService.ExtractOutputText(
        """{"output":[{"content":[{"type":"output_text","text":"你好"}]}]}""");
    AssertEqual("你好", text);
}

static void AcceptsHighConfidenceCandidate()
{
    var now = DateTimeOffset.Now;
    var decision = SelectionCandidateService.EvaluateGesture(
        new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, true, true, true),
        new AppSettings(),
        isExcluded: false,
        isSensitive: false);
    AssertTrue(decision.ShouldShow);
}

static void RejectsLowDistanceCandidate()
{
    var now = DateTimeOffset.Now;
    var decision = SelectionCandidateService.EvaluateGesture(
        new SelectionCandidateInput(10, 10, 14, 12, now, now.AddMilliseconds(320), null, true, true, true),
        new AppSettings(),
        isExcluded: false,
        isSensitive: false);
    AssertFalse(decision.ShouldShow);
}

static void RejectsNoCtrlSelectionCandidate()
{
    var now = DateTimeOffset.Now;
    var decision = SelectionCandidateService.EvaluateGesture(
        new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, false, false, false),
        new AppSettings(),
        isExcluded: false,
        isSensitive: false);
    AssertFalse(decision.ShouldShow);
    AssertEqual("ctrl-not-held", decision.Reason);
}

static void RejectsReleasedCtrlSelectionCandidate()
{
    var now = DateTimeOffset.Now;
    var decision = SelectionCandidateService.EvaluateGesture(
        new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, true, false, false),
        new AppSettings(),
        isExcluded: false,
        isSensitive: false);
    AssertFalse(decision.ShouldShow);
    AssertEqual("ctrl-not-held", decision.Reason);
}

static void RejectsDisabledAutomaticSelection()
{
    var settings = new AppSettings();
    settings.Triggers.AutoShowSelectionButton = false;
    var now = DateTimeOffset.Now;
    var decision = SelectionCandidateService.EvaluateGesture(
        new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, true, true, true),
        settings,
        isExcluded: false,
        isSensitive: false);
    AssertFalse(decision.ShouldShow);
}

static void RejectsExcludedAppSelectionCandidate()
{
    var now = DateTimeOffset.Now;
    var foreground = new ForegroundWindowInfo(IntPtr.Zero, 123, "blocked", "Blocked app");
    var decision = SelectionCandidateService.EvaluateGesture(
        new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), foreground, true, true, true),
        new AppSettings(),
        isExcluded: true,
        isSensitive: false);
    AssertFalse(decision.ShouldShow);
    AssertEqual("foreground-app-excluded", decision.Reason);
}

static void RejectsExpiredCandidate()
{
    var candidate = new SelectionCandidate(
        Guid.NewGuid(),
        DateTimeOffset.Now.Subtract(TimeSpan.FromSeconds(10)),
        20,
        20,
        120,
        TimeSpan.FromMilliseconds(250),
        null,
        null,
        null,
        0.8);
    AssertTrue(SelectionCandidateService.IsExpired(candidate));
}

static void UsesCandidatePreReadText()
{
    var candidate = CreateCandidate("Hello world");
    var created = SelectionCandidateService.TryCreatePreReadSelection(
        candidate,
        new AppSettings(),
        out var selection,
        out var validation);
    AssertTrue(created);
    AssertTrue(validation.IsValid);
    AssertEqual("Hello world", selection.Text);
    AssertEqual(SelectionProviderKind.UiAutomation, selection.Provider);
}

static void RejectsStaleCandidatePreReadText()
{
    var candidate = CreateCandidate("Hello world") with
    {
        CreatedAt = DateTimeOffset.Now.Subtract(TimeSpan.FromSeconds(10))
    };
    var created = SelectionCandidateService.TryCreatePreReadSelection(
        candidate,
        new AppSettings(),
        out _,
        out _);
    AssertFalse(created);
}

static void RejectsEmptyCandidatePreReadText()
{
    var candidate = CreateCandidate(null);
    var created = SelectionCandidateService.TryCreatePreReadSelection(
        candidate,
        new AppSettings(),
        out _,
        out _);
    AssertFalse(created);
}

static SelectionCandidate CreateCandidate(string? preReadText)
{
    return new SelectionCandidate(
        Guid.NewGuid(),
        DateTimeOffset.Now,
        20,
        20,
        120,
        TimeSpan.FromMilliseconds(250),
        null,
        preReadText,
        null,
        0.8);
}

static void ValidatesHotkeyGesture()
{
    AssertTrue(HotkeyGesture.TryParse("Ctrl+Alt+E", out var gesture));
    AssertEqual("Ctrl+Alt+E", gesture.ToString());
}

static void RejectsBareHotkeyGesture()
{
    AssertFalse(HotkeyGesture.TryParse("E", out _));
}

static void AssertTrue(bool value)
{
    if (!value)
    {
        throw new InvalidOperationException("Expected true.");
    }
}

static void AssertFalse(bool value)
{
    if (value)
    {
        throw new InvalidOperationException("Expected false.");
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}
