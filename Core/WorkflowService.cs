using WorkflowEngine.Core;
using WorkflowEngine.Common;

namespace WorkflowEngine.Core;

// Request DTOs (keep them close to logic for now)
public class CreateWorkflowDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public List<State> States { get; set; } = new();
    public List<Action> Actions { get; set; } = new();
    public string? Description { get; set; }
}

public class CreateWorkflowInstanceRequest
{
    public string DefinitionId { get; set; } = string.Empty;
}

public class ExecuteActionRequest
{
    public string ActionId { get; set; } = string.Empty;
}

// Core service class
public class WorkflowService : IWorkflowService
{
    private readonly Dictionary<string, WorkflowDefinition> _definitions = new();
    private readonly Dictionary<string, WorkflowInstance> _instances = new();

    public Task<WorkflowDefinition> CreateWorkflowDefinitionAsync(CreateWorkflowDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Workflow name cannot be empty.");

        if (!request.States.Any(s => s.IsInitial))
            throw new ValidationException("At least one initial state is required.");

        var definition = new WorkflowDefinition
        {
            Name = request.Name,
            Description = request.Description,
            States = request.States,
            Actions = request.Actions
        };

        _definitions[definition.Id] = definition;
        return Task.FromResult(definition);
    }

    public Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(string id)
    {
        _definitions.TryGetValue(id, out var def);
        return Task.FromResult(def);
    }

    public Task<IEnumerable<WorkflowDefinition>> GetAllWorkflowDefinitionsAsync()
    {
        return Task.FromResult(_definitions.Values.AsEnumerable());
    }

    public Task<WorkflowInstance> CreateWorkflowInstanceAsync(CreateWorkflowInstanceRequest request)
    {
        if (!_definitions.TryGetValue(request.DefinitionId, out var def))
            throw new ValidationException("Definition not found.");

        var initialState = def.States.FirstOrDefault(s => s.IsInitial);
        if (initialState == null)
            throw new ValidationException("No initial state defined.");

        var instance = new WorkflowInstance
        {
            DefinitionId = def.Id,
            CurrentStateId = initialState.Id
        };

        _instances[instance.Id] = instance;
        return Task.FromResult(instance);
    }

    public Task<WorkflowInstance?> GetWorkflowInstanceAsync(string id)
    {
        _instances.TryGetValue(id, out var instance);
        return Task.FromResult(instance);
    }

    public Task<IEnumerable<WorkflowInstance>> GetAllWorkflowInstancesAsync()
    {
        return Task.FromResult(_instances.Values.AsEnumerable());
    }

    public Task<WorkflowInstance> ExecuteActionAsync(string instanceId, string actionId)
    {
        if (!_instances.TryGetValue(instanceId, out var instance))
            throw new ValidationException("Workflow instance not found.");

        if (!_definitions.TryGetValue(instance.DefinitionId, out var definition))
            throw new ValidationException("Workflow definition not found.");

        var currentState = definition.States.FirstOrDefault(s => s.Id == instance.CurrentStateId)
            ?? throw new ValidationException("Current state not found in definition.");

        var action = definition.Actions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new ValidationException("Action not found in definition.");

        if (!action.FromStates.Contains(currentState.Id))
            throw new InvalidOperationException($"Action '{action.Name}' is not valid from current state '{currentState.Name}'.");

        var toState = definition.States.FirstOrDefault(s => s.Id == action.ToState)
            ?? throw new ValidationException("Target state not found.");

        instance.CurrentStateId = toState.Id;
        instance.LastUpdated = DateTime.UtcNow;
        instance.History.Add(new HistoryEntry
        {
            ActionId = action.Id,
            ActionName = action.Name,
            FromStateId = currentState.Id,
            ToStateId = toState.Id,
            ExecutedAt = DateTime.UtcNow
        });

        return Task.FromResult(instance);
    }
}
