using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using MySql.Data.MySqlClient;
using Webhook.Managers;

namespace Webhook.Controllers;

[Route("api/[controller]")]
public class GithubWebhookController : Controller
{
    private readonly ILogger<GithubWebhookController> logger;
    private readonly IGithubWebhookManager igithubWebhookManager;
    private readonly IConfiguration configuration;

    public GithubWebhookController(ILogger<GithubWebhookController> logger, IConfiguration configuration, IGithubWebhookManager igithubWebhookManager)
    {
        this.logger = logger;
        this.configuration = configuration;

        this.igithubWebhookManager = igithubWebhookManager;
    }

    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        Request.Headers.TryGetValue("X-GitHub-Delivery", out StringValues gitHubDeliveryId);
        Request.Headers.TryGetValue("X-GitHub-Event", out StringValues gitHubEvent);
        Request.Headers.TryGetValue("X-Hub-Signature", out StringValues gitHubSignature);

        logger.LogInformation("Received GitHub delivery {GitHubDeliveryId} for event {GitHubEvent}", gitHubDeliveryId, gitHubEvent);

        if (gitHubDeliveryId.Count == 0 || gitHubEvent.Count == 0 || gitHubSignature.Count == 0)
        {
            return NotFound();
        }

        using StreamReader reader = new(Request.Body);

        string txt = await reader.ReadToEndAsync();

        if (!igithubWebhookManager.IsGitHubSignatureValid(txt, gitHubEvent, gitHubSignature))
        {
            return Unauthorized();
        }

        List<string> commands = new();

        if (Process.GetProcessesByName("Mapleserver2").Length > 0)
        {
            commands.Add("taskkill /f /im mapleserver2.exe");
        }

        if (Process.GetProcessesByName("MapleWebServer").Length > 0)
        {
            commands.Add("taskkill /f /im maplewebserver.exe");
        }

        string repo = configuration["Repo"];
        string mapleServer2 = configuration["MapleServer2"];
        string mapleWebServer = configuration["MapleWebServer"];
        string dbUser = configuration["dbUser"];
        string dbPass = configuration["dbPass"];
        string dbName = configuration["dbName"];

        commands.Add("cd " + repo + " && git fetch upstream");
        commands.Add("git pull upstream master");

        if (igithubWebhookManager.CheckDb())
        {
            MySqlConnection conn = new($"SERVER=localhost;PORT=3306;USER={dbUser};PASSWORD={dbPass};DATABASE={dbName};");
            await new MySqlScript(conn, "DROP DATABASE IF EXISTS " + dbName).ExecuteAsync();
        }

        commands.Add("dotnet run --project gamedataparser/gamedataparser.csproj");
        commands.Add("cd " + mapleServer2 + " && dotnet build --configuration Release");
        commands.Add("cd " + mapleWebServer + " && dotnet build --configuration Release");
        commands.Add("start " + mapleServer2 + "\\bin\\Release\\net6.0\\MapleServer2.exe");
        commands.Add("start " + mapleWebServer + "\\bin\\Release\\net6.0\\mapleWebServer.exe");
        igithubWebhookManager.RunCommands(commands);
        return Ok("works with configured secret!");
    }
}