using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Cash.Diagnostic;

[DebuggerStepThrough]
internal static class DebugLog
{
    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void Log(string message) => Debug.WriteLine($"[{nameof(Cash)}] {message}");

    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteDebug(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0) =>
        Log($"DEBUG: {FormatCallSite(callerFilePath, callerLineNumber)}{message}");

    [Conditional("DEBUG")]
    public static void WriteDiagnostic(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0) =>
        Log($"DIAGNOSTIC: {FormatCallSite(callerFilePath, callerLineNumber)}{message}");

    [Conditional("DEBUG")]
    public static void WriteError(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0) =>
        Log($"ERROR: {FormatCallSite(callerFilePath, callerLineNumber)}{message}");

    [Conditional("DEBUG")]
    public static void WriteEvent(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0) =>
        Log($"EVENT: {FormatCallSite(callerFilePath, callerLineNumber)}{message}");

    [Conditional("DEBUG")]
    public static void WriteException(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0) =>
        Log($"EXCEPTION: {FormatCallSite(callerFilePath, callerLineNumber)}{exception}");

    [Conditional("DEBUG")]
    public static void WriteException(Exception exception, string additionalInfo, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0) =>
        Log($"EXCEPTION: {FormatCallSite(callerFilePath, callerLineNumber)}{additionalInfo}\n{exception}");

    [Conditional("DEBUG")]
    public static void WriteInfo(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0) =>
        Log($"INFO: {FormatCallSite(callerFilePath, callerLineNumber)}{message}");

    [Conditional("DEBUG")]
    public static void WriteWarning(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0) =>
        Log($"WARNING: {FormatCallSite(callerFilePath, callerLineNumber)}{message}");

    private static string FormatCallSite(string callsite, int lineNo)
    {
        if (string.IsNullOrEmpty(callsite))
        {
            return string.Empty;
        }
        int fileNameIndex = callsite.AsSpan().LastIndexOfAny('\\', '/');
        if (fileNameIndex == -1 || ++fileNameIndex >= callsite.Length)
        {
            return $"{callsite}::L{lineNo}: ";
        }
        return $"{callsite[fileNameIndex..]}::L{lineNo}: ";
    }
}
