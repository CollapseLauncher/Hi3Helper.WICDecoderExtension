// ReSharper disable CheckNamespace
namespace Utility.Log;

public enum LogType
{
    Info    = 0,
    Default = Info,
    Warning,
    Error,
    Scheme,
    Game,
    Debug,
    GLC,
    Sentry,
    NoTag
}