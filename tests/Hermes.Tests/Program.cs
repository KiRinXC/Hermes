using Hermes.Tests;
using Hermes.Tests.Infrastructure;
using Hermes.Tests.Input;
using Hermes.Tests.Selection;
using Hermes.Tests.Settings;
using Hermes.Tests.Translation;

var suite = new TestSuite();
SettingsTests.Register(suite);
RedactorTests.Register(suite);
SelectionTextValidatorTests.Register(suite);
SelectionCandidateServiceTests.Register(suite);
OpenAiTranslationServiceTests.Register(suite);
HotkeyGestureTests.Register(suite);

suite.Run();
