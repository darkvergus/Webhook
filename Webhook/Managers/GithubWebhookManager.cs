using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Webhook.Managers;

public class GithubWebhookManager : IGithubWebhookManager
{
    private const string Sha1Prefix = "sha1=";
    private readonly IConfiguration configuration;

    public GithubWebhookManager(IConfiguration configuration) => this.configuration = configuration;

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

        string signature = signatureWithPrefix[Sha1Prefix.Length..];
        byte[] secret = Encoding.ASCII.GetBytes(configuration["GitHubWebhookSecret"]);
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

    public bool CheckDb()
    {
        bool dropDb = false;
        string sqlpath = configuration["MapleServer2"] + "\\Database";
        string[] files = Directory.GetFileSystemEntries(sqlpath, "*.sql", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            if (HasValidHash(file))
            {
                Console.WriteLine($"\rSkipping {file}");
                continue;
            }

            dropDb = true;
            WriteHash(file);
        }

        return dropDb;
    }

    private bool HasValidHash(string file)
    {
        string fileName = Path.GetFileName(file);
        string hashPath = AppContext.BaseDirectory + $"Hashes/{fileName}-hash";

        if (!File.Exists(hashPath))
        {
            return false;
        }

        string currentHash = File.ReadAllText(hashPath);
        string newHash = GetHash(file);

        return currentHash.Equals(newHash);
    }

    private void WriteHash(string file)
    {
        string fileName = Path.GetFileName(file);
        string hashPath = AppContext.BaseDirectory + $"Hashes/{fileName}-hash";

        string newHash = GetHash(file);

        File.WriteAllText(hashPath, newHash);
    }

    private string GetHash(string file)
    {
        if (!File.Exists(file))
        {
            return string.Empty;
        }

        using MD5 md5 = MD5.Create();
        using FileStream stream = File.OpenRead(file);

        byte[] hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}