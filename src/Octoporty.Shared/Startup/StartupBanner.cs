// StartupBanner.cs
// Displays ASCII art banner, version, and configuration at startup.
// Obfuscates secret values showing only first 2 and last 2 characters.

using System.Reflection;
using System.Text;

namespace Octoporty.Shared.Startup;

public static class StartupBanner
{
    private const string AsciiArt = @"
   ██████╗  ██████╗████████╗ ██████╗ ██████╗  ██████╗ ██████╗ ████████╗██╗   ██╗
  ██╔═══██╗██╔════╝╚══██╔══╝██╔═══██╗██╔══██╗██╔═══██╗██╔══██╗╚══██╔══╝╚██╗ ██╔╝
  ██║   ██║██║        ██║   ██║   ██║██████╔╝██║   ██║██████╔╝   ██║    ╚████╔╝
  ██║   ██║██║        ██║   ██║   ██║██╔═══╝ ██║   ██║██╔══██╗   ██║     ╚██╔╝
  ╚██████╔╝╚██████╗   ██║   ╚██████╔╝██║     ╚██████╔╝██║  ██║   ██║      ██║
   ╚═════╝  ╚═════╝   ╚═╝    ╚═════╝ ╚═╝      ╚═════╝ ╚═╝  ╚═╝   ╚═╝      ╚═╝
";

    /// <summary>
    /// Prints the startup banner with ASCII art, version, and configuration.
    /// </summary>
    /// <param name="serviceName">Name of the service (e.g., "Gateway" or "Agent")</param>
    /// <param name="configValues">Dictionary of config key-value pairs to display</param>
    /// <param name="secretKeys">Keys that should be obfuscated (case-insensitive partial match)</param>
    public static void Print(
        string serviceName,
        Dictionary<string, string?> configValues,
        string[]? secretKeys = null)
    {
        var version = GetVersion();
        secretKeys ??= ["ApiKey", "Secret", "Password", "Token", "Key"];

        Console.WriteLine(AsciiArt);
        Console.WriteLine($"          {serviceName} v{version}");
        Console.WriteLine("          https://octoporty.com");
        Console.WriteLine();
        Console.WriteLine("  ════════════════════════════════════════════════════════════════");
        Console.WriteLine();

        foreach (var (key, value) in configValues)
        {
            var displayValue = value ?? "(not set)";

            // Check if this key should be obfuscated
            var shouldObfuscate = secretKeys.Any(sk =>
                key.Contains(sk, StringComparison.OrdinalIgnoreCase));

            if (shouldObfuscate && !string.IsNullOrEmpty(value))
            {
                displayValue = ObfuscateSecret(value);
            }

            Console.WriteLine($"  {key}: {displayValue}");
        }

        Console.WriteLine();
        Console.WriteLine("  ════════════════════════════════════════════════════════════════");
        Console.WriteLine();
    }

    /// <summary>
    /// Obfuscates a secret value, showing only first 2 and last 2 characters.
    /// Example: "mysupersecretkey123" becomes "my***************23"
    /// </summary>
    public static string ObfuscateSecret(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "(not set)";

        if (value.Length <= 4)
            return new string('*', value.Length);

        var firstTwo = value[..2];
        var lastTwo = value[^2..];
        var middleLength = value.Length - 4;
        var middle = new string('*', middleLength);

        return $"{firstTwo}{middle}{lastTwo}";
    }

    /// <summary>
    /// Gets the assembly version or "dev" if not available.
    /// </summary>
    public static string GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly?.GetName().Version?.ToString()
                      ?? "dev";

        // Strip git hash suffix if present (e.g., "1.0.0+abc123" -> "1.0.0")
        var plusIndex = version.IndexOf('+');
        if (plusIndex > 0)
            version = version[..plusIndex];

        return version;
    }
}
