using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Webhook.Managers;

namespace Webhook.Controllers;

[Route("api/[controller]")]
public class GithubWebhookController : Controller
{
    private readonly ILogger<GithubWebhookController> logger;
    private readonly string repo;
    private readonly string mapleServer2;
    private readonly string mapleWebServer;
    private readonly IGithubWebhookManager igithubWebhookManager;

    public GithubWebhookController(ILogger<GithubWebhookController> logger, IConfiguration configuration, IGithubWebhookManager igithubWebhookManager)
    {
        this.logger = logger;
        repo = configuration["Repo"];
        mapleServer2 = configuration["MapleServer2"];
        mapleWebServer = configuration["MapleWebServer"];
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

        commands.Add("cd " + repo + " && git fetch upstream");
        commands.Add("git pull upstream master");
        commands.Add("dotnet run --project gamedataparser/gamedataparser.csproj");
        commands.Add("cd " + mapleServer2 + " && dotnet build --configuration Release");
        commands.Add("cd " + mapleWebServer + " && dotnet build --configuration Release");
        commands.Add("start " + mapleServer2 + "\\bin\\Release\\net6.0\\MapleServer2.exe");
        commands.Add("start " + mapleWebServer + "\\bin\\Release\\net6.0\\mapleWebServer.exe");
        igithubWebhookManager.RunCommands(commands);
        return Ok("works with configured secret!");
    }
}