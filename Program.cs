using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WorkflowEngine.Models;
using WorkflowEngine.Services;
using WorkflowEngine.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IWorkflowDefinitionService, WorkflowDefinitionService>();
builder.Services.AddSingleton<IWorkflowInstanceService, WorkflowInstanceService>();
builder.Services.AddSingleton<IWorkflowValidator, WorkflowValidator>();
builder.Services.AddSingleton<IPersistenceService, FilePersistenceService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

app.MapPost("/api/workflow-definitions", async (
    [FromBody] CreateWorkflowDefinitionRequest request,
    [FromServices] IWorkflowDefinitionService service) =>
{
    try
    {
        var definition = await service.CreateDefinitionAsync(request);
        return Results.Created($"/api/workflow-definitions/{definition.Id}", definition);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/workflow-definitions/{id}", async (
    string id,
    [FromServices] IWorkflowDefinitionService service) =>
{
    var definition = await service.GetDefinitionAsync(id);
    return definition != null ? Results.Ok(definition) : Results.NotFound();
});

app.MapGet("/api/workflow-definitions", async (
    [FromServices] IWorkflowDefinitionService service) =>
{
    var definitions = await service.GetAllDefinitionsAsync();
    return Results.Ok(definitions);
});

// Workflow Instance endpoints
app.MapPost("/api/workflow-instances", async (
    [FromBody] StartWorkflowInstanceRequest request,
    [FromServices] IWorkflowInstanceService service) =>
{
    try
    {
        var instance = await service.StartInstanceAsync(request.DefinitionId);
        return Results.Created($"/api/workflow-instances/{instance.Id}", instance);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/workflow-instances/{id}", async (
    string id,
    [FromServices] IWorkflowInstanceService service) =>
{
    var instance = await service.GetInstanceAsync(id);
    return instance != null ? Results.Ok(instance) : Results.NotFound();
});

app.MapGet("/api/workflow-instances", async (
    [FromServices] IWorkflowInstanceService service) =>
{
    var instances = await service.GetAllInstancesAsync();
    return Results.Ok(instances);
});

app.MapPost("/api/workflow-instances/{id}/execute", async (
    string id,
    [FromBody] ExecuteActionRequest request,
    [FromServices] IWorkflowInstanceService service) =>
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
});

app.Run();

// Models/State.cs
namespace WorkflowEngine.Models
{
    public class State
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsInitial { get; set; }
        public bool IsFinal { get; set; }
        public bool Enabled { get; set; } = true;
        public string? Description { get; set; }
    }
}

// Models/Action.cs
namespace WorkflowEngine.Models
{
    public class WorkflowAction
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public List<string> FromStates { get; set; } = new();
        public string ToState { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}

// Models/WorkflowDefinition.cs
namespace WorkflowEngine.Models
{
    public class WorkflowDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<State> States { get; set; } = new();
        public List<WorkflowAction> Actions { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Description { get; set; }
    }
}

// Models/WorkflowInstance.cs
namespace WorkflowEngine.Models
{
    public class WorkflowInstance
    {
        public string Id { get; set; } = string.Empty;
        public string DefinitionId { get; set; } = string.Empty;
        public string CurrentStateId { get; set; } = string.Empty;
        public List<ActionHistory> History { get; set; } = new();
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public bool IsCompleted => CompletedAt.HasValue;
    }

    public class ActionHistory
    {
        public string ActionId { get; set; } = string.Empty;
        public string FromStateId { get; set; } = string.Empty;
        public string ToStateId { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    }
}

// Models/Requests.cs
namespace WorkflowEngine.Models
{
    public class CreateWorkflowDefinitionRequest
    {
        public string Name { get; set; } = string.Empty;
        public List<State> States { get; set; } = new();
        public List<WorkflowAction> Actions { get; set; } = new();
        public string? Description { get; set; }
    }

    public class StartWorkflowInstanceRequest
    {
        public string DefinitionId { get; set; } = string.Empty;
    }

    public class ExecuteActionRequest
    {
        public string ActionId { get; set; } = string.Empty;
    }
}

// Validation/ValidationException.cs
namespace WorkflowEngine.Validation
{
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }
}

// Validation/IWorkflowValidator.cs
namespace WorkflowEngine.Validation
{
    public interface IWorkflowValidator
    {
        Task ValidateDefinitionAsync(WorkflowDefinition definition);
        Task ValidateActionExecutionAsync(WorkflowInstance instance, WorkflowAction action, WorkflowDefinition definition);
    }
}

// Validation/WorkflowValidator.cs
namespace WorkflowEngine.Validation
{
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

// Services/IPersistenceService.cs
namespace WorkflowEngine.Services
{
    public interface IPersistenceService
    {
        Task SaveDefinitionAsync(WorkflowDefinition definition);
        Task<WorkflowDefinition?> GetDefinitionAsync(string id);
        Task<List<WorkflowDefinition>> GetAllDefinitionsAsync();
        Task SaveInstanceAsync(WorkflowInstance instance);
        Task<WorkflowInstance?> GetInstanceAsync(string id);
        Task<List<WorkflowInstance>> GetAllInstancesAsync();
    }
}

// Services/FilePersistenceService.cs
namespace WorkflowEngine.Services
{
    public class FilePersistenceService : IPersistenceService
    {
        private readonly string _dataDirectory = "data";
        private readonly string _definitionsFile = "definitions.json";
        private readonly string _instancesFile = "instances.json";
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public FilePersistenceService()
        {
            Directory.CreateDirectory(_dataDirectory);
        }

        public async Task SaveDefinitionAsync(WorkflowDefinition definition)
        {
            var definitions = await GetAllDefinitionsAsync();
            definitions.RemoveAll(d => d.Id == definition.Id);
            definitions.Add(definition);
            await SaveDefinitionsAsync(definitions);
        }

        public async Task<WorkflowDefinition?> GetDefinitionAsync(string id)
        {
            var definitions = await GetAllDefinitionsAsync();
            return definitions.FirstOrDefault(d => d.Id == id);
        }

        public async Task<List<WorkflowDefinition>> GetAllDefinitionsAsync()
        {
            var filePath = Path.Combine(_dataDirectory, _definitionsFile);
            if (!File.Exists(filePath))
                return new List<WorkflowDefinition>();

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<List<WorkflowDefinition>>(json, _jsonOptions) ?? new List<WorkflowDefinition>();
        }

        public async Task SaveInstanceAsync(WorkflowInstance instance)
        {
            var instances = await GetAllInstancesAsync();
            instances.RemoveAll(i => i.Id == instance.Id);
            instances.Add(instance);
            await SaveInstancesAsync(instances);
        }

        public async Task<WorkflowInstance?> GetInstanceAsync(string id)
        {
            var instances = await GetAllInstancesAsync();
            return instances.FirstOrDefault(i => i.Id == id);
        }

        public async Task<List<WorkflowInstance>> GetAllInstancesAsync()
        {
            var filePath = Path.Combine(_dataDirectory, _instancesFile);
            if (!File.Exists(filePath))
                return new List<WorkflowInstance>();

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<List<WorkflowInstance>>(json, _jsonOptions) ?? new List<WorkflowInstance>();
        }

        private async Task SaveDefinitionsAsync(List<WorkflowDefinition> definitions)
        {
            var filePath = Path.Combine(_dataDirectory, _definitionsFile);
            var json = JsonSerializer.Serialize(definitions, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        private async Task SaveInstancesAsync(List<WorkflowInstance> instances)
        {
            var filePath = Path.Combine(_dataDirectory, _instancesFile);
            var json = JsonSerializer.Serialize(instances, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }
    }
}

// Services/IWorkflowDefinitionService.cs
namespace WorkflowEngine.Services
{
    public interface IWorkflowDefinitionService
    {
        Task<WorkflowDefinition> CreateDefinitionAsync(CreateWorkflowDefinitionRequest request);
        Task<WorkflowDefinition?> GetDefinitionAsync(string id);
        Task<List<WorkflowDefinition>> GetAllDefinitionsAsync();
    }
}

// Services/WorkflowDefinitionService.cs
namespace WorkflowEngine.Services
{
    public class WorkflowDefinitionService : IWorkflowDefinitionService
    {
        private readonly IPersistenceService _persistence;
        private readonly IWorkflowValidator _validator;

        public WorkflowDefinitionService(IPersistenceService persistence, IWorkflowValidator validator)
        {
            _persistence = persistence;
            _validator = validator;
        }

        public async Task<WorkflowDefinition> CreateDefinitionAsync(CreateWorkflowDefinitionRequest request)
        {
            var definition = new WorkflowDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                States = request.States,
                Actions = request.Actions,
                Description = request.Description
            };

            await _validator.ValidateDefinitionAsync(definition);
            await _persistence.SaveDefinitionAsync(definition);
            return definition;
        }

        public async Task<WorkflowDefinition?> GetDefinitionAsync(string id)
        {
            return await _persistence.GetDefinitionAsync(id);
        }

        public async Task<List<WorkflowDefinition>> GetAllDefinitionsAsync()
        {
            return await _persistence.GetAllDefinitionsAsync();
        }
    }
}

// Services/IWorkflowInstanceService.cs
namespace WorkflowEngine.Services
{
    public interface IWorkflowInstanceService
    {
        Task<WorkflowInstance> StartInstanceAsync(string definitionId);
        Task<WorkflowInstance?> GetInstanceAsync(string id);
        Task<List<WorkflowInstance>> GetAllInstancesAsync();
        Task<WorkflowInstance> ExecuteActionAsync(string instanceId, string actionId);
    }
}

// Services/WorkflowInstanceService.cs
namespace WorkflowEngine.Services
{
    public class WorkflowInstanceService : IWorkflowInstanceService
    {
        private readonly IPersistenceService _persistence;
        private readonly IWorkflowDefinitionService _definitionService;
        private readonly IWorkflowValidator _validator;

        public WorkflowInstanceService(
            IPersistenceService persistence,
            IWorkflowDefinitionService definitionService,
            IWorkflowValidator validator)
        {
            _persistence = persistence;
            _definitionService = definitionService;
            _validator = validator;
        }

        public async Task<WorkflowInstance> StartInstanceAsync(string definitionId)
        {
            var definition = await _definitionService.GetDefinitionAsync(definitionId);
            if (definition == null)
                throw new ValidationException($"Workflow definition '{definitionId}' not found");

            var initialState = definition.States.First(s => s.IsInitial);
            var instance = new WorkflowInstance
            {
                Id = Guid.NewGuid().ToString(),
                DefinitionId = definitionId,
                CurrentStateId = initialState.Id
            };

            await _persistence.SaveInstanceAsync(instance);
            return instance;
        }

        public async Task<WorkflowInstance?> GetInstanceAsync(string id)
        {
            return await _persistence.GetInstanceAsync(id);
        }

        public async Task<List<WorkflowInstance>> GetAllInstancesAsync()
        {
            return await _persistence.GetAllInstancesAsync();
        }

        public async Task<WorkflowInstance> ExecuteActionAsync(string instanceId, string actionId)
        {
            var instance = await _persistence.GetInstanceAsync(instanceId);
            if (instance == null)
                throw new ValidationException($"Workflow instance '{instanceId}' not found");

            var definition = await _definitionService.GetDefinitionAsync(instance.DefinitionId);
            if (definition == null)
                throw new ValidationException($"Workflow definition '{instance.DefinitionId}' not found");

            var action = definition.Actions.FirstOrDefault(a => a.Id == actionId);
            if (action == null)
                throw new ValidationException($"Action '{actionId}' not found in workflow definition");

            await _validator.ValidateActionExecutionAsync(instance, action, definition);

            // Execute the action
            var previousStateId = instance.CurrentStateId;
            instance.CurrentStateId = action.ToState;
            
            instance.History.Add(new ActionHistory
            {
                ActionId = actionId,
                FromStateId = previousStateId,
                ToStateId = action.ToState
            });

            // Check if workflow is completed
            var currentState = definition.States.First(s => s.Id == instance.CurrentStateId);
            if (currentState.IsFinal)
            {
                instance.CompletedAt = DateTime.UtcNow;
            }

            await _persistence.SaveInstanceAsync(instance);
            return instance;
        }
    }
}