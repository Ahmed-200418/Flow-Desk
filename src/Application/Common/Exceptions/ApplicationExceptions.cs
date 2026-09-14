namespace FlowDesk.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string name, object key) : base($"Entity \"{name}\" ({key}) was not found.") { }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Invalid credentials or unauthorized access.") : base(message) { }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "You do not have permission to access this resource.") : base(message) { }
}
