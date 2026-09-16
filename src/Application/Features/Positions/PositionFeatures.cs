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

// Get Position By Id Query
public record GetPositionByIdQuery(Guid Id) : IRequest<PositionDto>;

public class GetPositionByIdQueryHandler : IRequestHandler<GetPositionByIdQuery, PositionDto>
{
    private readonly IApplicationDbContext _context;

    public GetPositionByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PositionDto> Handle(GetPositionByIdQuery request, CancellationToken cancellationToken)
    {
        var position = await _context.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (position == null) throw new NotFoundException(nameof(Position), request.Id);

        return new PositionDto(position.Id, position.DepartmentId, position.Title, position.Code, position.Level);
    }
}

// Update Position Command
public record UpdatePositionCommand(Guid Id, string Title, int Level) : IRequest<PositionDto>;

public class UpdatePositionCommandHandler : IRequestHandler<UpdatePositionCommand, PositionDto>
{
    private readonly IApplicationDbContext _context;

    public UpdatePositionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PositionDto> Handle(UpdatePositionCommand request, CancellationToken cancellationToken)
    {
        var position = await _context.Positions.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (position == null) throw new NotFoundException(nameof(Position), request.Id);

        var titleProp = typeof(Position).GetProperty(nameof(Position.Title));
        titleProp?.SetValue(position, request.Title.Trim());

        var levelProp = typeof(Position).GetProperty(nameof(Position.Level));
        levelProp?.SetValue(position, request.Level);

        await _context.SaveChangesAsync(cancellationToken);

        return new PositionDto(position.Id, position.DepartmentId, position.Title, position.Code, position.Level);
    }
}

// Delete Position Command
public record DeletePositionCommand(Guid Id) : IRequest;

public class DeletePositionCommandHandler : IRequestHandler<DeletePositionCommand>
{
    private readonly IApplicationDbContext _context;

    public DeletePositionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeletePositionCommand request, CancellationToken cancellationToken)
    {
        var position = await _context.Positions.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (position == null) throw new NotFoundException(nameof(Position), request.Id);

        _context.Positions.Remove(position);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
