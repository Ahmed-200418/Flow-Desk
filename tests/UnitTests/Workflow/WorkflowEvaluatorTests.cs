using FlowDesk.Domain.Entities;
using FlowDesk.Domain.Enums;
using FlowDesk.Infrastructure.Services;
using Xunit;

namespace FlowDesk.UnitTests.Workflow;

public class WorkflowEvaluatorTests
{
    private readonly WorkflowEvaluator _evaluator = new();

    [Theory]
    [InlineData(10000, 5000, ConditionOperator.GreaterThan, true)]
    [InlineData(3000, 5000, ConditionOperator.GreaterThan, false)]
    [InlineData(5000, 5000, ConditionOperator.GreaterThanOrEqual, true)]
    [InlineData(5000, 5000, ConditionOperator.Equals, true)]
    [InlineData(4000, 5000, ConditionOperator.LessThan, true)]
    public void EvaluateCondition_NumericTotalAmount_ShouldReturnExpectedResult(
        decimal actualAmount, decimal targetAmount, ConditionOperator op, bool expected)
    {
        var orgId = Guid.NewGuid();
        var reqTypeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var request = new Request("REQ-100", reqTypeId, userId, orgId, "Laptop Purchase", "High Spec", actualAmount, "EGP");
        var condition = new WorkflowCondition(Guid.NewGuid(), "TotalAmount", op, targetAmount.ToString());

        var result = _evaluator.EvaluateCondition(condition, request);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void EvaluateCondition_StringCurrency_ShouldReturnTrueForEquals()
    {
        var orgId = Guid.NewGuid();
        var reqTypeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var request = new Request("REQ-101", reqTypeId, userId, orgId, "Software License", "Desc", 1000m, "USD");
        var condition = new WorkflowCondition(Guid.NewGuid(), "Currency", ConditionOperator.Equals, "USD");

        var result = _evaluator.EvaluateCondition(condition, request);

        Assert.True(result);
    }

    [Fact]
    public void EvaluateCondition_InOperator_ShouldReturnTrueWhenValueMatches()
    {
        var orgId = Guid.NewGuid();
        var reqTypeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var request = new Request("REQ-102", reqTypeId, userId, orgId, "Urgent Equipment", "Desc", 1000m, "EGP", RequestPriority.High);
        var condition = new WorkflowCondition(Guid.NewGuid(), "Priority", ConditionOperator.In, "High,Urgent");

        var result = _evaluator.EvaluateCondition(condition, request);

        Assert.True(result);
    }

    [Fact]
    public void EvaluateStepConditions_EmptyConditions_ShouldReturnTrue()
    {
        var request = new Request("REQ-103", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Title", "Desc");

        var result = _evaluator.EvaluateStepConditions(Enumerable.Empty<WorkflowCondition>(), request);

        Assert.True(result);
    }

    [Fact]
    public void EvaluateStepConditions_MultipleAndConditions_AllMustPass()
    {
        var request = new Request("REQ-104", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Server", "Desc", 15000m, "USD");

        var stepId = Guid.NewGuid();
        var c1 = new WorkflowCondition(stepId, "TotalAmount", ConditionOperator.GreaterThan, "10000", "AND");
        var c2 = new WorkflowCondition(stepId, "Currency", ConditionOperator.Equals, "USD", "AND");

        var result = _evaluator.EvaluateStepConditions(new[] { c1, c2 }, request);

        Assert.True(result);
    }
}
