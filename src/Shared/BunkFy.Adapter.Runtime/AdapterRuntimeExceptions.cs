namespace BunkFy.Adapter.Runtime;

public sealed class AdapterCheckpointException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

public sealed class AdapterRuntimeProtocolException(string message)
    : InvalidOperationException(message);

public sealed class AdapterRemoteLeaseLostException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
