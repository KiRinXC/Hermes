namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Services;

public sealed class CodexSwitchException : Exception
{
    public CodexSwitchException(string message)
        : base(message)
    {
    }

    public CodexSwitchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
