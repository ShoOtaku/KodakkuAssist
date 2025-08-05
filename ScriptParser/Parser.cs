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
        // 获取仓库的根目录
        var workspacePath = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? ".";
        // *** 重要 ***: 定义脚本所在的文件夹。
        // 推荐做法是将所有脚本放在 "Scripts" 文件夹中。
        // 如果您的脚本文件在仓库根目录，请将下面这行改为 var scriptsPath = workspacePath;
        var scriptsPath = Path.Combine(workspacePath, "Scripts"); 
        var jsonFilePath = Path.Combine(workspacePath, "OnlineRepo.json");
        var githubRepo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "ShoOtaku/KodakkuAssist";

        var scriptInfos = new List<ScriptInfo>();

        Console.WriteLine($"Scanning for scripts in: {scriptsPath}");

        if (Directory.Exists(scriptsPath))
        {
            // 注意：SearchOption.TopDirectoryOnly 意味着只搜索当前文件夹，不搜索子文件夹。
            foreach (var file in Directory.GetFiles(scriptsPath, "*.cs", SearchOption.TopDirectoryOnly))
            {
                // 避免解析自身
                if (Path.GetFileName(file).Equals("Parser.cs", StringComparison.OrdinalIgnoreCase)) continue;

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
                    // 注意：这里假设您的脚本都在 "Scripts" 文件夹下
                    info.DownloadUrl = $"https://raw.githubusercontent.com/{githubRepo}/main/Scripts/{Path.GetFileName(file)}";

                    scriptInfos.Add(info);
                    Console.WriteLine($"Successfully parsed: {info.Name}");
                }
            }
        }
        else
        {
            Console.WriteLine($"Error: Directory not found at {scriptsPath}");
        }

        // 设置JSON序列化选项以美化输出
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var jsonString = JsonSerializer.Serialize(scriptInfos, options);

        File.WriteAllText(jsonFilePath, jsonString);
        Console.WriteLine($"Successfully generated OnlineRepo.json at: {jsonFilePath}");
    }
}
