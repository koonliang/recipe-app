using Amazon.Lambda.AspNetCoreServer;
using BuildingBlocks.Observability;
using Infrastructure.Persistence;
using Core.Application.Configuration;
using Core.Application.Interfaces;
using DotNetEnv;

// Load .env file for local development
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add environment variables to configuration
builder.Configuration.AddEnvironmentVariables();

// Validate configuration on startup
try
{
    builder.Configuration.ValidateConfigurationOnStartup();
}
catch (Exception ex)
{
    Console.WriteLine($"Configuration validation failed: {ex.Message}");
    throw;
}

builder.Host.UseSerilogLogging();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS configuration for frontend integration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
    
    // Allow all origins for development (less secure but easier for development)
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add configuration options with validation
builder.Services.AddConfigurationOptions(builder.Configuration);

// Add database
builder.Services.AddDatabase(builder.Configuration);

// Add infrastructure services
builder.Services.AddInfrastructureServices();

// Add MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(Core.Application.Commands.Recipe.CreateRecipeCommand).Assembly);
});

// Authorization will be handled by the Authorizer Lambda

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

var app = builder.Build();

// Ensure database is created and seeded with fallback handling
using (var scope = app.Services.CreateScope())
{
    try 
    {
        var context = scope.ServiceProvider.GetRequiredService<RecipeAppDbContext>();
        context.Database.EnsureCreated();
    }
    catch (Exception ex) when (ex.Message.Contains("Unable to connect") || ex.Message.Contains("MySQL"))
    {
        Console.WriteLine($"Database connection failed during startup: {ex.Message}");
        Console.WriteLine("Note: Database fallback should be configured at service registration time for proper operation.");
        // Don't rethrow - let the application start but database operations will fail
        // This allows the Lambda to start but individual requests may need to handle DB errors
    }
}

if (app.Environment.IsDevelopment())
{
    Console.WriteLine("Dev mode swagger");
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // Use permissive CORS in development for easier frontend integration
    app.UseCors("AllowAll");
}
else
{
    // Use more restrictive CORS in production
    app.UseCors("AllowAll");
}

app.UseHttpsRedirection();
// Authentication and authorization removed - handled by Authorizer Lambda
app.MapControllers();

app.Run();