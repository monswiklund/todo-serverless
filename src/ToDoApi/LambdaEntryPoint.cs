using Amazon.Lambda.AspNetCoreServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Serilog;
using Microsoft.AspNetCore.Http;
using todo_serverless.Models;
using todo_serverless.Services;

namespace todo_serverless;

public class LambdaEntryPoint : APIGatewayHttpApiV2ProxyFunction
{
    protected override void Init(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices((context, services) =>
            {
                // Serilog
                var logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .CreateLogger();
                services.AddSingleton<Serilog.ILogger>(logger);

                // AWS DynamoDB
                services.AddAWSService<IAmazonDynamoDB>();
                services.AddSingleton<IDynamoDBContext>(provider =>
                {
                    var client = provider.GetRequiredService<IAmazonDynamoDB>();
                    var config = new DynamoDBContextConfig
                    {
                        DisableFetchingTableMetadata = true
                    };
                    return new DynamoDBContext(client, config);
                });

                services.AddScoped<TaskService>();
                services.AddEndpointsApiExplorer();
                services.AddSwaggerGen();

                // CORS för att tillåta frontend att anropa API:t
                services.AddCors(options =>
                {
                    options.AddDefaultPolicy(builder =>
                    {
                        builder.AllowAnyOrigin()
                               .AllowAnyMethod()
                               .AllowAnyHeader();
                    });
                });
            })
            .Configure(app =>
            {
                // CORS måste vara före routing
                app.UseCors();

                // Swagger
                app.UseSwagger();

                // Root endpoint
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/", async context =>
                    {
                        await context.Response.WriteAsJsonAsync(new
                        {
                            api = "TODO Serverless API",
                            version = "1.0",
                            endpoints = new
                            {
                                todos = "/todos",
                                swagger = "/swagger/v1/swagger.json",
                                health = "/health"
                            }
                        });
                    });

                    // CRUD endpoints
                    endpoints.MapGet("/todos", async context =>
                    {
                        var service = context.RequestServices.GetRequiredService<TaskService>();
                        var todos = await service.GetAllAsync(100);
                        await context.Response.WriteAsJsonAsync(todos);
                    });

                    endpoints.MapGet("/todos/{id}", async context =>
                    {
                        var id = context.Request.RouteValues["id"]?.ToString();
                        var service = context.RequestServices.GetRequiredService<TaskService>();
                        var todo = await service.GetByIdAsync(id!);

                        if (todo != null)
                            await context.Response.WriteAsJsonAsync(todo);
                        else
                        {
                            context.Response.StatusCode = 404;
                            await context.Response.WriteAsJsonAsync(new { error = "Not found" });
                        }
                    });

                    endpoints.MapPost("/todos", async context =>
                    {
                        var service = context.RequestServices.GetRequiredService<TaskService>();
                        var newTask = await context.Request.ReadFromJsonAsync<TodoTask>();

                        if (string.IsNullOrWhiteSpace(newTask?.Title))
                        {
                            context.Response.StatusCode = 400;
                            await context.Response.WriteAsJsonAsync(new { error = "Title is required" });
                            return;
                        }

                        newTask.Id = Guid.NewGuid().ToString();
                        await service.CreateAsync(newTask);

                        context.Response.StatusCode = 201;
                        await context.Response.WriteAsJsonAsync(newTask);
                    });

                    endpoints.MapPut("/todos/{id}", async context =>
                    {
                        var id = context.Request.RouteValues["id"]?.ToString();
                        var service = context.RequestServices.GetRequiredService<TaskService>();
                        var updatedTask = await context.Request.ReadFromJsonAsync<TodoTask>();

                        if (string.IsNullOrWhiteSpace(updatedTask?.Title))
                        {
                            context.Response.StatusCode = 400;
                            await context.Response.WriteAsJsonAsync(new { error = "Title is required" });
                            return;
                        }

                        var existing = await service.GetByIdAsync(id!);
                        if (existing == null)
                        {
                            context.Response.StatusCode = 404;
                            await context.Response.WriteAsJsonAsync(new { error = "Not found" });
                            return;
                        }

                        updatedTask.Id = id!;
                        await service.UpdateAsync(updatedTask);
                        await context.Response.WriteAsJsonAsync(updatedTask);
                    });

                    endpoints.MapDelete("/todos/{id}", async context =>
                    {
                        var id = context.Request.RouteValues["id"]?.ToString();
                        var service = context.RequestServices.GetRequiredService<TaskService>();
                        await service.DeleteAsync(id!);
                        context.Response.StatusCode = 204;
                    });

                    endpoints.MapGet("/health", async context =>
                    {
                        await context.Response.WriteAsJsonAsync(new
                        {
                            status = "healthy",
                            timestamp = DateTime.UtcNow,
                            version = "1.0"
                        });
                    });
                });
            });
    }
}