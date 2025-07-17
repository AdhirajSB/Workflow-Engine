namespace WorkflowEngine.Models
{
    // Core workflow entities
    public class State
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsInitial { get; set; }
        public bool IsFinal { get; set; }
        public bool Enabled { get; set; } = true;
        public string? Description { get; set; }
    }

    public class WorkflowAction
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
        public List<WorkflowAction> Actions { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Description { get; set; }
    }

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

    // Request/Response DTOs
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