using Hermes.Tests;
using Hermes.Tests.Infrastructure;
using Hermes.Tests.Input;
using Hermes.Tests.Selection;
using Hermes.Tests.Settings;
using Hermes.Tests.Shell;
using Hermes.Tests.Translation;
using Hermes.Tests.Tray;

var suite = new TestSuite();
SettingsTests.Register(suite);
RedactorTests.Register(suite);
SelectionTextValidatorTests.Register(suite);
SelectionCandidateServiceTests.Register(suite);
OpenAiTranslationServiceTests.Register(suite);
HotkeyGestureTests.Register(suite);
SettingsWindowOptionTests.Register(suite);
TrayServiceTests.Register(suite);

suite.Run();
