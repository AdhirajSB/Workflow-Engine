using System.Text.Json;
using WorkflowEngine.Models;
using WorkflowEngine.Validation;

namespace WorkflowEngine.Services
{
    // Persistence interfaces and implementation
    public interface IPersistenceService
    {
        Task SaveDefinitionAsync(WorkflowDefinition definition);
        Task<WorkflowDefinition?> GetDefinitionAsync(string id);
        Task<List<WorkflowDefinition>> GetAllDefinitionsAsync();
        Task SaveInstanceAsync(WorkflowInstance instance);
        Task<WorkflowInstance?> GetInstanceAsync(string id);
        Task<List<WorkflowInstance>> GetAllInstancesAsync();
    }

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

    // Workflow Definition Service
    public interface IWorkflowDefinitionService
    {
        Task<WorkflowDefinition> CreateDefinitionAsync(CreateWorkflowDefinitionRequest request);
        Task<WorkflowDefinition?> GetDefinitionAsync(string id);
        Task<List<WorkflowDefinition>> GetAllDefinitionsAsync();
    }

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

    // Workflow Instance Service
    public interface IWorkflowInstanceService
    {
        Task<WorkflowInstance> StartInstanceAsync(string definitionId);
        Task<WorkflowInstance?> GetInstanceAsync(string id);
        Task<List<WorkflowInstance>> GetAllInstancesAsync();
        Task<WorkflowInstance> ExecuteActionAsync(string instanceId, string actionId);
    }

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