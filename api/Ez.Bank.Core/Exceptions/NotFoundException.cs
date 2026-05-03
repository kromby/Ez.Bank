namespace Ez.Bank.Core.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityType, string id)
        : base($"{entityType} with id '{id}' was not found.") { }
}
