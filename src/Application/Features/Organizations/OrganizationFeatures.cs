using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Common.Models;
using FlowDesk.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = FlowDesk.Application.Common.Exceptions.ValidationException;

namespace FlowDesk.Application.Features.Organizations;

public record OrganizationDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

// Create Command
public record CreateOrganizationCommand(string Name, string Code, string? Description) : IRequest<OrganizationDto>;

public class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    }
}

public class CreateOrganizationCommandHandler : IRequestHandler<CreateOrganizationCommand, OrganizationDto>
{
    private readonly IApplicationDbContext _context;

    public CreateOrganizationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrganizationDto> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _context.Organizations.AnyAsync(o => o.Code == code, cancellationToken))
        {
            throw new ValidationException(new[] { new FluentValidation.Results.ValidationFailure("Code", "An organization with this code already exists.") });
        }

        var organization = new Organization(request.Name, code, request.Description);
        _context.Organizations.Add(organization);
        await _context.SaveChangesAsync(cancellationToken);

        return new OrganizationDto(organization.Id, organization.Name, organization.Code, organization.Description, organization.IsActive, organization.CreatedAtUtc, organization.UpdatedAtUtc);
    }
}

// Update Command
public record UpdateOrganizationCommand(Guid Id, string Name, string? Description, bool IsActive) : IRequest<OrganizationDto>;

public class UpdateOrganizationCommandHandler : IRequestHandler<UpdateOrganizationCommand, OrganizationDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateOrganizationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrganizationDto> Handle(UpdateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);
        if (org == null) throw new NotFoundException(nameof(Organization), request.Id);

        var nameProp = typeof(Organization).GetProperty(nameof(Organization.Name));
        nameProp?.SetValue(org, request.Name.Trim());

        var descProp = typeof(Organization).GetProperty(nameof(Organization.Description));
        descProp?.SetValue(org, request.Description?.Trim());

        var activeProp = typeof(Organization).GetProperty(nameof(Organization.IsActive));
        activeProp?.SetValue(org, request.IsActive);

        await _context.SaveChangesAsync(cancellationToken);

        return new OrganizationDto(org.Id, org.Name, org.Code, org.Description, org.IsActive, org.CreatedAtUtc, org.UpdatedAtUtc);
    }
}

// Get All Query
public record GetOrganizationsQuery(int PageNumber = 1, int PageSize = 10, string? SearchTerm = null) : IRequest<PaginatedList<OrganizationDto>>;

public class GetOrganizationsQueryHandler : IRequestHandler<GetOrganizationsQuery, PaginatedList<OrganizationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOrganizationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<OrganizationDto>> Handle(GetOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Organizations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(o => o.Name.ToLower().Contains(term) || o.Code.ToLower().Contains(term));
        }

        var dtoQuery = query.OrderBy(o => o.Name)
            .Select(o => new OrganizationDto(o.Id, o.Name, o.Code, o.Description, o.IsActive, o.CreatedAtUtc, o.UpdatedAtUtc));

        return await PaginatedList<OrganizationDto>.CreateAsync(dtoQuery, request.PageNumber, request.PageSize, cancellationToken);
    }
}

// Get By Id Query
public record GetOrganizationByIdQuery(Guid Id) : IRequest<OrganizationDto>;

public class GetOrganizationByIdQueryHandler : IRequestHandler<GetOrganizationByIdQuery, OrganizationDto>
{
    private readonly IApplicationDbContext _context;

    public GetOrganizationByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrganizationDto> Handle(GetOrganizationByIdQuery request, CancellationToken cancellationToken)
    {
        var org = await _context.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);
        if (org == null) throw new NotFoundException(nameof(Organization), request.Id);

        return new OrganizationDto(org.Id, org.Name, org.Code, org.Description, org.IsActive, org.CreatedAtUtc, org.UpdatedAtUtc);
    }
}
