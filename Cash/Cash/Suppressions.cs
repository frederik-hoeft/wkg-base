namespace Cash;

internal static class Suppressions
{
    public const string RELIABILITY = "Reliability";

    public const string CA2000_DISPOSE_OBJECT = "CA2000:Dispose objects before losing scope";

    public const string JUSTIFY_CA2000_OWNERSHIP_TRANSFER_TO_CALLER = "Ownership of the disposable object is transferred to the caller.";

    public const string JUSTIFY_CA2000_OWNERSHIP_TRANSFER_TO_PROXY = "Ownership of the disposable object is transferred to a proxy which will dispose it.";
}