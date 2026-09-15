using FlowDesk.Domain.Entities;

namespace FlowDesk.Application.Common.Interfaces;

public interface IWorkflowEvaluator
{
    bool EvaluateCondition(WorkflowCondition condition, Request request);
    bool EvaluateStepConditions(IEnumerable<WorkflowCondition> conditions, Request request);
}
