using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Threading.Tasks;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Data;
using KodakkuAssist.Module.GameOperate;
using IBattleChara = Dalamud.Game.ClientState.Objects.Types.IBattleChara;

namespace KodakkuAssistXSZYYSPolice;

[ScriptType(
    name: "力之塔（警察）",
    territorys: [1252],
    guid: "9F8E7D6C-5B4A-3C2D-1E0F-9A8B7C6D5E4F",
    version: "0.0.1",
    author: "XSZYYS",
    note: "力之塔副本的小警察功能，提供关键机制点名播报和检查功能"
)]
public class TowerPolice
{
    [UserSetting("小警察（开启后默语频道输出关键机制被点名玩家名字）")]
    public bool PoliceMode { get; set; } = false;

    [UserSetting("接收小队内的扔钱/复活/蓝药检查请求")]
    public bool ReceivePartyCheckRequest { get; set; } = false;

    [UserSetting("蓝药检查范围（仅小队）")]
    public bool Partycheck { get; set; } = false;

    [UserSetting("开发者模式（调试日志）")]
    public bool EnableDeveloperMode { get; set; } = false;

    // 辅助职业字典
    private static readonly Dictionary<uint, string> _supportJobStatus = new()
    {
        { 4242, "辅助自由人" },
        { 4358, "辅助骑士" },
        { 4359, "辅助狂战士" },
        { 4360, "辅助武僧" },
        { 4361, "辅助猎人" },
        { 4362, "辅助武士" },
        { 4363, "辅助吟游诗人" },
        { 4364, "辅助风水师" },
        { 4365, "辅助时魔法师" },
        { 4366, "辅助炮击士" },
        { 4367, "辅助药剂师" },
        { 4368, "辅助预言师" },
        { 4369, "辅助盗贼" }
    };

    // 用于记录扔钱次数的字典和锁
    private readonly Dictionary<string, Dictionary<string, int>> _moneyThrowCounts = new();
    private readonly object _moneyThrowLock = new();

    // 用于记录蓝药次数的字典和锁
    private readonly Dictionary<string, Dictionary<string, int>> _bluePotionCounts = new();
    private readonly object _bluePotionLock = new();

    // 用于猎物站位检测
    private readonly HashSet<ulong> _checkedPreyPlayers = new();
    private readonly object _preyCheckLock = new();

    // 用于圣枪分摊记录和擦边检查
    private readonly HashSet<ulong> _sacredBowPreyRecordedPlayers = new();
    private readonly object _sacredBowPreyLock = new();
    private readonly Dictionary<int, List<(ulong PlayerId, float Duration)>> _lanceShareAssignments = new();
    private readonly object _lanceShareLock = new();

    // 尾王平台站位位置 (用于猎物位置检测)
    private static readonly List<Vector3> SquarePositions = new()
    {
        new Vector3(100, 0, 60),
        new Vector3(140, 0, 60),
        new Vector3(140, 0, 100),
        new Vector3(140, 0, 140),
        new Vector3(100, 0, 140),
        new Vector3(60, 0, 140),
        new Vector3(60, 0, 100),
        new Vector3(60, 0, 60)
    };

    private static readonly float[] SquareAngles = new float[]
    {
        MathF.PI / 4,       // 45度
        MathF.PI / 2,       // 90度
        3 * MathF.PI / 4,   // 135度
        MathF.PI,           // 180度
        -3 * MathF.PI / 4,  // -135度
        -MathF.PI / 2,      // -90度
        -MathF.PI / 4,      // -45度
        0                   // 0度
    };

    // 猎物持续时间常量
    private const float PREY_DURATION_9S = 9.0f;
    private const float PREY_DURATION_13S = 13.0f;
    private const float PREY_DURATION_21S = 21.0f;
    private const float PREY_DURATION_TOLERANCE = 0.05f;

    public void Init(ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");

        // 清理小警察数据
        lock (_moneyThrowLock)
        {
            _moneyThrowCounts.Clear();
        }
        lock (_bluePotionLock)
        {
            _bluePotionCounts.Clear();
        }
        lock (_preyCheckLock)
        {
            _checkedPreyPlayers.Clear();
        }
        lock (_sacredBowPreyLock)
        {
            _sacredBowPreyRecordedPlayers.Clear();
        }
        lock (_lanceShareLock)
        {
            _lanceShareAssignments.Clear();
        }
    }

    #region 老一：分摊点名播报

    [ScriptMethod(
        name: "分摊点名播报（南侧）",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:40623"],
        userControl: false
    )]
    public void OnSouthStack(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        var target = accessory.Data.Objects.SearchById(@event.TargetId);
        if (target != null)
        {
            accessory.Method.SendChat($"/e 分摊（南侧）点名: {target.Name}");
        }
    }

    [ScriptMethod(
        name: "分摊点名播报（北侧）",
        eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:40622"],
        userControl: false
    )]
    public void OnNorthStack(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        var target = accessory.Data.Objects.SearchById(@event.TargetId);
        if (target != null)
        {
            accessory.Method.SendChat($"/e 分摊（北侧）点名: {target.Name}");
        }
    }

    #endregion

    #region 老一：陨石点名播报

    [ScriptMethod(
        name: "陨石点名播报",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:4339"],
        userControl: false
    )]
    public void OnCometeorStatusAdd(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        var target = accessory.Data.Objects.SearchById(@event.TargetId);
        if (target != null)
        {
            accessory.Method.SendChat($"/e 陨石点名: {target.Name}");
        }
    }

    #endregion

    #region 老二：雪球连线点名播报

    [ScriptMethod(
        name: "雪球连线点名播报",
        eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:0112"],
        userControl: false
    )]
    public void OnGlacialImpactTether(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        var target = accessory.Data.Objects.SearchById(@event.TargetId);
        if (target != null)
        {
            accessory.Method.SendChat($"/e 雪球连线点名: {target.Name}");
        }
    }

    #endregion

    #region 尾王：大斧猎物点名播报

    [ScriptMethod(
        name: "大斧猎物点名播报（9秒）",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:4351"],
        userControl: false
    )]
    public void GreatAxePrey(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        if (float.TryParse(@event["Duration"], out var duration1) &&
            Math.Abs(duration1 - PREY_DURATION_9S) < PREY_DURATION_TOLERANCE)
        {
            var target = accessory.Data.Objects.SearchById(@event.TargetId);
            if (target != null)
            {
                accessory.Method.SendChat($"/e 大圈（9秒）点名: {target.Name}");
            }
        }
    }

    [ScriptMethod(
        name: "大斧猎物点名播报（21秒）",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:4352"],
        userControl: false
    )]
    public void GreatAxePreyLong(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        CheckPreyPosition(accessory, @event.TargetId);

        if (float.TryParse(@event["Duration"], out var duration1) &&
            Math.Abs(duration1 - PREY_DURATION_21S) < PREY_DURATION_TOLERANCE)
        {
            var target = accessory.Data.Objects.SearchById(@event.TargetId);
            if (target != null)
            {
                accessory.Method.SendChat($"/e 大圈（21秒）点名: {target.Name}");
            }
        }
    }

    #endregion

    #region 尾王：小斧猎物点名播报

    [ScriptMethod(
        name: "小斧猎物点名播报",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:4350"],
        userControl: false
    )]
    public void LesserAxePrey(Event @event, ScriptAccessory accessory)
    {
        if (!PoliceMode) return;

        CheckPreyPosition(accessory, @event.TargetId);

        if (float.TryParse(@event["Duration"], out var duration1) &&
            Math.Abs(duration1 - PREY_DURATION_13S) < PREY_DURATION_TOLERANCE)
        {
            var target = accessory.Data.Objects.SearchById(@event.TargetId);
            if (target != null)
            {
                accessory.Method.SendChat($"/e 小圈（13秒）点名: {target.Name}");
            }
        }
        else if (Math.Abs(duration1 - PREY_DURATION_21S) < PREY_DURATION_TOLERANCE)
        {
            var target = accessory.Data.Objects.SearchById(@event.TargetId);
            if (target != null)
            {
                accessory.Method.SendChat($"/e 小圈（21秒）点名: {target.Name}");
            }
        }
    }

    #endregion
    
    #region 尾王：圣枪分摊点名播报和擦边检查

    [ScriptMethod(
        name: "圣枪分摊点名",
        eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:4338"]
    )]
    public void SacredBowPrey_RecordAndBroadcast(Event @event, ScriptAccessory accessory)
    {
        var player = accessory.Data.Objects.SearchById(@event.TargetId);
        if (player == null) return;

        lock (_sacredBowPreyLock)
        {
            if (_sacredBowPreyRecordedPlayers.Contains(player.EntityId)) return;
            _sacredBowPreyRecordedPlayers.Add(player.EntityId);

            if (float.TryParse(@event["Duration"], out var duration))
            {
                // 记录分摊数据（用于擦边检查）
                lock (_lanceShareLock)
                {
                    bool alreadyRecorded = _lanceShareAssignments.Values.Any(list => list.Any(p => p.PlayerId == player.EntityId));

                    if (!alreadyRecorded)
                    {
                        int platformIndex = -1;
                        for (int i = 0; i < SquarePositions.Count; i++)
                        {
                            if (IsPointInRotatedRect(player.Position, SquarePositions[i], 20, 20, SquareAngles[i]))
                            {
                                platformIndex = i % 3; // 0=下平台, 1=右上平台, 2=左上平台
                                break;
                            }
                        }

                        if (platformIndex != -1)
                        {
                            if (!_lanceShareAssignments.ContainsKey(platformIndex))
                            {
                                _lanceShareAssignments[platformIndex] = new List<(ulong, float)>();
                            }
                            _lanceShareAssignments[platformIndex].Add((player.EntityId, duration));
                            if (EnableDeveloperMode)
                            {
                                accessory.Log.Debug($"圣枪分摊记录: {player.Name.TextValue} 在平台 {platformIndex + 1}，持续时间 {duration:F2}s");
                            }
                        }
                        else
                        {
                            if (EnableDeveloperMode)
                            {
                                accessory.Log.Debug($"圣枪分摊记录: {player.Name.TextValue} 是战犯，不在任何平台。");
                            }
                        }
                    }
                }

                if (PoliceMode)
                {
                    // 为播报再次检查平台位置
                    int reportPlatformIndex = -1;
                    for (int i = 0; i < SquarePositions.Count; i++)
                    {
                        if (IsPointInRotatedRect(player.Position, SquarePositions[i], 20, 20, SquareAngles[i]))
                        {
                            reportPlatformIndex = i % 3;
                            break;
                        }
                    }

                    string platformName = reportPlatformIndex switch
                    {
                        0 => "下",
                        1 => "右上",
                        2 => "左上",
                        _ => "战犯"
                    };

                    accessory.Method.SendChat($"/e 圣枪分摊点名: {player.Name.TextValue} - {platformName} ({duration:F1}s)");
                }
            }
        }
    }

    [ScriptMethod(
        name: "圣枪分摊 - 擦边检查",
        eventType: EventTypeEnum.StatusRemove,
        eventCondition: ["StatusID:4338"]
    )]
    public void SacredBowPrey_CheckOnExpire(Event @event, ScriptAccessory accessory)
    {
        // 检查buff是否为正常到期
        if (!float.TryParse(@event["Duration"], out var remainingDuration) || remainingDuration > 0.1f)
        {
            return; // 如果持续时间不为0，说明是提前移除，不检查
        }

        var player = accessory.Data.Objects.SearchById(@event.TargetId);
        if (player == null || player.IsDead) return;

        lock (_lanceShareLock)
        {
            int initialPlatform = -1;
            (ulong PlayerId, float Duration) assignment = (0, 0);

            foreach (var entry in _lanceShareAssignments)
            {
                var found = entry.Value.FirstOrDefault(p => p.PlayerId == player.EntityId);
                if (found.PlayerId != 0)
                {
                    initialPlatform = entry.Key;
                    assignment = found;
                    break;
                }
            }

            if (initialPlatform == -1) return;

            var sortedPlayers = _lanceShareAssignments[initialPlatform].OrderBy(p => p.Duration).ToList();
            int orderIndex = sortedPlayers.FindIndex(p => p.PlayerId == player.EntityId);

            bool shouldCheck = false;
            switch (initialPlatform)
            {
                case 0: // 平台1 (下)
                    if (orderIndex == 0 || orderIndex == 2) shouldCheck = true;
                    break;
                case 1: // 平台2 (右上)
                    if (orderIndex == 1 || orderIndex == 2) shouldCheck = true;
                    break;
                case 2: // 平台3 (左上)
                    if (orderIndex == 0 || orderIndex == 1) shouldCheck = true;
                    break;
            }

            if (shouldCheck)
            {
                if (!IsCircleFullyContainedInAnyPlatform(player.Position))
                {
                    if (PoliceMode) accessory.Method.SendChat($"/e 分摊擦边: {player.Name.TextValue}");
                }
            }
        }
    }

    #endregion

    #region 尾王：站位检测辅助方法

    private void CheckPreyPosition(ScriptAccessory accessory, ulong targetId)
    {
        // 如果已经检查过该玩家，则直接返回
        lock (_preyCheckLock)
        {
            if (_checkedPreyPlayers.Contains(targetId)) return;
            _checkedPreyPlayers.Add(targetId);
        }

        // 这个检查由小警察模式统一控制
        if (!PoliceMode) return;

        var player = accessory.Data.Objects.SearchById(targetId);
        if (player == null) return;

        bool isInAnySquare = false;
        for (int i = 0; i < SquarePositions.Count; i++)
        {
            if (IsPointInRotatedRect(player.Position, SquarePositions[i], 20, 20, SquareAngles[i]))
            {
                isInAnySquare = true;
                break;
            }
        }

        if (!isInAnySquare)
        {
            accessory.Method.SendChat($"/e {player.Name} 站位错误！");
        }
    }

    private bool IsPointInRotatedRect(Vector3 point, Vector3 rectCenter, float rectWidth, float rectHeight, float rectAngleRad)
    {
        float translatedX = point.X - rectCenter.X;
        float translatedZ = point.Z - rectCenter.Z;

        float cosAngle = MathF.Cos(-rectAngleRad);
        float sinAngle = MathF.Sin(-rectAngleRad);

        float rotatedX = translatedX * cosAngle - translatedZ * sinAngle;
        float rotatedZ = translatedX * sinAngle + translatedZ * cosAngle;

        return (Math.Abs(rotatedX) <= rectWidth / 2) && (Math.Abs(rotatedZ) <= rectHeight / 2);
    }

    private bool IsCircleFullyContainedInAnyPlatform(Vector3 circleCenter)
    {
        const float circleRadius = 6f;
        const float platformSize = 20f;
        // 通过收缩平台来简化问题：如果圆心在一个更小的矩形内，那么整个圆就在原始矩形内
        float shrunkenSize = platformSize - 2 * circleRadius;

        for (int i = 0; i < SquarePositions.Count; i++)
        {
            if (IsPointInRotatedRect(circleCenter, SquarePositions[i], shrunkenSize, shrunkenSize, SquareAngles[i]))
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    #region 复活检查功能

    [ScriptMethod(
        name: "检查复活",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:regex:^(Echo|Party)$"]
    )]
    public async void CheckResurrection(Event @event, ScriptAccessory accessory)
    {
        try
        {
            string channel = @event["Type"].ToLower();
            if (!ReceivePartyCheckRequest && channel == "party") return;

            string message = @event["Message"];
            if (!message.StartsWith("复活检查")) return;

            string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int? targetCount = null;
            if (parts.Length > 1 && int.TryParse(parts[1], out int count))
            {
                targetCount = count;
            }

            var allResurrectionData = new List<Tuple<string, string, string, int>>();

            foreach (var gameObject in accessory.Data.Objects)
            {
                if (gameObject is IPlayerCharacter player)
                {
                    string playerName = player.Name.TextValue;
                    string classJob = player.ClassJob.Value.Name.ToString();
                    string supportJob = "无";
                    int resurrectionCount = 0;
                    bool hasResDebuff = false;

                    foreach (var status in player.StatusList)
                    {
                        if (status.StatusId == 4262 || status.StatusId == 4263)
                        {
                            resurrectionCount = status.Param;
                            hasResDebuff = true;
                        }
                        if (_supportJobStatus.TryGetValue(status.StatusId, out var jobName))
                        {
                            supportJob = jobName;
                        }
                    }

                    if (hasResDebuff)
                    {
                        allResurrectionData.Add(new Tuple<string, string, string, int>(
                            playerName, classJob, supportJob, resurrectionCount));
                    }
                }
            }

            var filteredData = targetCount.HasValue
                ? allResurrectionData.Where(t => t.Item4 == targetCount.Value).ToList()
                : allResurrectionData;

            if (filteredData.Count > 0)
            {
                var sortedData = filteredData.OrderBy(t => t.Item4).ToList();

                string title = targetCount.HasValue
                    ? $"--- 复活次数为 {targetCount.Value} 的玩家 ---"
                    : "--- 复活次数检查 ---";
                accessory.Method.SendChat($"/{channel} {title}");

                foreach (var data in sortedData)
                {
                    await Task.Delay(10);
                    accessory.Method.SendChat($"/{channel} {data.Item1} ({data.Item2} | {data.Item3}): {data.Item4}");
                }
            }
            else
            {
                await Task.Delay(10);
                string notFoundMessage = targetCount.HasValue
                    ? $"未找到复活次数为 {targetCount.Value} 的玩家。"
                    : "未找到有限制复活的玩家。";
                accessory.Method.SendChat($"/{channel} {notFoundMessage}");
            }
        }
        catch (Exception ex)
        {
            accessory.Log.Error($"CheckResurrection error: {ex.Message}");
        }
    }

    #endregion

    #region 扔钱检查功能

    [ScriptMethod(
        name: "记录扔钱次数",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:41606"],
        userControl: false
    )]
    public void RecordMoneyThrow(Event @event, ScriptAccessory accessory)
    {
        var source = accessory.Data.Objects.SearchById(@event.SourceId);
        var target = accessory.Data.Objects.SearchById(@event.TargetId);

        if (source == null || target == null || !(source is IBattleChara) || !(target is IBattleChara))
            return;

        string playerName = source.Name.TextValue;
        string bossName = target.Name.TextValue;

        lock (_moneyThrowLock)
        {
            if (!_moneyThrowCounts.ContainsKey(bossName))
            {
                _moneyThrowCounts[bossName] = new Dictionary<string, int>();
            }
            if (!_moneyThrowCounts[bossName].ContainsKey(playerName))
            {
                _moneyThrowCounts[bossName][playerName] = 0;
            }
            _moneyThrowCounts[bossName][playerName]++;
        }
    }

    [ScriptMethod(
        name: "检查扔钱",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:regex:^(Echo|Party)$", "Message:扔钱检查"]
    )]
    public async void CheckMoneyThrow(Event @event, ScriptAccessory accessory)
    {
        try
        {
            string channel = @event["Type"].ToLower();
            if (!ReceivePartyCheckRequest && channel == "party") return;

            Dictionary<string, List<KeyValuePair<string, int>>> sortedData;
            lock (_moneyThrowLock)
            {
                if (_moneyThrowCounts.Count == 0)
                {
                    accessory.Method.SendChat($"/{channel} 未记录到任何扔钱数据。");
                    return;
                }

                sortedData = new Dictionary<string, List<KeyValuePair<string, int>>>();
                foreach (var bossEntry in _moneyThrowCounts)
                {
                    sortedData[bossEntry.Key] = bossEntry.Value.OrderBy(kvp => kvp.Value).ToList();
                }
            }

            foreach (var bossEntry in sortedData)
            {
                accessory.Method.SendChat($"/{channel} --- {bossEntry.Key} 扔钱统计 ---");
                foreach (var data in bossEntry.Value)
                {
                    await Task.Delay(100);
                    accessory.Method.SendChat($"/{channel} {data.Key}: {data.Value} 次");
                }
            }
        }
        catch (Exception ex)
        {
            accessory.Log.Error($"CheckMoneyThrow error: {ex.Message}");
        }
    }

    [ScriptMethod(
        name: "清理扔钱数据",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:regex:^(Echo|Party)$", "Message:扔钱清理"]
    )]
    public void ClearMoneyThrowData(Event @event, ScriptAccessory accessory)
    {
        string channel = @event["Type"].ToLower();
        if (!ReceivePartyCheckRequest && channel == "party") return;

        lock (_moneyThrowLock)
        {
            _moneyThrowCounts.Clear();
        }
        accessory.Method.SendChat($"/{channel} 扔钱数据已清理。");
    }

    #endregion

    #region 蓝药检查功能

    [ScriptMethod(
        name: "记录蓝药次数",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:41633"],
        userControl: false
    )]
    public void RecordBluePotion(Event @event, ScriptAccessory accessory)
    {
        var source = accessory.Data.Objects.SearchById(@event.SourceId);
        var target = accessory.Data.Objects.SearchById(@event.TargetId);

        if (source == null || target == null || !(source is IPlayerCharacter) || !(target is IPlayerCharacter))
            return;

        string sourcePlayerName = source.Name.TextValue;
        string targetPlayerName = target.Name.TextValue;

        lock (_bluePotionLock)
        {
            if (!_bluePotionCounts.ContainsKey(targetPlayerName))
            {
                _bluePotionCounts[targetPlayerName] = new Dictionary<string, int>();
            }
            if (!_bluePotionCounts[targetPlayerName].ContainsKey(sourcePlayerName))
            {
                _bluePotionCounts[targetPlayerName][sourcePlayerName] = 0;
            }
            _bluePotionCounts[targetPlayerName][sourcePlayerName]++;
        }
    }

    [ScriptMethod(
        name: "检查蓝药",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:regex:^(Echo|Party)$", "Message:蓝药检查"]
    )]
    public async void CheckBluePotion(Event @event, ScriptAccessory accessory)
    {
        try
        {
            string channel = @event["Type"].ToLower();
            if (!ReceivePartyCheckRequest && channel == "party") return;

            Dictionary<string, List<KeyValuePair<string, int>>> sortedData;
            lock (_bluePotionLock)
            {
                if (_bluePotionCounts.Count == 0)
                {
                    accessory.Method.SendChat($"/{channel} 未记录到任何蓝药数据。");
                    return;
                }

                var partyMemberNames = Partycheck
                    ? accessory.Data.PartyList
                        .Select(id => accessory.Data.Objects.SearchById(id)?.Name.TextValue)
                        .Where(name => name != null)
                        .ToHashSet()
                    : null;

                sortedData = new Dictionary<string, List<KeyValuePair<string, int>>>();
                foreach (var bossEntry in _bluePotionCounts)
                {
                    var filteredPlayers = Partycheck
                        ? bossEntry.Value
                            .Where(kvp => partyMemberNames.Contains(kvp.Key) && partyMemberNames.Contains(bossEntry.Key))
                            .ToList()
                        : bossEntry.Value.ToList();

                    if (filteredPlayers.Count > 0)
                    {
                        sortedData[bossEntry.Key] = filteredPlayers.OrderBy(kvp => kvp.Value).ToList();
                    }
                }
            }

            if (sortedData.Count == 0)
            {
                accessory.Method.SendChat($"/{channel} 当前范围内未记录到符合条件的蓝药数据。");
                return;
            }

            foreach (var bossEntry in sortedData)
            {
                accessory.Method.SendChat($"/{channel} --- 对 {bossEntry.Key} 的蓝药统计 ---");

                foreach (var data in bossEntry.Value)
                {
                    await Task.Delay(100);
                    accessory.Method.SendChat($"/{channel} {data.Key}: {data.Value} 次");
                }
            }
        }
        catch (Exception ex)
        {
            accessory.Log.Error($"CheckBluePotion error: {ex.Message}");
        }
    }

    [ScriptMethod(
        name: "清理蓝药数据",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:regex:^(Echo|Party)$", "Message:蓝药清理"]
    )]
    public void ClearBluePotionData(Event @event, ScriptAccessory accessory)
    {
        string channel = @event["Type"].ToLower();
        if (!ReceivePartyCheckRequest && channel == "party") return;

        lock (_bluePotionLock)
        {
            _bluePotionCounts.Clear();
        }
        accessory.Method.SendChat($"/{channel} 蓝药数据已清理。");
    }

    #endregion

    #region 标记药师功能

    [ScriptMethod(
        name: "标记药师",
        eventType: EventTypeEnum.Chat,
        eventCondition: ["Type:Echo"]
    )]
    public async void MarkChemists(Event @event, ScriptAccessory accessory)
    {
        try
        {
            if (@event["Message"] != "标记药师") return;

            if (EnableDeveloperMode) accessory.Log.Debug("检测到'标记药师'指令...");

            accessory.Method.MarkClear();
            await Task.Delay(1000);

            var markType = MarkType.Attack1;
            int chemistsFound = 0;

            foreach (var gameObject in accessory.Data.Objects)
            {
                if (gameObject is IPlayerCharacter player)
                {
                    bool isChemist = false;
                    foreach (var status in player.StatusList)
                    {
                        if (status.StatusId == 4367) // 辅助药剂师
                        {
                            isChemist = true;
                            break;
                        }
                    }

                    if (isChemist)
                    {
                        accessory.Method.Mark(player.EntityId, markType);
                        chemistsFound++;
                        if (markType < MarkType.Attack8)
                        {
                            markType++;
                        }
                    }
                }
            }

            accessory.Method.SendChat($"/e 已标记 {chemistsFound} 名药师。");
        }
        catch (Exception ex)
        {
            accessory.Log.Error($"MarkChemists error: {ex.Message}");
        }
    }

    #endregion
}
