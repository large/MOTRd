using System;

namespace WebSockets.Common
{
    public interface IWebSocketLogger
    {
        void Information(Type type, string format, params object[] args);
        void Warning(Type type, string format, params object[] args);
        void Error(Type type, string format, params object[] args);
        void Error(Type type, Exception exception);
        void Debug(Type type, string message,
                     [System.Runtime.CompilerServices.CallerMemberName]  string memberName = "",
                     [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
                     [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0);
    }
}
