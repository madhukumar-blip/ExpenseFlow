namespace ExpenseFlow.Application.Expenses;

public sealed class ExpenseConflictException : Exception
{
    public ExpenseConflictException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}