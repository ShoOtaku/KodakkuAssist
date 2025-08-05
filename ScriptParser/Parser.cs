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
        // 获取仓库的根目录，并定位到Scripts文件夹
        var workspacePath = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? ".";
        var scriptsPath = Path.Combine(workspacePath, "Scripts");
        var jsonFilePath = Path.Combine(workspacePath, "OnlineRepo.json");
        var githubRepo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "ShoOtaku/KodakkuAssist";

        var scriptInfos = new List<ScriptInfo>();

        Console.WriteLine($"Scanning for scripts in: {scriptsPath}");

        if (Directory.Exists(scriptsPath))
        {
            foreach (var file in Directory.GetFiles(scriptsPath, "*.cs"))
            {
                Console.WriteLine($"Processing file: {Path.GetFileName(file)}");
                var content = File.ReadAllText(file);

                // 使用正则表达式匹配 [ScriptType(...)] 属性
                var match = Regex.Match(content, @"\[ScriptType\(([^\]]+)\)\]");
                if (match.Success)
                {
                    var info = new ScriptInfo();
                    var attributes = match.Groups[1].Value;

                    // 提取各个参数
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

                    // 自动生成下载链接
                    info.DownloadUrl = $"https://raw.githubusercontent.com/{githubRepo}/main/Scripts/{Path.GetFileName(file)}";

                    scriptInfos.Add(info);
                    Console.WriteLine($"Successfully parsed: {info.Name}");
                }
            }
        }

        // 设置JSON序列化选项以美化输出
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var jsonString = JsonSerializer.Serialize(scriptInfos, options);

        File.WriteAllText(jsonFilePath, jsonString);
        Console.WriteLine($"Successfully generated OnlineRepo.json at: {jsonFilePath}");
    }
}