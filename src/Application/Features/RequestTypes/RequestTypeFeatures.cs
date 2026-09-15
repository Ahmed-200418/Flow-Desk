using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = FlowDesk.Application.Common.Exceptions.ValidationException;

namespace FlowDesk.Application.Features.RequestTypes;

public record RequestTypeDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Code,
    string Description,
    string? Icon,
    bool IsActive,
    DateTime CreatedAtUtc
);

// Create Request Type Command
public record CreateRequestTypeCommand(
    Guid OrganizationId,
    string Name,
    string Code,
    string Description,
    string? Icon = null
) : IRequest<RequestTypeDto>;

public class CreateRequestTypeCommandValidator : AbstractValidator<CreateRequestTypeCommand>
{
    public CreateRequestTypeCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
    }
}

public class CreateRequestTypeCommandHandler : IRequestHandler<CreateRequestTypeCommand, RequestTypeDto>
{
    private readonly IApplicationDbContext _context;

    public CreateRequestTypeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RequestTypeDto> Handle(CreateRequestTypeCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _context.RequestTypes.AnyAsync(rt => rt.OrganizationId == request.OrganizationId && rt.Code == code, cancellationToken))
        {
            throw new ValidationException(new[] { new FluentValidation.Results.ValidationFailure("Code", "A request type with this code already exists in the organization.") });
        }

        var rt = new RequestType(request.OrganizationId, request.Name, code, request.Description, request.Icon);
        _context.RequestTypes.Add(rt);
        await _context.SaveChangesAsync(cancellationToken);

        return new RequestTypeDto(rt.Id, rt.OrganizationId, rt.Name, rt.Code, rt.Description, rt.Icon, rt.IsActive, rt.CreatedAtUtc);
    }
}

// Get Request Types Query
public record GetRequestTypesQuery(Guid? OrganizationId = null) : IRequest<List<RequestTypeDto>>;

public class GetRequestTypesQueryHandler : IRequestHandler<GetRequestTypesQuery, List<RequestTypeDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRequestTypesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<RequestTypeDto>> Handle(GetRequestTypesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.RequestTypes.AsNoTracking().Where(rt => rt.IsActive);
        if (request.OrganizationId.HasValue)
        {
            query = query.Where(rt => rt.OrganizationId == request.OrganizationId.Value);
        }

        return await query
            .OrderBy(rt => rt.Name)
            .Select(rt => new RequestTypeDto(rt.Id, rt.OrganizationId, rt.Name, rt.Code, rt.Description, rt.Icon, rt.IsActive, rt.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
