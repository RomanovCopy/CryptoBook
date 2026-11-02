using Autofac;
using CryptoBook;
using CryptoBook.Help;
using CryptoBook.Validation;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using System.Text.Json;

internal static partial class Program
{
    private static async Task ValidateStage12(IsolatedServices isolated)
    {
        string bundleRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR")
            ?? throw new InvalidOperationException("Package extraction root is required."));
        string assemblyPath = typeof(App).Assembly.Location;
        Check(Assembly.GetEntryAssembly() == typeof(App).Assembly, "The launched entry assembly is the packaged CryptoBook application.");
        Check(Path.GetFullPath(assemblyPath).StartsWith(bundleRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "CryptoBook.dll comes from this single-file executable's isolated extraction, not the validation host.");
        Check(!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "docs")) &&
            !Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Help")), "The portable application has no adjacent source-help directory.");
        Check(!Directory.Exists(Path.Combine(Environment.CurrentDirectory, "docs")), "The working directory has no source-help directory.");

        var manifest = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(repo, "expected-help.json")))
            ?? throw new InvalidDataException("Missing expected resource hashes.");
        using var stream = typeof(App).Assembly.GetManifestResourceStream("CryptoBook.g.resources")
            ?? throw new InvalidDataException("Missing WPF resources.");
        using var reader = new ResourceReader(stream);
        string[] embedded = reader.Cast<DictionaryEntry>().Select(e => (string)e.Key)
            .Where(key => key.StartsWith("help/", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        Check(embedded.SequenceEqual(manifest.Keys.Select(key => "help/" + key.ToLowerInvariant()).Order(StringComparer.Ordinal)),
            "The package embeds exactly the expected help files, without raw captures or validation evidence.");
        var source = new ResourceHelpContentSource();
        foreach (var entry in manifest)
        {
            using var resource = source.Open(entry.Key);
            Check(Convert.ToHexString(SHA256.HashData(resource)) == entry.Value,
                "Packaged resource matches the reviewed source: " + entry.Key);
        }
        var content = isolated.Container.Resolve<HelpContentService>();
        foreach (string selectedCulture in content.Cultures)
        {
            var locale = content.GetLocale(selectedCulture);
            Check(locale.Culture == selectedCulture && locale.Topics.Count == 76, "Complete explicit locale without fallback: " + selectedCulture);
            var topic = locale.Topics.Single(t => t.Id == "help.use");
            Check(content.Search(selectedCulture, topic.Title).Any(result => result.Id == topic.Id), "Localized title search: " + selectedCulture);
            Check(content.Search(selectedCulture, "ORBIT-731-no-such-help").Count == 0, "No-result search: " + selectedCulture);
        }
        await ValidateStage11(isolated, input: false, verify: true, evidenceRoot: "evidence/");
        WriteJson("evidence/package-" + culture + ".json", new
        {
            culture, commit, entryAssembly = Assembly.GetEntryAssembly()!.GetName().Name,
            applicationVersion = typeof(App).Assembly.GetName().Version?.ToString(),
            assemblyPath, assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))),
            executable = Environment.ProcessPath, cwd = Environment.CurrentDirectory,
            helpFiles = embedded.Length, hashesMatched = manifest.Count, checks, bindingErrors,
            startup = "Published EXE; tool-only startup hook invokes production App.OnStartup with isolated services before the normal entry point.",
            network = "No network service is used: updates return no release, models are disabled, help reads only WPF pack resources."
        });
    }
}
