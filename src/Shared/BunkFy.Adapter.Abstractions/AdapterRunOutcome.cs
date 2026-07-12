namespace BunkFy.Adapter.Abstractions;

public enum AdapterRunOutcome
{
    Unknown = 0,
    Succeeded = 1,
    PartiallySucceeded = 2,
    Failed = 3,
    Cancelled = 4
}
