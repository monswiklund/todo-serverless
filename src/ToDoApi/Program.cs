﻿using System.Threading.RateLimiting;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.OpenApi;
using Serilog;
using todo_serverless.Models;
using todo_serverless.Services;

var builder = WebApplication.CreateBuilder(args);

// Konfigurera Serilog för console logging
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});




// Rate limiting - 100 requests per minut per IP
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 100,
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Rate limit exceeded. Please try again later.",
            retryAfter = "60 seconds"
        }, cancellationToken: token);
    };
});

// AWS services setup
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddSingleton<IDynamoDBContext>(provider =>
{
    var client = provider.GetRequiredService<IAmazonDynamoDB>();
    var config = new DynamoDBContextConfig
    {
        // Fick problem med att containers kraschade pga DescribeTable-anrop så skippar metadata loading
        DisableFetchingTableMetadata = true
    };
    return new DynamoDBContext(client, config);
});

builder.Services.AddScoped<TaskService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Serilog request logging
app.UseSerilogRequestLogging();

// Aktivera rate limiting
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(options => { options.SwaggerEndpoint("/openapi/v1.json", "v1"); });

// Serve static files från wwwroot
app.UseStaticFiles();

// Serve startpage istället för redirect
app.MapGet("/", () => Results.File("~/index.html", "text/html"));

// Hämta alla todos med optional pagination
app.MapGet("/todos", async (TaskService taskService, int? limit) =>
    Results.Ok(await taskService.GetAllAsync(limit ?? 100))
);

// Hämta en specifik todo
app.MapGet("/todos/{id}", async (string id, TaskService service) =>
{
    var todo = await service.GetByIdAsync(id);
    return todo != null ? Results.Ok(todo) : Results.NotFound();
});

// Skapa ny todo
app.MapPost("/todos", async (TodoTask newTask, TaskService service) =>
{
    // Validera input
    if (string.IsNullOrWhiteSpace(newTask.Title))
        return Results.BadRequest(new { error = "Title is required and cannot be empty" });

    if (newTask.Title.Length > 200)
        return Results.BadRequest(new { error = "Title cannot exceed 200 characters" });

    newTask.Id = Guid.NewGuid().ToString(); // Genererar nytt ID
    await service.CreateAsync(newTask);
    return Results.Created($"/todos/{newTask.Id}", newTask);
});

// Uppdatera todo
app.MapPut("/todos/{id}", async (string id, TodoTask updatedTask, TaskService service) =>
{
    // Validera input
    if (string.IsNullOrWhiteSpace(updatedTask.Title))
        return Results.BadRequest(new { error = "Title is required and cannot be empty" });

    if (updatedTask.Title.Length > 200)
        return Results.BadRequest(new { error = "Title cannot exceed 200 characters" });

    // Verifiera att task existerar
    var existing = await service.GetByIdAsync(id);
    if (existing == null)
        return Results.NotFound(new { error = $"Task with ID '{id}' not found" });

    updatedTask.Id = id; // Använder ID från URL
    await service.UpdateAsync(updatedTask);
    return Results.Ok(updatedTask);
});

// Ta bort todo
app.MapDelete("/todos/{id}", async (string id, TaskService service) =>
{
    await service.DeleteAsync(id);
    return Results.NoContent();
});

// Health check endpoint för ALB med Docker Swarm status
app.MapGet("/health", async () =>
{
    try
    {
        var healthData = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "3.0",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            aws_region = Environment.GetEnvironmentVariable("AWS_REGION"),
            hostname = Environment.MachineName
        };

        return Results.Ok(healthData);
    }
    catch (Exception ex)
    {
        var errorData = new
        {
            status = "unhealthy",
            timestamp = DateTime.UtcNow,
            error = ex.Message,
            version = "3.0"
        };

        return Results.Json(errorData, statusCode: 503);
    }
});

// Explicit IPv4 binding för ALB health checks 
app.Run();

// Lambda hosting support
public partial class Program { }