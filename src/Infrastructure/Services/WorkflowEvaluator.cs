using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;

namespace FlowDesk.Infrastructure.Services;

public class WorkflowEvaluator : IWorkflowEvaluator
{
    public bool EvaluateStepConditions(IEnumerable<WorkflowCondition> conditions, Request request)
    {
        var conditionList = conditions.ToList();
        if (conditionList.Count == 0) return true;

        var andConditions = conditionList.Where(c => c.LogicGroup.Equals("AND", StringComparison.OrdinalIgnoreCase)).ToList();
        var orConditions = conditionList.Where(c => c.LogicGroup.Equals("OR", StringComparison.OrdinalIgnoreCase)).ToList();

        bool andResult = andConditions.Count == 0 || andConditions.All(c => EvaluateCondition(c, request));
        bool orResult = orConditions.Count == 0 || orConditions.Any(c => EvaluateCondition(c, request));

        return andResult && orResult;
    }

    public bool EvaluateCondition(WorkflowCondition condition, Request request)
    {
        if (condition == null || request == null) return false;

        var propertyName = condition.FieldName.Trim();
        var rawPropertyValue = GetRequestPropertyValue(propertyName, request);

        if (rawPropertyValue == null) return false;

        return CompareValues(rawPropertyValue, condition.Operator, condition.Value);
    }

    private static object? GetRequestPropertyValue(string fieldName, Request request)
    {
        return fieldName.ToLowerInvariant() switch
        {
            "totalamount" or "amount" => request.TotalAmount,
            "currency" => request.Currency,
            "priority" => request.Priority.ToString(),
            "departmentid" or "department" => request.DepartmentId?.ToString(),
            "organizationid" => request.OrganizationId.ToString(),
            "requesttypeid" => request.RequestTypeId.ToString(),
            "title" => request.Title,
            "description" => request.Description,
            _ => null
        };
    }

    private static bool CompareValues(object actualValue, ConditionOperator op, string targetValueStr)
    {
        if (actualValue is decimal actualDecimal)
        {
            if (!decimal.TryParse(targetValueStr, out var targetDecimal)) return false;

            return op switch
            {
                ConditionOperator.Equals => actualDecimal == targetDecimal,
                ConditionOperator.NotEquals => actualDecimal != targetDecimal,
                ConditionOperator.GreaterThan => actualDecimal > targetDecimal,
                ConditionOperator.GreaterThanOrEqual => actualDecimal >= targetDecimal,
                ConditionOperator.LessThan => actualDecimal < targetDecimal,
                ConditionOperator.LessThanOrEqual => actualDecimal <= targetDecimal,
                ConditionOperator.In => targetValueStr.Split(',')
                    .Select(v => decimal.TryParse(v.Trim(), out var d) ? (decimal?)d : null)
                    .Any(d => d.HasValue && d.Value == actualDecimal),
                _ => false
            };
        }

        var actualStr = actualValue.ToString() ?? string.Empty;
        var targetStr = targetValueStr.Trim();

        return op switch
        {
            ConditionOperator.Equals => actualStr.Equals(targetStr, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotEquals => !actualStr.Equals(targetStr, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.Contains => actualStr.Contains(targetStr, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.In => targetStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(v => v.Equals(actualStr, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }
}
