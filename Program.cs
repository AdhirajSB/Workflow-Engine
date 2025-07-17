using WorkflowEngine.Core;
using WorkflowEngine.Common;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Create one instance of the workflow service to use across routes
var workflowService = new WorkflowService();

app.MapPost("/workflows", async (CreateWorkflowDefinitionRequest req) =>
{
    try
    {
        var def = await workflowService.CreateWorkflowDefinitionAsync(req);
        return Results.Ok(def);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/workflows", async () =>
{
    var defs = await workflowService.GetAllWorkflowDefinitionsAsync();
    return Results.Ok(defs);
});

app.MapPost("/instances", async (CreateWorkflowInstanceRequest req) =>
{
    try
    {
        var instance = await workflowService.CreateWorkflowInstanceAsync(req);
        return Results.Ok(instance);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/instances/{id}/execute", async (string id, ExecuteActionRequest req) =>
{
    try
    {
        var updated = await workflowService.ExecuteActionAsync(id, req.ActionId);
        return Results.Ok(updated);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/instances", async () =>
{
    var instances = await workflowService.GetAllWorkflowInstancesAsync();
    return Results.Ok(instances);
});

app.Run();
