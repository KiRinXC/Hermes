namespace Hermes.Tests;

public sealed class TestSuite
{
    private readonly List<(string Name, Action Test)> _tests = new();

    public void Add(string name, Action test)
    {
        _tests.Add((name, test));
    }

    public void Run()
    {
        var failed = 0;
        foreach (var (name, test) in _tests)
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
    }
}
