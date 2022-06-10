using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Webhook.Managers;

public class GithubWebhookManager : IGithubWebhookManager
{
    private const string Sha1Prefix = "sha1=";
    private readonly string githubWebhookSecret;

    public GithubWebhookManager(IConfiguration configuration) => githubWebhookSecret = configuration["GitHubWebhookSecret"];

    public bool IsGitHubSignatureValid(string payload, string eventName, string signatureWithPrefix)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentNullException(nameof(eventName));
        }

        if (string.IsNullOrWhiteSpace(signatureWithPrefix))
        {
            throw new ArgumentNullException(nameof(signatureWithPrefix));
        }

        if (!signatureWithPrefix.StartsWith(Sha1Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!eventName.Equals("push", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string signature = signatureWithPrefix.Substring(Sha1Prefix.Length);
        byte[] secret = Encoding.ASCII.GetBytes(githubWebhookSecret);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        using HMACSHA1 hmSha1 = new(secret);
        byte[] hash = hmSha1.ComputeHash(payloadBytes);

        string hashString = ToHexString(hash);

        return hashString.Equals(signature);
    }

    private static string ToHexString(IReadOnlyCollection<byte> bytes)
    {
        StringBuilder builder = new(bytes.Count * 2);
        foreach (byte b in bytes)
        {
            builder.Append($"{b:x2}");
        }

        return builder.ToString();
    }

    public void RunCommands(List<string> cmds, string workingDirectory = "")
    {
        Process process = new();
        ProcessStartInfo startInfo = new()
        {
            FileName = "cmd.exe",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        };
        process.StartInfo = startInfo;
        process.Start();
        process.OutputDataReceived += (sender, e) => { Console.WriteLine(e.Data); };
        process.ErrorDataReceived += (sender, e) => { Console.WriteLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using (StreamWriter sw = process.StandardInput)
        {
            foreach (string cmd in cmds)
            {
                sw.WriteLine(cmd);
            }
        }

        process.WaitForExit();
    }
}