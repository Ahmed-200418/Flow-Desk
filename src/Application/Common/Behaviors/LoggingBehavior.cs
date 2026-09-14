using FlowDesk.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FlowDesk.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;
    private readonly ICurrentUserService _currentUserService;

    public LoggingBehavior(ILogger<TRequest> logger, ICurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUserService.UserId?.ToString() ?? "Anonymous";

        _logger.LogInformation("FlowDesk Request: {Name} [{UserId}] {@Request}", requestName, userId, request);

        var response = await next();

        return response;
    }
}
