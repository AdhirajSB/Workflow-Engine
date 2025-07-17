using WorkflowEngine.Models;

namespace WorkflowEngine.Validation
{
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }

    public interface IWorkflowValidator
    {
        Task ValidateDefinitionAsync(WorkflowDefinition definition);
        Task ValidateActionExecutionAsync(WorkflowInstance instance, WorkflowAction action, WorkflowDefinition definition);
    }

    public class WorkflowValidator : IWorkflowValidator
    {
        public Task ValidateDefinitionAsync(WorkflowDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.Name))
                throw new ValidationException("Definition name is required");

            if (!definition.States.Any())
                throw new ValidationException("Definition must have at least one state");

            // Check for duplicate state IDs
            var stateIds = definition.States.Select(s => s.Id).ToList();
            if (stateIds.Count != stateIds.Distinct().Count())
                throw new ValidationException("Duplicate state IDs found");

            // Check for duplicate action IDs
            var actionIds = definition.Actions.Select(a => a.Id).ToList();
            if (actionIds.Count != actionIds.Distinct().Count())
                throw new ValidationException("Duplicate action IDs found");

            // Exactly one initial state
            var initialStates = definition.States.Where(s => s.IsInitial).ToList();
            if (initialStates.Count != 1)
                throw new ValidationException("Definition must have exactly one initial state");

            // Validate actions reference valid states
            foreach (var action in definition.Actions)
            {
                if (!stateIds.Contains(action.ToState))
                    throw new ValidationException($"Action '{action.Id}' references unknown target state '{action.ToState}'");

                foreach (var fromState in action.FromStates)
                {
                    if (!stateIds.Contains(fromState))
                        throw new ValidationException($"Action '{action.Id}' references unknown source state '{fromState}'");
                }
            }

            return Task.CompletedTask;
        }

        public Task ValidateActionExecutionAsync(WorkflowInstance instance, WorkflowAction action, WorkflowDefinition definition)
        {
            if (!action.Enabled)
                throw new ValidationException($"Action '{action.Id}' is disabled");

            if (!action.FromStates.Contains(instance.CurrentStateId))
                throw new ValidationException($"Action '{action.Id}' cannot be executed from current state '{instance.CurrentStateId}'");

            var currentState = definition.States.First(s => s.Id == instance.CurrentStateId);
            if (currentState.IsFinal)
                throw new ValidationException($"Cannot execute actions on final state '{currentState.Id}'");

            return Task.CompletedTask;
        }
    }
}