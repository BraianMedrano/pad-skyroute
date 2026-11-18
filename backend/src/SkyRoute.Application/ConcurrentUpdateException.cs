namespace SkyRoute.Application;

// A repository could not save because someone else changed the same mission or drone after it was read.
public sealed class ConcurrentUpdateException(string message, Exception? innerException = null)
    : Exception(message, innerException);
