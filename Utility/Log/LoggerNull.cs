using System.Text;
// ReSharper disable MethodOverloadWithOptionalParameter
// ReSharper disable CheckNamespace

namespace Utility.Log;

public sealed class LoggerNull : LoggerBase
{
    public LoggerNull(string folderPath, Encoding encoding) : base(folderPath, encoding) { }
    internal LoggerNull() { }
}
