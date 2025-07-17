using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WorkflowEngine.Models;
using WorkflowEngine.Services;
using WorkflowEngine.Validation;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<IWorkflowDefinitionService, WorkflowDefinitionService>();
builder.Services.AddSingleton<IWorkflowInstanceService, WorkflowInstanceService>();
builder.Services.AddSingleton<IWorkflowValidator, WorkflowValidator>();
builder.Services.AddSingleton<IPersistenceService, FilePersistenceService>();

// Configure JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

// Workflow Definition endpoints
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