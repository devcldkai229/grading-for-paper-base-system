namespace GradingService.Domain.Exceptions;

/// <summary>Thrown by the persistence layer when an optimistic-concurrency check fails on save.</summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The record was modified by another operation before this change could be saved.")
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
