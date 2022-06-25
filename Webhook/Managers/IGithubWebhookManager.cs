namespace Webhook.Managers;

public interface IGithubWebhookManager
{
    bool IsGitHubSignatureValid(string payload, string eventName, string signatureWithPrefix);
    void RunCommands(List<string> cmds, string workingDirectory = "");
    bool CheckDb();
}