using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Features.Positions;

public record PositionDto(
    Guid Id,
    Guid DepartmentId,
    string Title,
    string Code,
    int Level
);

// Create Command
public record CreatePositionCommand(Guid DepartmentId, string Title, string Code, int Level = 1) : IRequest<PositionDto>;

public class CreatePositionCommandValidator : AbstractValidator<CreatePositionCommand>
{
    public CreatePositionCommandValidator()
    {
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Level).GreaterThanOrEqualTo(1);
    }
}

public class CreatePositionCommandHandler : IRequestHandler<CreatePositionCommand, PositionDto>
{
    private readonly IApplicationDbContext _context;

    public CreatePositionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PositionDto> Handle(CreatePositionCommand request, CancellationToken cancellationToken)
    {
        var deptExists = await _context.Departments.AnyAsync(d => d.Id == request.DepartmentId, cancellationToken);
        if (!deptExists) throw new NotFoundException(nameof(Department), request.DepartmentId);

        var position = new Position(request.DepartmentId, request.Title, request.Code, request.Level);
        _context.Positions.Add(position);
        await _context.SaveChangesAsync(cancellationToken);

        return new PositionDto(position.Id, position.DepartmentId, position.Title, position.Code, position.Level);
    }
}

// Get By Department Query
public record GetPositionsByDepartmentQuery(Guid DepartmentId) : IRequest<List<PositionDto>>;

public class GetPositionsByDepartmentQueryHandler : IRequestHandler<GetPositionsByDepartmentQuery, List<PositionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPositionsByDepartmentQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PositionDto>> Handle(GetPositionsByDepartmentQuery request, CancellationToken cancellationToken)
    {
        return await _context.Positions
            .AsNoTracking()
            .Where(p => p.DepartmentId == request.DepartmentId)
            .OrderBy(p => p.Level)
            .ThenBy(p => p.Title)
            .Select(p => new PositionDto(p.Id, p.DepartmentId, p.Title, p.Code, p.Level))
            .ToListAsync(cancellationToken);
    }
}
