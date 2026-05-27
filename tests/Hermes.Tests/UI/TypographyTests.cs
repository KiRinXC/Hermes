using System.IO;

namespace Hermes.Tests.UI;

public static class TypographyTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("ui typography avoids heavy font weights", UiTypographyAvoidsHeavyFontWeights);
    }

    private static void UiTypographyAvoidsHeavyFontWeights()
    {
        var root = FindRepoRoot();
        var uiFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src", "Hermes.Windows"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Shell{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}Overlay{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}Tray{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}UI{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var path in uiFiles)
        {
            var text = File.ReadAllText(path);
            TestAssert.False(text.Contains("FontWeight=\"Bold\"", StringComparison.Ordinal));
            TestAssert.False(text.Contains("FontWeight=\"SemiBold\"", StringComparison.Ordinal));
            TestAssert.False(text.Contains("Value=\"Bold\"", StringComparison.Ordinal));
            TestAssert.False(text.Contains("Value=\"SemiBold\"", StringComparison.Ordinal));
            TestAssert.False(text.Contains("FontWeights.Bold", StringComparison.Ordinal));
            TestAssert.False(text.Contains("FontWeights.SemiBold", StringComparison.Ordinal));
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hermes.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repo root.");
    }
}
