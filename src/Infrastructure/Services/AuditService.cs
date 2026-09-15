using System.Text.Json;
using System.Text.Json.Nodes;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Entities;

namespace FlowDesk.Infrastructure.Services;

public class AuditService : IAuditService
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "secret",
        "token",
        "refreshtoken",
        "securitystamp",
        "creditcard",
        "ssn",
        "apikey",
        "authorization"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        IgnoreReadOnlyProperties = false
    };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AuditService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string entityId,
        Guid? userId = null,
        string? userEmail = null,
        string? ipAddress = null,
        string? userAgent = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveUserId = userId ?? _currentUserService.UserId;
        var effectiveUserEmail = userEmail ?? _currentUserService.UserEmail;
        var effectiveIp = ipAddress ?? _currentUserService.IpAddress;

        var oldJson = SerializeAndRedact(oldValues);
        var newJson = SerializeAndRedact(newValues);

        var auditLog = new AuditLog(
            action: action,
            entityName: entityName,
            entityId: entityId,
            userId: effectiveUserId,
            userEmail: effectiveUserEmail,
            ipAddress: effectiveIp,
            userAgent: userAgent,
            oldValuesJson: oldJson,
            newValuesJson: newJson
        );

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public static string? SerializeAndRedact(object? obj)
    {
        if (obj == null) return null;

        try
        {
            if (obj is string str)
            {
                if (str.TrimStart().StartsWith("{") || str.TrimStart().StartsWith("["))
                {
                    var parsedNode = JsonNode.Parse(str);
                    if (parsedNode != null)
                    {
                        RedactJsonNode(parsedNode);
                        return parsedNode.ToJsonString(JsonOptions);
                    }
                }
                return str;
            }

            var node = JsonSerializer.SerializeToNode(obj, JsonOptions);
            if (node == null) return null;

            RedactJsonNode(node);
            return node.ToJsonString(JsonOptions);
        }
        catch
        {
            return obj.ToString();
        }
    }

    private static void RedactJsonNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var keys = obj.Select(k => k.Key).ToList();
            foreach (var key in keys)
            {
                if (IsSensitiveKey(key))
                {
                    obj[key] = "[REDACTED]";
                }
                else if (obj[key] != null)
                {
                    RedactJsonNode(obj[key]!);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                {
                    RedactJsonNode(item);
                }
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return SensitiveKeys.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase));
    }
}
