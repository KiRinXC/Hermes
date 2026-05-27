using Hermes.Windows.Translation;
using System.Text;
using System.Text.Json;

namespace Hermes.Tests.Translation;

public static class OpenAiTranslationServiceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("builds responses URI", BuildsResponsesUri);
        suite.Add("extracts output_text", ExtractsOutputText);
        suite.Add("extracts output array text", ExtractsOutputArrayText);
        suite.Add("uses custom prompt instructions", UsesCustomPromptInstructions);
        suite.Add("builds streaming responses payload", BuildsStreamingResponsesPayload);
        suite.Add("parses streaming output text delta", ParsesStreamingOutputTextDelta);
        suite.Add("reconciles streaming done text", ReconcilesStreamingDoneText);
        suite.Add("maps streaming provider error event", MapsStreamingProviderErrorEvent);
        suite.Add("maps invalid streaming event", MapsInvalidStreamingEvent);
    }

    private static void BuildsResponsesUri()
    {
        var uri = OpenAiTranslationService.BuildResponsesUri("https://api.openai.com/v1");
        TestAssert.Equal("https://api.openai.com/v1/responses", uri.ToString());
    }

    private static void ExtractsOutputText()
    {
        var text = OpenAiTranslationService.ExtractOutputText("""{"output_text":"你好"}""");
        TestAssert.Equal("你好", text);
    }

    private static void ExtractsOutputArrayText()
    {
        var text = OpenAiTranslationService.ExtractOutputText(
            """{"output":[{"content":[{"type":"output_text","text":"你好"}]}]}""");
        TestAssert.Equal("你好", text);
    }

    private static void UsesCustomPromptInstructions()
    {
        var instructions = TranslationPromptBuilder.BuildInstructions(
            "literal",
            "Simplified Chinese",
            preserveFormatting: false,
            customPrompt: "请用短句翻译，并保留产品名。");

        TestAssert.Equal("请用短句翻译，并保留产品名。", instructions);
    }

    private static void BuildsStreamingResponsesPayload()
    {
        var payload = OpenAiTranslationService.CreatePayload(
            "gpt-test",
            new TranslationRequest("Hello", "natural", "Simplified Chinese", PreserveFormatting: true),
            stream: true);

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        TestAssert.Contains("\"model\":\"gpt-test\"", json);
        TestAssert.Contains("\"stream\":true", json);
    }

    private static void ParsesStreamingOutputTextDelta()
    {
        var accumulated = new StringBuilder();
        var streamEvent = OpenAiTranslationService.ParseStreamingEvent(
            "response.output_text.delta",
            """{"type":"response.output_text.delta","delta":"你"}""",
            accumulated);

        TestAssert.Equal(TranslationStreamEventKind.Delta, streamEvent!.Kind);
        TestAssert.Equal("你", streamEvent.DeltaText);
        TestAssert.Equal("你", streamEvent.CurrentText);
        TestAssert.Equal("你", accumulated.ToString());
    }

    private static void ReconcilesStreamingDoneText()
    {
        var accumulated = new StringBuilder("你");
        var doneEvent = OpenAiTranslationService.ParseStreamingEvent(
            "",
            """{"type":"response.output_text.done","text":"你好"}""",
            accumulated);
        var completedEvent = OpenAiTranslationService.ParseStreamingEvent(
            "response.completed",
            """{"type":"response.completed"}""",
            accumulated);

        TestAssert.Equal(null, doneEvent);
        TestAssert.Equal("你好", accumulated.ToString());
        TestAssert.Equal(TranslationStreamEventKind.Completed, completedEvent!.Kind);
        TestAssert.Equal("你好", completedEvent.CurrentText);
    }

    private static void MapsStreamingProviderErrorEvent()
    {
        var streamEvent = OpenAiTranslationService.ParseStreamingEvent(
            "error",
            """{"error":{"message":"bad request"}}""",
            new StringBuilder());

        TestAssert.Equal(TranslationStreamEventKind.Failed, streamEvent!.Kind);
        TestAssert.Equal(TranslationErrorKind.Unknown, streamEvent.Result!.ErrorKind);
        TestAssert.Equal("bad request", streamEvent.Result.UserMessage);
    }

    private static void MapsInvalidStreamingEvent()
    {
        var streamEvent = OpenAiTranslationService.ParseStreamingEvent(
            "response.output_text.delta",
            "{not-json",
            new StringBuilder());

        TestAssert.Equal(TranslationStreamEventKind.Failed, streamEvent!.Kind);
        TestAssert.Equal(TranslationErrorKind.InvalidRequest, streamEvent.Result!.ErrorKind);
    }
}
