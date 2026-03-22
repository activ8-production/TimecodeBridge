namespace TimecodeBridge.ViewModels;

public record LogEntry(DateTime Timestamp, string Message, bool IsSuccess);
