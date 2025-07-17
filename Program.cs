// Program.cs
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseHttpsRedirection();

// Workflow Definition endpoints
app.MapPost("/api/workflow-definitions", async ([FromBody] CreateWorkflowDefinitionRequest request, IWorkflowService service) =>
{
    try
    {
        var definition = await service.CreateWorkflowDefinitionAsync(request);
        return Results.Created($"/api/workflow-definitions/{definition.Id}", definition);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/workflow-definitions/{id}", async (string id, IWorkflowService service) =>
{
    var definition = await service.GetWorkflowDefinitionAsync(id);
    return definition != null ? Results.Ok(definition) : Results.NotFound();
});

app.MapGet("/api/workflow-definitions", async (IWorkflowService service) =>
{
    var definitions = await service.GetAllWorkflowDefinitionsAsync();
    return Results.Ok(definitions);
});

// Workflow Instance endpoints
app.MapPost("/api/workflow-instances", async ([FromBody] CreateWorkflowInstanceRequest request, IWorkflowService service) =>
{
    try
    {
        var instance = await service.CreateWorkflowInstanceAsync(request);
        return Results.Created($"/api/workflow-instances/{instance.Id}", instance);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/workflow-instances/{id}", async (string id, IWorkflowService service) =>
{
    var instance = await service.GetWorkflowInstanceAsync(id);
    return instance != null ? Results.Ok(instance) : Results.NotFound();
});

app.MapGet("/api/workflow-instances", async (IWorkflowService service) =>
{
    var instances = await service.GetAllWorkflowInstancesAsync();
    return Results.Ok(instances);
});

app.MapPost("/api/workflow-instances/{id}/execute", async (string id, [FromBody] ExecuteActionRequest request, IWorkflowService service) =>
{
    try
    {
        var instance = await service.ExecuteActionAsync(id, request.ActionId);
        return Results.Ok(instance);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

// Models
public class State
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsInitial { get; set; }
    public bool IsFinal { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Description { get; set; }
}

public class Action
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<string> FromStates { get; set; } = new();
    public string ToState { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class WorkflowDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<State> States { get; set; } = new();
    public List<Action> Actions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
}

public class WorkflowInstance
{
    public string Id { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string CurrentStateId { get; set; } = string.Empty;
    public List<HistoryEntry> History { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class HistoryEntry
{
    public string ActionId { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string FromStateId { get; set; } = string.Empty;
    public string ToStateId { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

// Request/Response DTOs
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

// Services
public interface IWorkflowService
{
    Task<WorkflowDefinition> CreateWorkflowDefinitionAsync(CreateWorkflowDefinitionRequest request);
    Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(string id);
    Task<List<WorkflowDefinition>> GetAllWorkflowDefinitionsAsync();
    Task<WorkflowInstance> CreateWorkflowInstanceAsync(CreateWorkflowInstanceRequest request);
    Task<WorkflowInstance?> GetWorkflowInstanceAsync(string id);
    Task<List<WorkflowInstance>> GetAllWorkflowInstancesAsync();
    Task<WorkflowInstance> ExecuteActionAsync(string instanceId, string actionId);
}

public class WorkflowService : IWorkflowService
{
    private readonly Dictionary<string, WorkflowDefinition> _definitions = new();
    private readonly Dictionary<string, WorkflowInstance> _instances = new();
    private readonly object _lock = new();

    public Task<WorkflowDefinition> CreateWorkflowDefinitionAsync(CreateWorkflowDefinitionRequest request)
    {
        ValidateWorkflowDefinition(request);

        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            States = request.States,
            Actions = request.Actions,
            Description = request.Description
        };

        lock (_lock)
        {
            _definitions[definition.Id] = definition;
        }

        return Task.FromResult(definition);
    }

    public Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(string id)
    {
        lock (_lock)
        {
            _definitions.TryGetValue(id, out var definition);
            return Task.FromResult(definition);
        }
    }

    public Task<List<WorkflowDefinition>> GetAllWorkflowDefinitionsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_definitions.Values.ToList());
        }
    }

    public Task<WorkflowInstance> CreateWorkflowInstanceAsync(CreateWorkflowInstanceRequest request)
    {
        WorkflowDefinition? definition;
        lock (_lock)
        {
            if (!_definitions.TryGetValue(request.DefinitionId, out definition))
            {
                throw new ValidationException($"Workflow definition '{request.DefinitionId}' not found");
            }
        }

        var initialState = definition.States.FirstOrDefault(s => s.IsInitial);
        if (initialState == null)
        {
            throw new ValidationException("Workflow definition must have an initial state");
        }

        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid().ToString(),
            DefinitionId = request.DefinitionId,
            CurrentStateId = initialState.Id
        };

        lock (_lock)
        {
            _instances[instance.Id] = instance;
        }

        return Task.FromResult(instance);
    }

    public Task<WorkflowInstance?> GetWorkflowInstanceAsync(string id)
    {
        lock (_lock)
        {
            _instances.TryGetValue(id, out var instance);
            return Task.FromResult(instance);
        }
    }

    public Task<List<WorkflowInstance>> GetAllWorkflowInstancesAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_instances.Values.ToList());
        }
    }

    public Task<WorkflowInstance> ExecuteActionAsync(string instanceId, string actionId)
    {
        WorkflowInstance? instance;
        WorkflowDefinition? definition;

        lock (_lock)
        {
            if (!_instances.TryGetValue(instanceId, out instance))
            {
                throw new ValidationException($"Workflow instance '{instanceId}' not found");
            }

            if (!_definitions.TryGetValue(instance.DefinitionId, out definition))
            {
                throw new ValidationException($"Workflow definition '{instance.DefinitionId}' not found");
            }
        }

        var action = definition.Actions.FirstOrDefault(a => a.Id == actionId);
        if (action == null)
        {
            throw new ValidationException($"Action '{actionId}' not found in workflow definition");
        }

        if (!action.Enabled)
        {
            throw new InvalidOperationException($"Action '{actionId}' is disabled");
        }

        var currentState = definition.States.FirstOrDefault(s => s.Id == instance.CurrentStateId);
        if (currentState == null)
        {
            throw new ValidationException($"Current state '{instance.CurrentStateId}' not found");
        }

        if (currentState.IsFinal)
        {
            throw new InvalidOperationException("Cannot execute actions on instances in final state");
        }

        if (!action.FromStates.Contains(instance.CurrentStateId))
        {
            throw new InvalidOperationException($"Action '{actionId}' cannot be executed from current state '{instance.CurrentStateId}'");
        }

        var targetState = definition.States.FirstOrDefault(s => s.Id == action.ToState);
        if (targetState == null)
        {
            throw new ValidationException($"Target state '{action.ToState}' not found");
        }

        // Execute the action
        var historyEntry = new HistoryEntry
        {
            ActionId = action.Id,
            ActionName = action.Name,
            FromStateId = instance.CurrentStateId,
            ToStateId = action.ToState
        };

        lock (_lock)
        {
            instance.History.Add(historyEntry);
            instance.CurrentStateId = action.ToState;
            instance.LastUpdated = DateTime.UtcNow;
        }

        return Task.FromResult(instance);
    }

    private static void ValidateWorkflowDefinition(CreateWorkflowDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Workflow name is required");
        }

        if (request.States == null || request.States.Count == 0)
        {
            throw new ValidationException("Workflow must have at least one state");
        }

        if (request.Actions == null)
        {
            throw new ValidationException("Actions collection cannot be null");
        }

        // Check for duplicate state IDs
        var stateIds = request.States.Select(s => s.Id).ToList();
        if (stateIds.Count != stateIds.Distinct().Count())
        {
            throw new ValidationException("Duplicate state IDs found");
        }

        // Check for duplicate action IDs
        var actionIds = request.Actions.Select(a => a.Id).ToList();
        if (actionIds.Count != actionIds.Distinct().Count())
        {
            throw new ValidationException("Duplicate action IDs found");
        }

        // Validate exactly one initial state
        var initialStates = request.States.Where(s => s.IsInitial).ToList();
        if (initialStates.Count != 1)
        {
            throw new ValidationException("Workflow must have exactly one initial state");
        }

        // Validate state references in actions
        var stateIdSet = new HashSet<string>(stateIds);
        foreach (var action in request.Actions)
        {
            if (string.IsNullOrWhiteSpace(action.Id))
            {
                throw new ValidationException("Action ID is required");
            }

            if (string.IsNullOrWhiteSpace(action.ToState))
            {
                throw new ValidationException($"Action '{action.Id}' must have a target state");
            }

            if (!stateIdSet.Contains(action.ToState))
            {
                throw new ValidationException($"Action '{action.Id}' references unknown target state '{action.ToState}'");
            }

            foreach (var fromState in action.FromStates)
            {
                if (!stateIdSet.Contains(fromState))
                {
                    throw new ValidationException($"Action '{action.Id}' references unknown source state '{fromState}'");
                }
            }
        }

        // Validate state IDs and names
        foreach (var state in request.States)
        {
            if (string.IsNullOrWhiteSpace(state.Id))
            {
                throw new ValidationException("State ID is required");
            }

            if (string.IsNullOrWhiteSpace(state.Name))
            {
                throw new ValidationException($"State '{state.Id}' must have a name");
            }
        }
    }
}

// Exceptions
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}