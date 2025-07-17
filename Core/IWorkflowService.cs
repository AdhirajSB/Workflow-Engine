namespace WorkflowEngine.Core;

public interface IWorkflowService
{
    Task<WorkflowDefinition> CreateWorkflowDefinitionAsync(CreateWorkflowDefinitionRequest request);
    Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(string id);
    Task<IEnumerable<WorkflowDefinition>> GetAllWorkflowDefinitionsAsync();

    Task<WorkflowInstance> CreateWorkflowInstanceAsync(CreateWorkflowInstanceRequest request);
    Task<WorkflowInstance?> GetWorkflowInstanceAsync(string id);
    Task<IEnumerable<WorkflowInstance>> GetAllWorkflowInstancesAsync();

    Task<WorkflowInstance> ExecuteActionAsync(string instanceId, string actionId);
}
