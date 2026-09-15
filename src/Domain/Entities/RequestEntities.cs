using FlowDesk.Domain.Common;
using FlowDesk.Domain.Enums;

namespace FlowDesk.Domain.Entities;

public class RequestType : AuditableEntity
{
    public Guid OrganizationId { get; private set; }
    public Organization Organization { get; private set; } = default!;

    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string? Icon { get; private set; }
    public bool IsActive { get; private set; } = true;

    public ICollection<Request> Requests { get; private set; } = new List<Request>();
    public ICollection<Workflow> Workflows { get; private set; } = new List<Workflow>();

    private RequestType() { }

    public RequestType(Guid organizationId, string name, string code, string description, string? icon = null)
    {
        OrganizationId = organizationId;
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Description = description.Trim();
        Icon = icon?.Trim();
        IsActive = true;
    }
}

public class Request : AuditableEntity
{
    public string RequestNumber { get; private set; } = default!;
    public Guid RequestTypeId { get; private set; }
    public RequestType RequestType { get; private set; } = default!;

    public Guid RequesterUserId { get; private set; }
    public User RequesterUser { get; private set; } = default!;

    public Guid OrganizationId { get; private set; }
    public Organization Organization { get; private set; } = default!;

    public Guid? DepartmentId { get; private set; }
    public Department? Department { get; private set; }

    public RequestPriority Priority { get; private set; } = RequestPriority.Medium;
    public RequestStatus Status { get; private set; } = RequestStatus.Draft;

    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public decimal TotalAmount { get; private set; } = 0m;
    public string Currency { get; private set; } = "EGP";

    public Guid? WorkflowVersionId { get; private set; }
    public WorkflowVersion? WorkflowVersion { get; private set; }
    public int CurrentStepNumber { get; private set; } = 0;

    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();

    public ICollection<RequestItem> Items { get; private set; } = new List<RequestItem>();
    public ICollection<Comment> Comments { get; private set; } = new List<Comment>();
    public ICollection<Attachment> Attachments { get; private set; } = new List<Attachment>();
    public ICollection<ApprovalInstance> ApprovalInstances { get; private set; } = new List<ApprovalInstance>();

    private Request() { }

    public Request(
        string requestNumber,
        Guid requestTypeId,
        Guid requesterUserId,
        Guid organizationId,
        string title,
        string description,
        decimal totalAmount = 0m,
        string currency = "EGP",
        RequestPriority priority = RequestPriority.Medium,
        Guid? departmentId = null)
    {
        RequestNumber = requestNumber;
        RequestTypeId = requestTypeId;
        RequesterUserId = requesterUserId;
        OrganizationId = organizationId;
        Title = title.Trim();
        Description = description.Trim();
        TotalAmount = totalAmount;
        Currency = currency.Trim().ToUpperInvariant();
        Priority = priority;
        DepartmentId = departmentId;
        Status = RequestStatus.Draft;
        RowVersion = Guid.NewGuid().ToByteArray();
    }

    public void Submit(Guid workflowVersionId)
    {
        if (Status != RequestStatus.Draft && Status != RequestStatus.Returned)
        {
            throw new DomainException($"Cannot submit request in status '{Status}'. Only Draft or Returned requests can be submitted.");
        }

        Status = RequestStatus.PendingApproval;
        WorkflowVersionId = workflowVersionId;
        CurrentStepNumber = 1;
        SubmittedAtUtc = DateTime.UtcNow;
    }

    public void Approve()
    {
        Status = RequestStatus.Approved;
    }

    public void Complete()
    {
        Status = RequestStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Reject()
    {
        Status = RequestStatus.Rejected;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void ReturnToDraft()
    {
        Status = RequestStatus.Returned;
    }

    public void Cancel()
    {
        Status = RequestStatus.Cancelled;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void AdvanceToStep(int nextStepNumber)
    {
        CurrentStepNumber = nextStepNumber;
    }
}

public class RequestItem : Entity
{
    public Guid RequestId { get; private set; }
    public Request Request { get; private set; } = default!;

    public string ItemName { get; private set; } = default!;
    public string? Description { get; private set; }
    public int Quantity { get; private set; } = 1;
    public decimal UnitPrice { get; private set; } = 0m;
    public decimal TotalPrice => Quantity * UnitPrice;

    private RequestItem() { }

    public RequestItem(Guid requestId, string itemName, int quantity, decimal unitPrice, string? description = null)
    {
        RequestId = requestId;
        ItemName = itemName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        Description = description?.Trim();
    }
}

public class Comment : Entity
{
    public Guid RequestId { get; private set; }
    public Request Request { get; private set; } = default!;

    public Guid AuthorUserId { get; private set; }
    public User AuthorUser { get; private set; } = default!;

    public string Content { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private Comment() { }

    public Comment(Guid requestId, Guid authorUserId, string content)
    {
        RequestId = requestId;
        AuthorUserId = authorUserId;
        Content = content.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }
}

public class Attachment : Entity
{
    public Guid RequestId { get; private set; }
    public Request Request { get; private set; } = default!;

    public string FileName { get; private set; } = default!;
    public string StoredFileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long FileSizeBytes { get; private set; }
    public string FilePath { get; private set; } = default!;

    public Guid UploadedByUserId { get; private set; }
    public User UploadedByUser { get; private set; } = default!;

    public DateTime UploadedAtUtc { get; private set; } = DateTime.UtcNow;

    private Attachment() { }

    public Attachment(
        Guid requestId,
        string fileName,
        string storedFileName,
        string contentType,
        long fileSizeBytes,
        string filePath,
        Guid uploadedByUserId)
    {
        RequestId = requestId;
        FileName = fileName.Trim();
        StoredFileName = storedFileName.Trim();
        ContentType = contentType.Trim();
        FileSizeBytes = fileSizeBytes;
        FilePath = filePath.Trim();
        UploadedByUserId = uploadedByUserId;
        UploadedAtUtc = DateTime.UtcNow;
    }
}
