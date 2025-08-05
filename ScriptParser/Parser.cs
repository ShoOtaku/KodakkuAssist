using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;

// 用于存储从脚本中提取的信息
public class ScriptInfo
{
    public string Name { get; set; } = "";
    public string Guid { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public List<int> TerritoryIds { get; set; } = new List<int>();
    public string DownloadUrl { get; set; } = "";
    public string UpdateInfo { get; set; } = "";
}

public class Parser
{
    public static void Main(string[] args)
    {
        var workspacePath = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? ".";
        var scriptsPath = Path.Combine(workspacePath, "Scripts");
        var jsonFilePath = Path.Combine(workspacePath, "OnlineRepo.json");
        var githubRepo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "ShoOtaku/KodakkuAssist";

        var scriptInfos = new List<ScriptInfo>();

        Console.WriteLine($"---> Scanning for scripts in: {scriptsPath}");

        if (Directory.Exists(scriptsPath))
        {
            var files = Directory.GetFiles(scriptsPath, "*.cs", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                Console.WriteLine("---> Warning: No .cs files found in the Scripts directory.");
            }

            foreach (var file in files)
            {
                if (Path.GetFileName(file).Equals("Parser.cs", StringComparison.OrdinalIgnoreCase)) continue;

                Console.WriteLine($"---> Processing file: {Path.GetFileName(file)}");
                var content = File.ReadAllText(file);

                // 更新后的正则表达式，可以更好地处理跨行属性
                var match = Regex.Match(content, @"\[ScriptType\((.*?)\)\]", RegexOptions.Singleline);
                if (match.Success)
                {
                    var info = new ScriptInfo();
                    var attributes = match.Groups[1].Value.Replace("\n", " ").Replace("\r", " ");

                    info.Name = Regex.Match(attributes, @"name:\s*""([^""]+)""").Groups[1].Value;
                    info.Guid = Regex.Match(attributes, @"guid:\s*""([^""]+)""").Groups[1].Value;
                    info.Version = Regex.Match(attributes, @"version:\s*""([^""]+)""").Groups[1].Value;
                    info.Author = Regex.Match(attributes, @"author:\s*""([^""]+)""").Groups[1].Value;

                    var territoryMatch = Regex.Match(attributes, @"territorys:\s*\[([^\]]+)\]");
                    if (territoryMatch.Success)
                    {
                        info.TerritoryIds = territoryMatch.Groups[1].Value
                            .Split(',')
                            .Select(s => int.Parse(s.Trim()))
                            .ToList();
                    }

                    info.DownloadUrl = $"https://raw.githubusercontent.com/{githubRepo}/main/Scripts/{Path.GetFileName(file)}";

                    scriptInfos.Add(info);
                    Console.WriteLine($"---> Successfully parsed: {info.Name}");
                }
                else
                {
                    Console.WriteLine($"---> Warning: No [ScriptType] attribute found in {Path.GetFileName(file)}.");
                }
            }
        }
        else
        {
            Console.WriteLine($"---> Error: Directory not found at {scriptsPath}. Please ensure your scripts are in a 'Scripts' folder in the repository root.");
        }

        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var jsonString = JsonSerializer.Serialize(scriptInfos, options);

        File.WriteAllText(jsonFilePath, jsonString);
        Console.WriteLine($"---> Generated OnlineRepo.json with {scriptInfos.Count} entries.");
    }
}
