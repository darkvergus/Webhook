using Webhook.Managers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ApplicationName = typeof(Program).Assembly.FullName,
    ContentRootPath = Directory.GetCurrentDirectory(),
    EnvironmentName = Environments.Development
});

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<IGithubWebhookManager, GithubWebhookManager>();

WebApplication app = builder.Build();

app.Urls.Add("http://*:8888");

app.UseAuthorization();

app.MapControllers();

app.Run();