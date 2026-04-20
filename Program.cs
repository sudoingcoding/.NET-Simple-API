var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Middleware pipeline
app.UseMiddleware<LoggingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<AuthenticationMiddleware>();

// In-memory storage for users
var users = new List<User>();
var nextId = 1;

// Helper method to validate user input
bool IsValidUser(User user, out string errorMessage)
{
    if (string.IsNullOrWhiteSpace(user.Name))
    {
        errorMessage = "Name is required.";
        return false;
    }
    if (string.IsNullOrWhiteSpace(user.Email))
    {
        errorMessage = "Email is required.";
        return false;
    }
    if (!System.Text.RegularExpressions.Regex.IsMatch(user.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
    {
        errorMessage = "Invalid email format.";
        return false;
    }
    if (string.IsNullOrWhiteSpace(user.Department))
    {
        errorMessage = "Department is required.";
        return false;
    }
    errorMessage = string.Empty;
    return true;
}

// GET: Retrieve all users
app.MapGet("/api/users", () =>
{
    try
    {
        return Results.Ok(users);
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while retrieving users.", statusCode: 500);
    }
})
.WithName("GetUsers");

// GET: Retrieve a specific user by ID
app.MapGet("/api/users/{id}", (int id) =>
{
    try
    {
        var user = users.FirstOrDefault(u => u.Id == id);
        return user is not null ? Results.Ok(user) : Results.NotFound();
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while retrieving the user.", statusCode: 500);
    }
})
.WithName("GetUser");

// POST: Add a new user
app.MapPost("/api/users", (User user) =>
{
    try
    {
        if (!IsValidUser(user, out string errorMessage))
        {
            return Results.BadRequest(errorMessage);
        }
        if (users.Any(u => u.Email == user.Email))
        {
            return Results.BadRequest("A user with this email already exists.");
        }
        user.Id = nextId++;
        users.Add(user);
        return Results.Created($"/api/users/{user.Id}", user);
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while creating the user.", statusCode: 500);
    }
})
.WithName("CreateUser");

// PUT: Update an existing user
app.MapPut("/api/users/{id}", (int id, User updatedUser) =>
{
    try
    {
        if (!IsValidUser(updatedUser, out string errorMessage))
        {
            return Results.BadRequest(errorMessage);
        }
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null) return Results.NotFound();

        // Check if email is being changed and if it's already taken by another user
        if (user.Email != updatedUser.Email && users.Any(u => u.Email == updatedUser.Email))
        {
            return Results.BadRequest("A user with this email already exists.");
        }

        user.Name = updatedUser.Name;
        user.Email = updatedUser.Email;
        user.Department = updatedUser.Department;
        return Results.NoContent();
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while updating the user.", statusCode: 500);
    }
})
.WithName("UpdateUser");

// DELETE: Remove a user by ID
app.MapDelete("/api/users/{id}", (int id) =>
{
    try
    {
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null) return Results.NotFound();

        users.Remove(user);
        return Results.NoContent();
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while deleting the user.", statusCode: 500);
    }
})
.WithName("DeleteUser");

app.Run();

class User
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Department { get; set; }
}

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    public ErrorHandlingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Internal server error.\"}");
        }
    }
}

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    public AuthenticationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token) || token != "valid-token")
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Unauthorized\"}");
            return;
        }
        await _next(context);
    }
}

public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    public LoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");
        await _next(context);
        Console.WriteLine($"Response: {context.Response.StatusCode}");
    }
}
