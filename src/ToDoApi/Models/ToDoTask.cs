using Amazon.DynamoDBv2.DataModel;


namespace todo_serverless.Models;


[DynamoDBTable("Tasks")]
public class TodoTask
{
    // Primary key i DynamoDB - genererar automatiskt nytt GUID
    [DynamoDBHashKey] public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
}