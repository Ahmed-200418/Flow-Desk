using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Application.Common.Models;
using FlowDesk.Domain.Common;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = FlowDesk.Application.Common.Exceptions.ValidationException;

namespace FlowDesk.Application.Features.Requests;

public record RequestItemDto(Guid Id, string ItemName, string? Description, int Quantity, decimal UnitPrice, decimal TotalPrice);

public record CommentDto(Guid Id, Guid AuthorUserId, string AuthorName, string Content, DateTime CreatedAtUtc);

public record AttachmentDto(Guid Id, string FileName, string ContentType, long FileSizeBytes, Guid UploadedByUserId, string UploadedByName, DateTime UploadedAtUtc);

public record RequestSummaryDto(
    Guid Id,
    string RequestNumber,
    Guid RequestTypeId,
    string RequestTypeName,
    Guid RequesterUserId,
    string RequesterName,
    Guid OrganizationId,
    Guid? DepartmentId,
    string? DepartmentName,
    RequestPriority Priority,
    RequestStatus Status,
    string Title,
    decimal TotalAmount,
    string Currency,
    int CurrentStepNumber,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? CompletedAtUtc
);

public record RequestDetailDto(
    Guid Id,
    string RequestNumber,
    Guid RequestTypeId,
    string RequestTypeName,
    Guid RequesterUserId,
    string RequesterName,
    Guid OrganizationId,
    Guid? DepartmentId,
    string? DepartmentName,
    RequestPriority Priority,
    RequestStatus Status,
    string Title,
    string Description,
    decimal TotalAmount,
    string Currency,
    Guid? WorkflowVersionId,
    int CurrentStepNumber,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? CompletedAtUtc,
    List<RequestItemDto> Items,
    List<CommentDto> Comments,
    List<AttachmentDto> Attachments
);

public record CreateRequestItemInput(string ItemName, int Quantity, decimal UnitPrice, string? Description = null);

// Create Request (Draft) Command
public record CreateRequestCommand(
    Guid OrganizationId,
    Guid RequestTypeId,
    string Title,
    string Description,
    RequestPriority Priority = RequestPriority.Medium,
    decimal TotalAmount = 0m,
    string Currency = "EGP",
    Guid? DepartmentId = null,
    List<CreateRequestItemInput>? Items = null
) : IRequest<RequestDetailDto>;

public class CreateRequestCommandValidator : AbstractValidator<CreateRequestCommand>
{
    public CreateRequestCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.RequestTypeId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.TotalAmount).GreaterThanOrEqualTo(0);
    }
}

public class CreateRequestCommandHandler : IRequestHandler<CreateRequestCommand, RequestDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRequestNumberGenerator _requestNumberGenerator;

    public CreateRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IRequestNumberGenerator requestNumberGenerator)
    {
        _context = context;
        _currentUserService = currentUserService;
        _requestNumberGenerator = requestNumberGenerator;
    }

    public async Task<RequestDetailDto> Handle(CreateRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue) throw new UnauthorizedException("Authenticated user required to create requests.");

        var requesterId = _currentUserService.UserId.Value;
        var requestNumber = await _requestNumberGenerator.GenerateRequestNumberAsync(cancellationToken);

        var req = new Request(
            requestNumber,
            request.RequestTypeId,
            requesterId,
            request.OrganizationId,
            request.Title,
            request.Description,
            request.TotalAmount,
            request.Currency,
            request.Priority,
            request.DepartmentId);

        if (request.Items != null && request.Items.Count != 0)
        {
            foreach (var itemInput in request.Items)
            {
                req.Items.Add(new RequestItem(req.Id, itemInput.ItemName, itemInput.Quantity, itemInput.UnitPrice, itemInput.Description));
            }
        }

        _context.Requests.Add(req);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetRequestDetailByIdAsync(req.Id, _context, cancellationToken);
    }

    public static async Task<RequestDetailDto> GetRequestDetailByIdAsync(Guid requestId, IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var r = await context.Requests
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.RequestType)
            .Include(x => x.RequesterUser)
            .Include(x => x.Department)
            .Include(x => x.Items)
            .Include(x => x.Comments).ThenInclude(c => c.AuthorUser)
            .Include(x => x.Attachments).ThenInclude(a => a.UploadedByUser)
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (r == null) throw new NotFoundException(nameof(Request), requestId);

        return new RequestDetailDto(
            r.Id,
            r.RequestNumber,
            r.RequestTypeId,
            r.RequestType.Name,
            r.RequesterUserId,
            r.RequesterUser.FullName,
            r.OrganizationId,
            r.DepartmentId,
            r.Department?.Name,
            r.Priority,
            r.Status,
            r.Title,
            r.Description,
            r.TotalAmount,
            r.Currency,
            r.WorkflowVersionId,
            r.CurrentStepNumber,
            r.CreatedAtUtc,
            r.SubmittedAtUtc,
            r.CompletedAtUtc,
            r.Items.Select(i => new RequestItemDto(i.Id, i.ItemName, i.Description, i.Quantity, i.UnitPrice, i.TotalPrice)).ToList(),
            r.Comments.OrderBy(c => c.CreatedAtUtc).Select(c => new CommentDto(c.Id, c.AuthorUserId, c.AuthorUser.FullName, c.Content, c.CreatedAtUtc)).ToList(),
            r.Attachments.OrderBy(a => a.UploadedAtUtc).Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.FileSizeBytes, a.UploadedByUserId, a.UploadedByUser.FullName, a.UploadedAtUtc)).ToList()
        );
    }
}

// Update Request Command
public record UpdateRequestCommand(
    Guid Id,
    string Title,
    string Description,
    RequestPriority Priority,
    decimal TotalAmount,
    string Currency,
    List<CreateRequestItemInput>? Items = null
) : IRequest<RequestDetailDto>;

public class UpdateRequestCommandHandler : IRequestHandler<UpdateRequestCommand, RequestDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateRequestCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<RequestDetailDto> Handle(UpdateRequestCommand request, CancellationToken cancellationToken)
    {
        var req = await _context.Requests.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (req == null) throw new NotFoundException(nameof(Request), request.Id);

        if (req.Status != RequestStatus.Draft && req.Status != RequestStatus.Returned)
        {
            throw new DomainException($"Cannot edit request in status '{req.Status}'. Only Draft or Returned requests can be edited.");
        }

        if (_currentUserService.UserId.HasValue && req.RequesterUserId != _currentUserService.UserId.Value)
        {
            throw new ForbiddenException("Only the original requester can edit this request.");
        }

        var titleProp = typeof(Request).GetProperty(nameof(Request.Title));
        titleProp?.SetValue(req, request.Title.Trim());

        var descProp = typeof(Request).GetProperty(nameof(Request.Description));
        descProp?.SetValue(req, request.Description.Trim());

        var priorityProp = typeof(Request).GetProperty(nameof(Request.Priority));
        priorityProp?.SetValue(req, request.Priority);

        var amountProp = typeof(Request).GetProperty(nameof(Request.TotalAmount));
        amountProp?.SetValue(req, request.TotalAmount);

        _context.RequestItems.RemoveRange(req.Items);
        if (request.Items != null)
        {
            foreach (var item in request.Items)
            {
                req.Items.Add(new RequestItem(req.Id, item.ItemName, item.Quantity, item.UnitPrice, item.Description));
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await CreateRequestCommandHandler.GetRequestDetailByIdAsync(req.Id, _context, cancellationToken);
    }
}

// Submit Request Command
public record SubmitRequestCommand(Guid RequestId) : IRequest<RequestDetailDto>;

public class SubmitRequestCommandHandler : IRequestHandler<SubmitRequestCommand, RequestDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IWorkflowEvaluator _workflowEvaluator;
    private readonly IApproverResolver _approverResolver;

    private readonly INotificationService? _notificationService;

    public SubmitRequestCommandHandler(
        IApplicationDbContext context,
        IWorkflowEvaluator workflowEvaluator,
        IApproverResolver approverResolver,
        INotificationService? notificationService = null)
    {
        _context = context;
        _workflowEvaluator = workflowEvaluator;
        _approverResolver = approverResolver;
        _notificationService = notificationService;
    }

    public async Task<RequestDetailDto> Handle(SubmitRequestCommand request, CancellationToken cancellationToken)
    {
        var req = await _context.Requests.FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken);
        if (req == null) throw new NotFoundException(nameof(Request), request.RequestId);

        var activeWorkflowVersion = await _context.WorkflowVersions
            .Include(wv => wv.Steps)
                .ThenInclude(s => s.Conditions)
            .Where(wv => wv.Workflow.RequestTypeId == req.RequestTypeId && wv.Status == WorkflowVersionStatus.Published)
            .OrderByDescending(wv => wv.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        Guid targetWorkflowVersionId = activeWorkflowVersion?.Id ?? Guid.NewGuid();
        req.Submit(targetWorkflowVersionId);

        if (activeWorkflowVersion != null && activeWorkflowVersion.Steps.Count > 0)
        {
            var matchingSteps = activeWorkflowVersion.Steps
                .OrderBy(s => s.StepNumber)
                .Where(s => _workflowEvaluator.EvaluateStepConditions(s.Conditions, req))
                .ToList();

            var firstStep = matchingSteps.FirstOrDefault();
            if (firstStep != null)
            {
                var (assignedUserId, assignedRoleId) = await _approverResolver.ResolveApproverAsync(firstStep, req, cancellationToken);

                var instance = new ApprovalInstance(
                    req.Id,
                    firstStep.Id,
                    firstStep.StepNumber,
                    assignedUserId,
                    assignedRoleId,
                    firstStep.TimeoutHours);

                _context.ApprovalInstances.Add(instance);

                if (assignedUserId.HasValue && _notificationService != null)
                {
                    await _notificationService.SendNotificationAsync(
                        assignedUserId.Value,
                        "Approval Required",
                        $"Request {req.RequestNumber} ({req.Title}) requires your approval.",
                        NotificationType.ApprovalRequired,
                        cancellationToken: cancellationToken);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await CreateRequestCommandHandler.GetRequestDetailByIdAsync(req.Id, _context, cancellationToken);
    }
}

// Cancel Request Command
public record CancelRequestCommand(Guid RequestId) : IRequest;

public class CancelRequestCommandHandler : IRequestHandler<CancelRequestCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CancelRequestCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(CancelRequestCommand request, CancellationToken cancellationToken)
    {
        var req = await _context.Requests.FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken);
        if (req == null) throw new NotFoundException(nameof(Request), request.RequestId);

        if (_currentUserService.UserId.HasValue && req.RequesterUserId != _currentUserService.UserId.Value)
        {
            throw new ForbiddenException("Only the requester can cancel this request.");
        }

        req.Cancel();
        await _context.SaveChangesAsync(cancellationToken);
    }
}

// Get Requests Query (Paginated & Filtered)
public record GetRequestsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    Guid? RequestTypeId = null,
    RequestStatus? Status = null,
    Guid? RequesterUserId = null,
    Guid? DepartmentId = null,
    RequestPriority? Priority = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    DateTime? FromDateUtc = null,
    DateTime? ToDateUtc = null
) : IRequest<PaginatedList<RequestSummaryDto>>;

public class GetRequestsQueryHandler : IRequestHandler<GetRequestsQuery, PaginatedList<RequestSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRequestsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<RequestSummaryDto>> Handle(GetRequestsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Requests.AsNoTracking().AsQueryable();

        if (request.RequestTypeId.HasValue) query = query.Where(r => r.RequestTypeId == request.RequestTypeId.Value);
        if (request.Status.HasValue) query = query.Where(r => r.Status == request.Status.Value);
        if (request.RequesterUserId.HasValue) query = query.Where(r => r.RequesterUserId == request.RequesterUserId.Value);
        if (request.DepartmentId.HasValue) query = query.Where(r => r.DepartmentId == request.DepartmentId.Value);
        if (request.Priority.HasValue) query = query.Where(r => r.Priority == request.Priority.Value);
        if (request.MinAmount.HasValue) query = query.Where(r => r.TotalAmount >= request.MinAmount.Value);
        if (request.MaxAmount.HasValue) query = query.Where(r => r.TotalAmount <= request.MaxAmount.Value);
        if (request.FromDateUtc.HasValue) query = query.Where(r => r.CreatedAtUtc >= request.FromDateUtc.Value);
        if (request.ToDateUtc.HasValue) query = query.Where(r => r.CreatedAtUtc <= request.ToDateUtc.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.RequestNumber.ToLower().Contains(term) ||
                r.Title.ToLower().Contains(term) ||
                r.Description.ToLower().Contains(term));
        }

        var dtoQuery = query.OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new RequestSummaryDto(
                r.Id,
                r.RequestNumber,
                r.RequestTypeId,
                r.RequestType.Name,
                r.RequesterUserId,
                r.RequesterUser.FullName,
                r.OrganizationId,
                r.DepartmentId,
                r.Department != null ? r.Department.Name : null,
                r.Priority,
                r.Status,
                r.Title,
                r.TotalAmount,
                r.Currency,
                r.CurrentStepNumber,
                r.CreatedAtUtc,
                r.SubmittedAtUtc,
                r.CompletedAtUtc));

        var clampedPageSize = Math.Min(Math.Max(1, request.PageSize), 100);
        var clampedPageNumber = Math.Max(1, request.PageNumber);

        return await PaginatedList<RequestSummaryDto>.CreateAsync(dtoQuery, clampedPageNumber, clampedPageSize, cancellationToken);
    }
}

// Get Request By Id Query
public record GetRequestByIdQuery(Guid Id) : IRequest<RequestDetailDto>;

public class GetRequestByIdQueryHandler : IRequestHandler<GetRequestByIdQuery, RequestDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetRequestByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<RequestDetailDto> Handle(GetRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var dto = await CreateRequestCommandHandler.GetRequestDetailByIdAsync(request.Id, _context, cancellationToken);

        if (_currentUserService.UserId.HasValue)
        {
            var userId = _currentUserService.UserId.Value;

            if (dto.RequesterUserId == userId)
            {
                return dto;
            }

            var isApproverOrActor = await _context.ApprovalInstances
                .AnyAsync(ai => ai.RequestId == request.Id &&
                                (ai.AssignedUserId == userId || ai.Actions.Any(a => a.ActorUserId == userId)), cancellationToken);

            if (isApproverOrActor)
            {
                return dto;
            }

            var hasGlobalPermission = await _context.UserRoles
                .Where(ur => ur.UserId == userId)
                .AnyAsync(ur => ur.Role.Name == "SuperAdmin" ||
                                ur.Role.Name == "OrganizationAdmin" ||
                                ur.Role.RolePermissions.Any(rp => rp.Permission.Code == "Permissions.Purchase.Read"), cancellationToken);

            if (!hasGlobalPermission)
            {
                throw new ForbiddenException("You are not authorized to view this request.");
            }
        }

        return dto;
    }
}

// Add Comment Command
public record AddCommentCommand(Guid RequestId, string Content) : IRequest<CommentDto>;

public class AddCommentCommandHandler : IRequestHandler<AddCommentCommand, CommentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AddCommentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<CommentDto> Handle(AddCommentCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue) throw new UnauthorizedException();

        var reqExists = await _context.Requests.AnyAsync(r => r.Id == request.RequestId, cancellationToken);
        if (!reqExists) throw new NotFoundException(nameof(Request), request.RequestId);

        var authorId = _currentUserService.UserId.Value;
        var comment = new Comment(request.RequestId, authorId, request.Content);

        _context.Comments.Add(comment);
        await _context.SaveChangesAsync(cancellationToken);

        var author = await _context.Users.FirstAsync(u => u.Id == authorId, cancellationToken);
        return new CommentDto(comment.Id, comment.AuthorUserId, author.FullName, comment.Content, comment.CreatedAtUtc);
    }
}

// Upload Attachment Command
public record UploadAttachmentCommand(Guid RequestId, Stream FileStream, string FileName, string ContentType, long FileSizeBytes) : IRequest<AttachmentDto>;

public class UploadAttachmentCommandHandler : IRequestHandler<UploadAttachmentCommand, AttachmentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAttachmentStorageService _storageService;

    public UploadAttachmentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAttachmentStorageService storageService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _storageService = storageService;
    }

    public async Task<AttachmentDto> Handle(UploadAttachmentCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue) throw new UnauthorizedException();

        var req = await _context.Requests.FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken);
        if (req == null) throw new NotFoundException(nameof(Request), request.RequestId);

        if (request.FileSizeBytes > 10 * 1024 * 1024)
        {
            throw new ValidationException(new[] { new FluentValidation.Results.ValidationFailure("FileSizeBytes", "Attachment file size cannot exceed 10 MB.") });
        }

        var (storedFileName, filePath) = await _storageService.SaveFileAsync(request.FileStream, request.FileName, cancellationToken);
        var userId = _currentUserService.UserId.Value;

        var attachment = new Attachment(request.RequestId, request.FileName, storedFileName, request.ContentType, request.FileSizeBytes, filePath, userId);
        _context.Attachments.Add(attachment);

        await _context.SaveChangesAsync(cancellationToken);

        var uploader = await _context.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        return new AttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.FileSizeBytes, attachment.UploadedByUserId, uploader.FullName, attachment.UploadedAtUtc);
    }
}

// Delete Request Command (for Drafts or Admin)
public record DeleteRequestCommand(Guid RequestId) : IRequest;

public class DeleteRequestCommandHandler : IRequestHandler<DeleteRequestCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteRequestCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteRequestCommand request, CancellationToken cancellationToken)
    {
        var req = await _context.Requests.FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken);
        if (req == null) throw new NotFoundException(nameof(Request), request.RequestId);

        _context.Requests.Remove(req);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
