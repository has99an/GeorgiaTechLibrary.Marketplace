namespace OrderService.Domain.Exceptions;

/// <summary>
/// Exception thrown when an invalid order state transition is attempted
/// </summary>
public class InvalidOrderStateException : DomainException
{
    public string CurrentState { get; }
    public string AttemptedState { get; }

    public InvalidOrderStateException(string currentState, string attemptedState) 
        : base($"Cannot transition order from '{currentState}' to '{attemptedState}'.")
    {
        CurrentState = currentState;
        AttemptedState = attemptedState;
    }

    public InvalidOrderStateException(string message) : base(message)
    {
        CurrentState = string.Empty;
        AttemptedState = string.Empty;
    }
}

