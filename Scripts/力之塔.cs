using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;
using Dalamud.Interface.ManagedFontAtlas;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Extensions;
using Newtonsoft.Json;
using System.Runtime.Intrinsics.Arm;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.GameOperate;
using System.Collections.Concurrent;
using FFXIVClientStructs.Havok.Animation.Rig;
using KodakkuAssist.Module.Draw.Manager;
using Lumina.Excel;

namespace KodakkuAssistXSZYYS
{
    public enum StrategySelection
    {
        ABC_123,  // 代表钰子烧分组攻略
        Pos_152463, // 代表 152463 分组攻略
        LemonCookie // 代表 柠檬烧饼 分组攻略
    }

    public enum TeamSelection
    {
        A,
        B,
        C,
        One,
        Two,
        Three
    }

    public enum PositionSelection
    {
        Pos1,
        Pos2,
        Pos3,
        Pos4,
        Pos5,
        Pos6
    }
    // 圣枪专用：分组覆盖（None=不变；左上=A；右上=C；下=B）
    public enum LanceGuideOverride
    {
        None, 
        左上, 
        右上, 
        下
    }

    [ScriptType(
    name: "力之塔",
    guid: "874D3ECF-BD6B-448F-BB42-AE7F082E4805",
    territorys: [1252],
    version: "0.0.41",
    author: "XSZYYS",
    note: "更新内容\r\n直接根据职能而非小队位置指向老二火球站位\r\n标记药师：输入【/e 标记药师】即可标记周围所有药师玩家\r\n藏宝图：1.5道中给箱子连线\r\n\r\n------------以下功能默认仅支持默语，可配置响应来自小队的检查指令并在小队频道输出------------\r\n检查蓝药：输入【/e 蓝药检查】会输出药师蓝药使用情况，输入【/e 蓝药清理】会清理所有数据\r\n检查复活：输入【/e 复活检查 <数字>】，比如【/e 复活检查 1】会输出周围所有剩余1次复活的玩家,支持空格分隔多个参数。默认输出复活次数在0~3次的所有玩家\r\n检查扔钱：输入【/e 扔钱检查】会输出所有使用扔钱的玩家和扔钱次数，输入【/e 扔钱清理】会清理所有数据\r\n------------------------------------------------------------\r\n请选择自己小队的分组，指路可选ABC123/152463/柠檬松饼攻略\r\n老一:\r\nAOE绘制：旋转，压溃\r\n指路：陨石点名，第一次踩塔，第二次踩塔\r\n老二：\r\nAOE绘制：死刑，扇形，冰火爆炸\r\n指路：雪球，火球\r\n老三：\r\nAOE绘制：龙态行动，冰圈，俯冲\r\n指路：龙态行动预站位，踩塔，小怪\r\n尾王：\r\nAOE绘制：致命斧/枪，暗杀短剑\r\n指路：符文之斧，圣枪"
    )]

    public class 力之塔
    {
        #region User_Settings_用户设置
        [UserSetting("-----全局设置----- (此设置无实际意义)")]
        public bool _____Global_Settings_____ { get; set; } = true;
        [UserSetting("启用TTS")]
        public bool EnableTTS { get; set; } = true;
        [UserSetting("启用文字横幅提示")]
        public bool EnableTextBanner { get; set; } = true;
        [UserSetting("攻略分组策略(钰子烧即ABC123/152463/柠檬松饼)")]
        public StrategySelection SelectedStrategy { get; set; } = StrategySelection.ABC_123;

        [UserSetting("【钰子烧】请选择您在团队中被分配到的分组")]
        public TeamSelection MyTeam { get; set; } = TeamSelection.A;

        [UserSetting("【152463】请选择您在团队中被分配到的分组")]
        public PositionSelection MyPosition { get; set; } = PositionSelection.Pos1;
        [UserSetting("【柠檬松饼】请选择您在团队中被分配到的分组")]
        public PositionSelection MyLemonCookiePosition { get; set; } = PositionSelection.Pos1;
        [UserSetting("符文之斧长点名小圈指路（会出现两个箭头指向左上和右上平台）")]
        public bool LongPointName { get; set; } = false;
        [UserSetting("圣枪分组覆盖（None=不变）")]
        public LanceGuideOverride HolyLanceGroupOverride { get; set; } = LanceGuideOverride.None;
        [UserSetting("小警察（开启后默语频道输出关键机制被点名玩家名字）")]
        public bool PoliceMode { get; set; } = false;
        [UserSetting("接收小队内的扔钱/复活/食物/蓝药检查请求")]
        public bool ReceivePartyCheckRequest { get; set; } = false;
        [UserSetting("食物检查剩余时间阈值（单位：分钟)")]
        public int FoodRemainingTimeThreshold { get; set; } = 10;
        [UserSetting("蓝药检查范围（仅小队）")]
        public bool Partycheck { get; set; } = false;
        [UserSetting("-----开发者设置----- (此设置无实际意义)")]
        public bool _____Developer_Settings_____ { get; set; } = true;

        [UserSetting("启用开发者模式")]
        public bool Enable_Developer_Mode { get; set; } = false;

        #endregion

        // 用于老一的状态变量
        private int _turnLeftRightCount = 0;
        // 用于陨石机制的状态变量
        private bool _hasCometeorStatus = false;
        private ulong _cometeorTargetId = 0;
        private const uint PortentousCometeorDataId = 2014582;
        private const float ArenaCenterZ = 379f; // 定义老一场地中心Z轴坐标
        private static readonly Vector3 Boss1ArenaCenter = new(700f, -481.01f, 379f);
        private bool? _isCasterInUpperHalf = null;
        private static readonly Vector3 Pos_A = new(704.49f, -481.01f, 365.38f);
        private static readonly Vector3 Pos_B = new(699.98f, -481.01f, 355.49f);
        private static readonly Vector3 Pos_C = new(695.49f, -481.01f, 365.38f);
        private static readonly Vector3 Pos_One = new(695.49f, -481.01f, 392.60f);
        private static readonly Vector3 Pos_Two = new(699.98f, -481.01f, 402.49f);
        private static readonly Vector3 Pos_Three = new(704.49f, -481.01f, 392.60f);

        // 用于老二的状态变量
        // 分别存储蓝色和红色AOE预告圈的位置
        private readonly object _iceFireLock = new();
        private readonly List<Vector3> _blueCircles = new();
        private readonly List<Vector3> _redCircles = new();
        private int _pairsProcessed = 0;
        // --- 雪球狂奔机制 ---
        private int _snowballRushCastCount = 0;
        private int _letterGroupRushCount = 0;
        private int _numberGroupRushCount = 0;
        private Vector3? _letterGroupNextPos;
        private Vector3? _numberGroupNextPos;
        private readonly object _snowballLock = new();
        private readonly object _fireballLock = new();
        private static readonly Vector3 InitialPosLetterGroup = new(-800.00f, -876.00f, 349.50f);
        private static readonly Vector3 InitialPosNumberGroup = new(-809.09f, -876.00f, 365.25f);
        private static readonly Vector3 SnowballArenaCenter = new(-800.00f, -876.00f, 360.00f);
        private ulong _tetherSourceId = 0;
        // --- 火球/地热机制 ---
        private readonly List<Vector3> _fireballPositions = new();
        // 定义两组火球的固定坐标
        private static readonly List<Vector3> LetterGroupFireballCoords = new()
        {
            new Vector3(-817.32f, -876.00f, 350.00f), 
            new Vector3(-817.32f, -876.00f, 370.00f), 
            new Vector3(-800.00f, -876.00f, 380.00f) 
        };
        private static readonly List<Vector3> NumberGroupFireballCoords = new()
        {
            new Vector3(-782.68f, -876.00f, 350.00f), 
            new Vector3(-800.00f, -876.00f, 340.00f), 
            new Vector3(-782.68f, -876.00f, 370.00f)  
        };
        //老三
        private static readonly Vector3 Boss3ArenaCenter = new(-337.00f, -840.00f, 157.00f); // Boss3场地中心位置
        // 用于区分水滩类型的枚举
        private enum PuddleType { Circle, Cross }
        // 用于存储场上水滩的字典 (Key: 实体ID, Value: 类型)
        private readonly ConcurrentDictionary<ulong, PuddleType> _puddles = new();
        // 冰塔分组坐标
        private static readonly Dictionary<TeamSelection, List<Vector3>> TowerPositions_ABC123 = new()
        {
            { TeamSelection.A, new List<Vector3> { new(-346.00f, -840.00f, 151.00f), new(-343.00f, -840.00f, 148.00f), new(-355.5f, -840.0f, 138.5f), new(-337.0f, -840.0f, 131.0f) } },
            { TeamSelection.B, new List<Vector3> { new(-337.00f, -840.00f, 151.00f), new(-343.00f, -840.00f, 157.00f), new(-337.0f, -840.0f, 131.0f), new(-318.5f, -840.0f, 138.5f) } },
            { TeamSelection.C, new List<Vector3> { new(-328.00f, -840.00f, 151.00f), new(-331.00f, -840.00f, 148.00f), new(-318.5f, -840.0f, 138.5f), new(-311.0f, -840.0f, 157.0f) } },
            { TeamSelection.One, new List<Vector3> { new(-328.00f, -840.00f, 163.00f), new(-331.00f, -840.00f, 166.00f), new(-318.5f, -840.0f, 175.5f), new(-337.0f, -840.0f, 183.0f) } },
            { TeamSelection.Two, new List<Vector3> { new(-337.00f, -840.00f, 163.00f), new(-331.00f, -840.00f, 157.00f), new(-337.0f, -840.0f, 183.0f), new(-355.5f, -840.0f, 175.5f) } },
            { TeamSelection.Three, new List<Vector3> { new(-346.00f, -840.00f, 163.00f), new(-343.00f, -840.00f, 166.00f), new(-355.5f, -840.0f, 175.5f), new(-363.0f, -840.0f, 157.0f) } }
        };
        // 152463 攻略的冰塔分组坐标
        private static readonly Dictionary<PositionSelection, List<Vector3>> TowerPositions_123456 = new()
        {
            { PositionSelection.Pos1, new List<Vector3> { new(-346.00f, -840.00f, 151.00f), new(-343.00f, -840.00f, 166.00f), new(-355.5f, -840.0f, 138.5f), new(-337.0f, -840.0f, 131.0f) } },
            { PositionSelection.Pos2, new List<Vector3> { new(-328.00f, -840.00f, 151.00f), new(-343.00f, -840.00f, 148.00f), new(-318.5f, -840.0f, 138.5f), new(-311.0f, -840.0f, 157.0f) } },
            { PositionSelection.Pos3, new List<Vector3> { new(-328.00f, -840.00f, 163.00f), new(-331.00f, -840.00f, 148.00f), new(-318.5f, -840.0f, 175.5f), new(-337.0f, -840.0f, 183.0f) } },
            { PositionSelection.Pos4, new List<Vector3> { new(-346.00f, -840.00f, 163.00f), new(-331.00f, -840.00f, 166.00f), new(-355.5f, -840.0f, 175.5f), new(-363.0f, -840.0f, 157.0f) } },
            { PositionSelection.Pos5, new List<Vector3> { new(-337.00f, -840.00f, 151.00f), new(-343.00f, -840.00f, 157.00f), new(-337.0f, -840.0f, 131.0f), new(-318.5f, -840.0f, 138.5f) } },
            { PositionSelection.Pos6, new List<Vector3> { new(-337.00f, -840.00f, 163.00f), new(-331.00f, -840.00f, 157.00f), new(-337.0f, -840.0f, 183.0f), new(-355.5f, -840.0f, 175.5f) } }
        };
        private static readonly Dictionary<PositionSelection, List<Vector3>> TowerPosition_Lemon = new()
        {
            { PositionSelection.Pos1, new List<Vector3> { new(-346.00f, -840.00f, 151.00f), new(-343.00f, -840.00f, 166.00f), new(-355.5f, -840.0f, 138.5f), new(-337.0f, -840.0f, 131.0f) } },
            { PositionSelection.Pos2, new List<Vector3> { new(-337.00f, -840.00f, 151.00f), new(-343.00f, -840.00f, 157.00f), new(-337.0f, -840.0f, 131.0f), new(-318.5f, -840.0f, 138.5f) } },
            { PositionSelection.Pos3, new List<Vector3> { new(-328.00f, -840.00f, 151.00f), new(-343.00f, -840.00f, 148.00f), new(-318.5f, -840.0f, 138.5f), new(-311.0f, -840.0f, 157.0f) } },
            { PositionSelection.Pos4, new List<Vector3> { new(-346.00f, -840.00f, 163.00f), new(-331.00f, -840.00f, 166.00f), new(-355.5f, -840.0f, 175.5f), new(-363.0f, -840.0f, 157.0f) } },
            { PositionSelection.Pos5, new List<Vector3> { new(-337.00f, -840.00f, 163.00f), new(-331.00f, -840.00f, 157.00f), new(-337.0f, -840.0f, 183.0f), new(-355.5f, -840.0f, 175.5f) } },
            { PositionSelection.Pos6, new List<Vector3> { new(-328.00f, -840.00f, 163.00f), new(-331.00f, -840.00f, 148.00f), new(-318.5f, -840.0f, 175.5f), new(-337.0f, -840.0f, 183.0f) } }
        };
        //小怪分组坐标
        private static readonly Dictionary<TeamSelection, Vector3> GroupMarkerPositions = new()
        {
            { TeamSelection.A, new Vector3(-347.50f, -840.00f, 146.50f) },
            { TeamSelection.B, new Vector3(-337.00f, -840.00f, 142.00f) },
            { TeamSelection.C, new Vector3(-326.50f, -840.00f, 146.50f) },
            { TeamSelection.One, new Vector3(-326.50f, -840.00f, 167.50f) },
            { TeamSelection.Two, new Vector3(-337.00f, -840.00f, 172.00f) },
            { TeamSelection.Three, new Vector3(-347.50f, -840.00f, 167.50f) }
        };
        //尾王
        // 方形AOE的固定坐标和角度
        private static readonly List<Vector3> SquarePositions = new()
        {
            new(700f, -476f, -659.504f),
            new(712.554f, -476f, -681.248f),
            new(687.443f, -476f, -681.25f)
        };
        private static readonly float[] SquareAngles =
        {
            -45 * MathF.PI / 180.0f,
            -15 * MathF.PI / 180.0f,
            105 * MathF.PI / 180.0f
        };
        // 大斧猎物机制的三个场边坐标
        private static readonly List<Vector3> GreataxePreyPositions = new()
        {
            new(699.95f, -476.00f, -705.12f),
            new(673.07f, -476.00f, -658.11f),
            new(726.77f, -476.00f, -658.39f)
        };
        // 致命斧机制的三个安全点坐标
        private static readonly List<Vector3> CriticalAxeSafePositions = new()
        {
            new(723.11f, -476.00f, -687.15f),
            new(677.93f, -476.00f, -686.84f),
            new(699.77f, -476.00f, -648.27f)
        };
        // 致命枪机制的三个安全点坐标
        private static readonly List<Vector3> CriticalLanceSafePositions = new()
        {
            new(693.53f, -476.00f, -670.09f),
            new(699.82f, -476.00f, -680.72f),
            new(706.28f, -476.00f, -670.38f)
        };
        // 圣枪机制的分组站位坐标
        private static readonly Vector3 RectSideInA = new(683.71f, -476.00f, -688.60f);
        private static readonly Vector3 RectSideOutA = new(680.45f, -476.00f, -691.63f);
        private static readonly Vector3 RectSideInB = new(721.56f, -476.00f, -682.53f);
        private static readonly Vector3 RectSideOutB = new(724.71f, -476.00f, -680.50f);
        private static readonly Vector3 RectSideInC = new(695.61f, -476.00f, -653.43f);
        private static readonly Vector3 RectSideOutC = new(694.24f, -476.00f, -648.11f);
        // 用于神圣机制的状态变量
        private enum HolyWeaponType { None, Axe, Lance }
        private HolyWeaponType _holyWeaponType = HolyWeaponType.None;
        // 用于记录已检查过猎物点名的玩家
        private readonly HashSet<ulong> _checkedPreyPlayers = new();
        private readonly object _preyCheckLock = new(); 
        private readonly HashSet<ulong> _sacredBowPreyRecordedPlayers = new();
        private readonly object _sacredBowPreyLock = new();
        // 用于记录枪分摊玩家及其debuff持续时间的字典
        private readonly Dictionary<int, List<(ulong PlayerId, float Duration)>> _lanceShareAssignments = new();
        private readonly object _lanceShareLock = new();
        
        //辅助职业字典
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
        // 获取玩家的辅助职业
        private string GetSupportJob(IPlayerCharacter player)
        {
            if (player == null) return "无";
            var status = player.StatusList.FirstOrDefault(s => _supportJobStatus.ContainsKey(s.StatusId));
            return status != null ? _supportJobStatus[status.StatusId] : "无";
        }
        // 用于记录扔钱次数的字典和锁
        private readonly Dictionary<string, Dictionary<string, int>> _moneyThrowCounts = new();
        private readonly object _moneyThrowLock = new();
        // 用于记录蓝药次数的字典和锁
        private readonly Dictionary<string, Dictionary<string, int>> _bluePotionCounts = new();
        private readonly object _bluePotionLock = new();
        
        
        public void Init(ScriptAccessory accessory)
        {
            accessory.Log.Debug("力之塔0.0.36脚本已加载。");
            accessory.Method.RemoveDraw(".*");

            _turnLeftRightCount = 0;
            // 初始化陨石机制状态
            _hasCometeorStatus = false;
            _cometeorTargetId = 0;
            _isCasterInUpperHalf = null;
            // 冰火
            _blueCircles.Clear();
            _redCircles.Clear();
            _pairsProcessed = 0;
            // 雪球狂奔
            _snowballRushCastCount = 0;
            _letterGroupRushCount = 0;
            _numberGroupRushCount = 0;
            _letterGroupNextPos = null;
            _numberGroupNextPos = null;
            _tetherSourceId = 0;
            // 火球/地热
            _fireballPositions.Clear();
            // 老三水滩
            _puddles.Clear();
            // 尾王
            _holyWeaponType = HolyWeaponType.None;
            lock(_sacredBowPreyLock)
            {
                _sacredBowPreyRecordedPlayers.Clear(); // 重置圣枪记录
            }
            lock(_preyCheckLock)
            {
                _checkedPreyPlayers.Clear(); // 重置已检查列表
            }
            lock(_lanceShareLock)
            {
                _lanceShareAssignments.Clear(); // 重置枪分摊记录
            }
            // 小警察
            lock(_moneyThrowLock)
            {
                _moneyThrowCounts.Clear();
            }
            lock(_bluePotionLock)
            {
                _bluePotionCounts.Clear();
            }

        }
        #region 老一
        [ScriptMethod(
            name: "初始化老一",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41734"],
            userControl: false
        )]
        public void OnInitializeBoss1Draw(Event @event, ScriptAccessory accessory)
        {
            // 初始化老一的状态
            _hasCometeorStatus = false;
            _cometeorTargetId = 0;
            _isCasterInUpperHalf = null;
            _turnLeftRightCount = 0;
            // 清除之前的绘制
            accessory.Method.RemoveDraw(".*");
            accessory.Log.Debug("老一初始化完成。");
        }



        [ScriptMethod(
            name: "降落（绘制）",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41812|43293|41709)$"]
        )]
        public void OnLandingDraw(Event @event, ScriptAccessory accessory)
        {
            var ActionId = @event.ActionId;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Landing_Danger_Zone";
            dp.Owner = @event.SourceId;
            switch (ActionId)
            {
                case 41812: //降落
                    dp.Scale = new Vector2(30, 6);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = 10500;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
                    break;
                case 43293: //降落
                    dp.Scale = new Vector2(30, 15);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = 10500;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
                case 41709: //降落
                    dp.Scale = new Vector2(18);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Delay = 8000;
                    dp.DestoryAt = 6000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    break;
            }
        }

        [ScriptMethod(
            name: "降落TTS（钢铁）",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:43293"],
            suppress: 1000
        )]
        public void OnLandingTTS(Event @event, ScriptAccessory accessory)
        {
            if (EnableTTS)
            {
                accessory.Method.EdgeTTS("钢铁");
            }
        }

        [ScriptMethod(
            name: "降落击退TTS",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(43794|43294)$"],
            suppress: 1000
        )]
        public void OnLandingKnockbackTTS(Event @event, ScriptAccessory accessory)
        {
            if (EnableTTS)
            {
                accessory.Method.EdgeTTS("击退");
            }
        }
        [ScriptMethod(
            name: "降落(击退)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(43794|43294)$"]
        )]
        public void AethericBarrierKnockback(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Aetheric_Barrier_Knockback";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(30, 5);
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.DestoryAt = 10500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            if(EnableTextBanner) accessory.Method.TextInfo("击退", 5000);
        }

        [ScriptMethod(
            name: "踩塔(指路)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41720"],
            suppress: 1000
        )]
        public void GroupPositionGuide(Event @event, ScriptAccessory accessory)
        {
            Vector3 targetPosition = new Vector3(); // Default value

            // 根据用户设置选择目标坐标
            switch (SelectedStrategy)
            {
                case StrategySelection.ABC_123:
                    switch (MyTeam)
                    {
                        case TeamSelection.A: targetPosition = Pos_A; break;
                        case TeamSelection.B: targetPosition = Pos_B; break;
                        case TeamSelection.C: targetPosition = Pos_C; break;
                        case TeamSelection.One: targetPosition = Pos_One; break;
                        case TeamSelection.Two: targetPosition = Pos_Two; break;
                        case TeamSelection.Three: targetPosition = Pos_Three; break;
                    }
                    break;
                case StrategySelection.Pos_152463:
                    switch (MyPosition)
                    {
                        case PositionSelection.Pos2: targetPosition = Pos_A; break;
                        case PositionSelection.Pos5: targetPosition = Pos_B; break;
                        case PositionSelection.Pos1: targetPosition = Pos_C; break;
                        case PositionSelection.Pos4: targetPosition = Pos_One; break;
                        case PositionSelection.Pos6: targetPosition = Pos_Two; break;
                        case PositionSelection.Pos3: targetPosition = Pos_Three; break;
                    }
                    break;
                case StrategySelection.LemonCookie:
                    // 柠檬烧饼分组攻略
                    // 152463组号到柠檬烧饼组号的映射：1->3, 2->1, 3->6, 4->4, 5->2, 6->5
                    switch (MyLemonCookiePosition) // 使用152463的位置选择器来表示柠檬烧饼的组号
                    {
                        case PositionSelection.Pos1: // 柠檬烧饼1组 -> 对应152463的2组位置
                            targetPosition = Pos_A; // 152463的2组对应Pos_A
                            break;
                        case PositionSelection.Pos2: // 柠檬烧饼2组 -> 对应152463的5组位置
                            targetPosition = Pos_B; // 152463的5组对应Pos_B
                            break;
                        case PositionSelection.Pos3: // 柠檬烧饼3组 -> 对应152463的1组位置
                            targetPosition = Pos_C; // 152463的1组对应Pos_C
                            break;
                        case PositionSelection.Pos4: // 柠檬烧饼4组 -> 对应152463的4组位置
                            targetPosition = Pos_One; // 152463的4组对应Pos_One
                            break;
                        case PositionSelection.Pos5: // 柠檬烧饼5组 -> 对应152463的6组位置
                            targetPosition = Pos_Two; // 152463的6组对应Pos_Two
                            break;
                        case PositionSelection.Pos6: // 柠檬烧饼6组 -> 对应152463的3组位置
                            targetPosition = Pos_Three; // 152463的3组对应Pos_Three
                            break;
                    }

                    break;
            }

            // 绘制指向目标位置的箭头
            var dp = accessory.Data.GetDefaultDrawProperties();
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"Group_Position_Guide_{MyTeam}";
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition = targetPosition;
            dp.Scale = new Vector2(1.5f);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.DestoryAt = 15000;
            dp2.Color = new Vector4(0, 1, 0, 0.6f); // 绿色
            dp2.Scale = new Vector2(4);
            dp2.DestoryAt = 15000;
            dp2.Position = targetPosition;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp2 );

            if (Enable_Developer_Mode)
            {
                accessory.Log.Debug($"为队伍 {MyTeam} 绘制站位指引，指向坐标 {targetPosition}");
            }
        }
        [ScriptMethod(
            name: "旋转",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41731"]
        )]
        public void OnRotationDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Rotation_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(37);
            dp.Radian = 90f * MathF.PI / 180f;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 8800;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        [ScriptMethod(
            name: "左/右转向",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41729|41730)$"]
        )]
        public void OnTurnLeftRightDraw(Event @event, ScriptAccessory accessory)
        {
            _turnLeftRightCount++;
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = "TurnLeftRight1_Danger_Zone";
            dp1.Position = @event.SourcePosition;
            dp1.Scale = new Vector2(66, 6);
            dp1.Color = accessory.Data.DefaultDangerColor;
            dp1.DestoryAt = 8800;
            dp2.Name = "TurnLeftRight2_Danger_Zone";
            dp2.Position = @event.SourcePosition;
            dp2.Scale = new Vector2(66, 6);
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.DestoryAt = 8800;
            dp2.Rotation = MathF.PI / 2f;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
            if (_turnLeftRightCount == 2 || _turnLeftRightCount == 4)
            {
                var dpNorth = accessory.Data.GetDefaultDrawProperties();
                dpNorth.Name = $"TurnLeftRight_North_Danger_Zone_{_turnLeftRightCount}";
                dpNorth.Position = Boss1ArenaCenter;
                dpNorth.Rotation = MathF.PI; // 指向北
                dpNorth.Scale = new Vector2(30, 33);
                dpNorth.Color = accessory.Data.DefaultDangerColor;
                dpNorth.DestoryAt = 8800;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpNorth);
            }
            else if (_turnLeftRightCount == 6)
            {
                var dpSouth = accessory.Data.GetDefaultDrawProperties();
                dpSouth.Name = "TurnLeftRight_South_Danger_Zone";
                dpSouth.Position = Boss1ArenaCenter;
                dpSouth.Rotation = 0; // 指向南
                dpSouth.Scale = new Vector2(30, 33);
                dpSouth.Color = accessory.Data.DefaultDangerColor;
                dpSouth.DestoryAt = 8800;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpSouth);
            }
        }

        [ScriptMethod(
            name: "分摊点名（北侧）",
            eventType: EventTypeEnum.TargetIcon,
            eventCondition: ["Id:023E"]
        )]
        public void OnNorthStack(Event @event, ScriptAccessory accessory)
        {
            if (PoliceMode)
            {
                var target = accessory.Data.Objects.SearchById(@event.TargetId);
                if (target != null)
                {
                    accessory.Method.SendChat($"/e 分摊（南侧）点名: {target.Name}");
                }
            }
            if (@event.TargetId != accessory.Data.Me) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "North_Stack";
            dp.Position = Boss1ArenaCenter;
            dp.Rotation = MathF.PI;
            dp.Scale = new Vector2(30, 33);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 12000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            if (EnableTTS)
            {
                accessory.Method.EdgeTTS("分摊去南侧");
            }
        }

        [ScriptMethod(
            name: "分摊点名（南侧）",
            eventType: EventTypeEnum.TargetIcon,
            eventCondition: ["Id:023F"]
        )]
        public void OnSouthStack(Event @event, ScriptAccessory accessory)
        {
            if (PoliceMode)
            {
                var target = accessory.Data.Objects.SearchById(@event.TargetId);
                if (target != null)
                {
                    accessory.Method.SendChat($"/e 分摊（北侧）点名: {target.Name}");
                }
            }
            if (@event.TargetId != accessory.Data.Me) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "South_Stack";
            dp.Position = Boss1ArenaCenter;
            dp.Rotation = 0;
            dp.Scale = new Vector2(30, 33);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 12000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            if (EnableTTS)
            {
                accessory.Method.EdgeTTS("分摊去北侧");
            }
        }
        [ScriptMethod(
            name: "陨石1(指路)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:4354"]
        )]
        public void OnCometeorStatusAdd(Event @event, ScriptAccessory accessory)
        {
            if (PoliceMode)
            {
                var target = accessory.Data.Objects.SearchById(@event.TargetId);
                if (target != null)
                {
                    accessory.Method.SendChat($"/e 陨石点名: {target.Name}");
                }
            }
            if (@event.TargetId != accessory.Data.Me) return;
            _hasCometeorStatus = true;
            if (EnableTTS)
            {
                accessory.Method.EdgeTTS("陨石点名");
            }
            if (Enable_Developer_Mode) accessory.Log.Debug("陨石机制：玩家获得状态。");
            TryDrawCometeorGuide(accessory);
        }
        [ScriptMethod(
            name: "陨石指路 - 状态消失",
            eventType: EventTypeEnum.StatusRemove,
            eventCondition: ["StatusID:4354"],
            userControl: false
        )]
        public void OnCometeorStatusRemove(Event @event, ScriptAccessory accessory)
        {
            if (@event.TargetId != accessory.Data.Me) return;
            _hasCometeorStatus = false;
            accessory.Method.RemoveDraw("Cometeor_Guide");
            if (Enable_Developer_Mode) accessory.Log.Debug("陨石机制：玩家状态消失，移除指路。");
        }

        [ScriptMethod(
            name: "陨石2(指路)",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["Operate:Add", "DataId:2014582"]
        )]
        public void OnCometeorTargetSpawn(Event @event, ScriptAccessory accessory)
        {
            _cometeorTargetId = @event.SourceId;
            if (Enable_Developer_Mode) accessory.Log.Debug($"陨石机制：目标单位(PortentousCometeor)出现，ID: {@event.SourceId}。");
            TryDrawCometeorGuide(accessory);
        }
        private void TryDrawCometeorGuide(ScriptAccessory accessory)
        {
            // 只有当玩家有状态且目标存在时，才绘制
            if (_hasCometeorStatus && _cometeorTargetId != 0)
            {
                if (Enable_Developer_Mode) accessory.Log.Debug("陨石机制：条件满足，开始绘制指路。");

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "Cometeor_Guide";
                dp.Owner = accessory.Data.Me;
                dp.TargetObject = _cometeorTargetId;
                dp.Scale = new Vector2(1.5f);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.DestoryAt = 12000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }
        [ScriptMethod(
            name: "陨石3(初始化)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:41702"],
            userControl: false
        )]
        public void OnCometeorActionEffect(Event @event, ScriptAccessory accessory)
        {
            // 初始化陨石机制状态
            _hasCometeorStatus = false;
            _cometeorTargetId = 0;
        }

        [ScriptMethod(
            name: "召唤(指路)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41741"]
        )]
        public void SummonGuide(Event @event, ScriptAccessory accessory)
        {
            Vector3 targetPosition = new Vector3();
            var letterGroupPos = new Vector3(700.24f, -481.00f, 360.46f);
            var numberGroupPos = new Vector3(700.02f, -481.00f, 398.08f);

            switch (SelectedStrategy)
            {
                case StrategySelection.ABC_123:
                    if (MyTeam == TeamSelection.A || MyTeam == TeamSelection.B || MyTeam == TeamSelection.C)
                    {
                        targetPosition = letterGroupPos;
                    }
                    else // One, Two, Three
                    {
                        targetPosition = numberGroupPos;
                    }
                    break;
                case StrategySelection.Pos_152463:
                    switch (MyPosition)
                    {
                        case PositionSelection.Pos1:
                        case PositionSelection.Pos5:
                        case PositionSelection.Pos2:
                            targetPosition = letterGroupPos;
                            break;
                        case PositionSelection.Pos4:
                        case PositionSelection.Pos6:
                        case PositionSelection.Pos3:
                            targetPosition = numberGroupPos;
                            break;
                    }
                    break;
                case StrategySelection.LemonCookie:
                    switch (MyLemonCookiePosition)
                    {
                        case PositionSelection.Pos1:
                        case PositionSelection.Pos2:
                        case PositionSelection.Pos3:
                            targetPosition = letterGroupPos;
                            break;
                        case PositionSelection.Pos4:
                        case PositionSelection.Pos5:
                        case PositionSelection.Pos6:
                            targetPosition = numberGroupPos;
                            break;
                    }
                    break;
            }

            // 绘制指路
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Summon_Guide";
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition = targetPosition;
            dp.Scale = new Vector2(1.5f);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.DestoryAt = 10000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            if (Enable_Developer_Mode)
            {
                accessory.Log.Debug($"为队伍 {MyTeam} 绘制召唤集合点，指向 {targetPosition}");
            }
        }
        //position.Z = 379 为分割上下半场
        [ScriptMethod(
            name: "浮空塔指路 - 记录半场",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41713|41711)$"],
            userControl: false
        )]
        public void HalfArenaRecord(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            _isCasterInUpperHalf = caster.Position.Z > ArenaCenterZ;

            if (Enable_Developer_Mode)
            {
                accessory.Log.Debug($"半场记录: 施法者位于 {(_isCasterInUpperHalf.Value ? "上半场" : "下半场")}");
            }
        }
        [ScriptMethod(
            name: "字母队浮空塔(指路)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:41707"],
            suppress: 1000
        )]
        public void FloatingTowerGuide(Event @event, ScriptAccessory accessory)
        {
            if (_isCasterInUpperHalf == null)
            {
                if (Enable_Developer_Mode) accessory.Log.Error("浮空塔指路: 未能获取到之前的半场信息。");
                return;
            }

            Vector3 targetPosition = new Vector3();
            bool shouldDraw = false;

            switch (SelectedStrategy)
            {
                case StrategySelection.ABC_123:
                    if (MyTeam == TeamSelection.A || MyTeam == TeamSelection.B || MyTeam == TeamSelection.C)
                    {
                        shouldDraw = true;
                        switch (MyTeam)
                        {
                            case TeamSelection.A: targetPosition = _isCasterInUpperHalf.Value ? Pos_One : Pos_A; break;
                            case TeamSelection.B: targetPosition = _isCasterInUpperHalf.Value ? Pos_Two : Pos_B; break;
                            case TeamSelection.C: targetPosition = _isCasterInUpperHalf.Value ? Pos_Three : Pos_C; break;
                        }
                    }
                    break;

                case StrategySelection.Pos_152463:
                    switch (MyPosition)
                    {
                        case PositionSelection.Pos2: // Corresponds to A
                            shouldDraw = true;
                            targetPosition = _isCasterInUpperHalf.Value ? Pos_One : Pos_A;
                            break;
                        case PositionSelection.Pos5: // Corresponds to B
                            shouldDraw = true;
                            targetPosition = _isCasterInUpperHalf.Value ? Pos_Two : Pos_B;
                            break;
                        case PositionSelection.Pos1: // Corresponds to C
                            shouldDraw = true;
                            targetPosition = _isCasterInUpperHalf.Value ? Pos_Three : Pos_C;
                            break;
                    }
                    break;
                case StrategySelection.LemonCookie:
                    switch (MyLemonCookiePosition)
                    {
                        case PositionSelection.Pos1: // Corresponds to A
                            shouldDraw = true;
                            targetPosition = _isCasterInUpperHalf.Value ? Pos_One : Pos_A;
                            break;
                        case PositionSelection.Pos2: // Corresponds to B
                            shouldDraw = true;
                            targetPosition = _isCasterInUpperHalf.Value ? Pos_Two : Pos_B;
                            break;
                        case PositionSelection.Pos3: // Corresponds to C
                            shouldDraw = true;
                            targetPosition = _isCasterInUpperHalf.Value ? Pos_Three : Pos_C;
                            break;
                    }
                    break;
            }

            if (shouldDraw)
            {
                var dpGuide = accessory.Data.GetDefaultDrawProperties();
                dpGuide.Name = $"Floating_Tower_Guide_Arrow";
                dpGuide.Owner = accessory.Data.Me;
                dpGuide.TargetPosition = targetPosition;
                dpGuide.Scale = new Vector2(1.5f);
                dpGuide.ScaleMode |= ScaleMode.YByDistance;
                dpGuide.Color = accessory.Data.DefaultSafeColor;
                dpGuide.DestoryAt = 7000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpGuide);

                var dpCircle = accessory.Data.GetDefaultDrawProperties();
                dpCircle.Name = $"Floating_Tower_Guide_Circle";
                dpCircle.Position = targetPosition;
                dpCircle.Scale = new Vector2(4);
                dpCircle.Color = new Vector4(0, 1, 0, 0.6f); // 绿色
                dpCircle.DestoryAt = 7000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dpCircle);
            }

            _isCasterInUpperHalf = null;
        }
        [ScriptMethod(
            name: "数字队地面塔(指路)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41713|41711)$"],
            suppress: 1000
        )]
        public void GroundTowerGuide(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            bool isCasterInUpperHalf = caster.Position.Z > ArenaCenterZ;
            Vector3 targetPosition = new Vector3();
            bool shouldDraw = false;

            switch (SelectedStrategy)
            {
                case StrategySelection.ABC_123:
                    if (MyTeam == TeamSelection.One || MyTeam == TeamSelection.Two || MyTeam == TeamSelection.Three)
                    {
                        shouldDraw = true;
                        switch (MyTeam)
                        {
                            case TeamSelection.One: targetPosition = isCasterInUpperHalf ? Pos_One : Pos_A; break;
                            case TeamSelection.Two: targetPosition = isCasterInUpperHalf ? Pos_Two : Pos_B; break;
                            case TeamSelection.Three: targetPosition = isCasterInUpperHalf ? Pos_Three : Pos_C; break;
                        }
                    }
                    break;
                case StrategySelection.Pos_152463:
                    if (MyPosition == PositionSelection.Pos3 || MyPosition == PositionSelection.Pos4 || MyPosition == PositionSelection.Pos6)
                    {
                        shouldDraw = true;
                        switch (MyPosition)
                        {
                            case PositionSelection.Pos4: targetPosition = isCasterInUpperHalf ? Pos_One : Pos_A; break;
                            case PositionSelection.Pos6: targetPosition = isCasterInUpperHalf ? Pos_Two : Pos_B; break;
                            case PositionSelection.Pos3: targetPosition = isCasterInUpperHalf ? Pos_Three : Pos_C; break;
                        }
                    }
                    break;
                case StrategySelection.LemonCookie:
                    if (MyLemonCookiePosition == PositionSelection.Pos4 ||
                        MyLemonCookiePosition == PositionSelection.Pos5 ||
                        MyLemonCookiePosition == PositionSelection.Pos6)
                    {
                        shouldDraw = true;
                        switch (MyLemonCookiePosition)
                        {
                            case PositionSelection.Pos4: targetPosition = isCasterInUpperHalf ? Pos_One : Pos_A; break;
                            case PositionSelection.Pos5: targetPosition = isCasterInUpperHalf ? Pos_Two : Pos_B; break;
                            case PositionSelection.Pos6: targetPosition = isCasterInUpperHalf ? Pos_Three : Pos_C; break;
                        }
                    }
                    break;
            }

            if (shouldDraw)
            {
                var dpGuide = accessory.Data.GetDefaultDrawProperties();
                dpGuide.Name = $"Ground_Tower_Guide_Arrow";
                dpGuide.Owner = accessory.Data.Me;
                dpGuide.TargetPosition = targetPosition;
                dpGuide.Scale = new Vector2(1.5f);
                dpGuide.ScaleMode |= ScaleMode.YByDistance;
                dpGuide.Color = accessory.Data.DefaultSafeColor;
                dpGuide.DestoryAt = 21000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpGuide);

                var dpCircle = accessory.Data.GetDefaultDrawProperties();
                dpCircle.Name = $"Ground_Tower_Guide_Circle";
                dpCircle.Position = targetPosition;
                dpCircle.Scale = new Vector2(4);
                dpCircle.Color = new Vector4(0, 1, 0, 0.6f); // 绿色
                dpCircle.DestoryAt = 21000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dpCircle);
            }
        }
        [ScriptMethod(
            name: "浮空(指路)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41707"]
        )]
        public void AbcTeamSafeZone(Event @event, ScriptAccessory accessory)
        {
            bool shouldDraw = false;
            switch (SelectedStrategy)
            {
                case StrategySelection.ABC_123:
                    if (MyTeam == TeamSelection.A || MyTeam == TeamSelection.B || MyTeam == TeamSelection.C)
                    {
                        shouldDraw = true;
                    }
                    break;
                case StrategySelection.Pos_152463:
                    if (MyPosition == PositionSelection.Pos1 || MyPosition == PositionSelection.Pos2 || MyPosition == PositionSelection.Pos5)
                    {
                        shouldDraw = true;
                    }
                    break;
                case StrategySelection.LemonCookie:
                    if (MyLemonCookiePosition == PositionSelection.Pos1 || MyLemonCookiePosition == PositionSelection.Pos2 || MyLemonCookiePosition == PositionSelection.Pos3)
                    {
                        shouldDraw = true;
                    }
                    break;
            }

            if (!shouldDraw) return;

            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Abc_Safe_Zone";
            dp.Owner = caster.EntityId;
            dp.Scale = new Vector2(4);
            dp.Color = new Vector4(0, 1, 0, 0.6f);
            dp.DestoryAt = 14000;

            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

            if (Enable_Developer_Mode)
            {
                accessory.Log.Debug("为浮空塔组绘制安全区。");
            }
        }
        #endregion
        #region 老二
        [ScriptMethod(
            name: "初始化老二",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:42492"],
            userControl: false
        )]
        public void OnInitializeBoss2Draw(Event @event, ScriptAccessory accessory)
        {
            // 初始化老二的状态
            _blueCircles.Clear();
            _redCircles.Clear();
            _pairsProcessed = 0;
            // 雪球狂奔
            _snowballRushCastCount = 0;
            _letterGroupRushCount = 0;
            _numberGroupRushCount = 0;
            _letterGroupNextPos = null;
            _numberGroupNextPos = null;
            _tetherSourceId = 0;
            // 火球/地热
            _fireballPositions.Clear();
            accessory.Method.RemoveDraw(".*");
            if(Enable_Developer_Mode) accessory.Log.Debug("老二初始化完成。");
        }

        [ScriptMethod(
            name: "斩切",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:42498"]
        )]
        public void OnSliceNDiceDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Slice_N_Dice_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(70);
            dp.Radian = 90f * MathF.PI / 180f;
            dp.TargetObject = @event.TargetId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(
            name: "复仇爆炎/冰封/毒",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(42429|42430|42431)$"]
        )]
        public void OnVengeDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Revenge_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(60);
            dp.Radian = 120f * MathF.PI / 180f;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(
            name: "冰火圈 - 记录预告",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(42464|42463)$"]
        )]
        public void PrimordialChaosTelegraph(Event @event, ScriptAccessory accessory)
        {
            // 使用锁来确保线程安全
            lock (_iceFireLock)
            {
                bool isBlue = @event.ActionId == 42464;
                var position = @event.EffectPosition;

                // 根据颜色将位置存入对应的列表
                if (isBlue)
                {
                    _blueCircles.Add(position);
                }
                else
                {
                    _redCircles.Add(position);
                }

                // 尝试处理并绘制成对的AOE
                ProcessAoePairs(accessory);
            }
        }

        private void ProcessAoePairs(ScriptAccessory accessory)
        {
            // 只要两个列表都有圈，就说明可以凑成一对
            while (_blueCircles.Count > 0 && _redCircles.Count > 0)
            {
                // 取出各自列表中的第一个圈
                var bluePos = _blueCircles[0];
                var redPos = _redCircles[0];

                // 计算爆炸时间点
                int explosionTime = 11000 + _pairsProcessed * 5500;
                // 固定显示时长
                const int displayDuration = 7000;
                // 计算预告圈的触发时间点 (假设每个预告圈的触发间隔是2500ms)
                int triggerTime = _pairsProcessed * 2500;
                // 计算绘图需要延迟的时间
                int delay = (explosionTime - displayDuration) - triggerTime;
                if (delay < 0) delay = 0; // 确保延迟不是负数
                var dpBlue = accessory.Data.GetDefaultDrawProperties();
                dpBlue.Name = $"PrimordialChaos_Blue_{_pairsProcessed}";
                dpBlue.Position = bluePos;
                dpBlue.Scale = new Vector2(22);
                dpBlue.Color = new Vector4(0.2f, 0.5f, 1f, 2.0f);
                dpBlue.Delay = delay;
                dpBlue.DestoryAt = displayDuration;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpBlue);

                // 绘制红色圈
                var dpRed = accessory.Data.GetDefaultDrawProperties();
                dpRed.Name = $"PrimordialChaos_Red_{_pairsProcessed}";
                dpRed.Position = redPos;
                dpRed.Scale = new Vector2(22);
                dpRed.Color = new Vector4(1f, 0.2f, 0.2f, 2.0f);
                dpRed.Delay = delay;
                dpRed.DestoryAt = displayDuration;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpRed);

                if(Enable_Developer_Mode) accessory.Log.Debug($"绘制第 {_pairsProcessed + 1} 对冰火圈");

                // 从列表中移除已处理的圈，并增加计数器
                _blueCircles.RemoveAt(0);
                _redCircles.RemoveAt(0);
                _pairsProcessed++;
            }
        }

        [ScriptMethod(
            name: "雪球狂奔(指路)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:42447"]
        )]
        public void SnowballRush(Event @event, ScriptAccessory accessory)
        {
            lock (_snowballLock)
            {
                var sourcePos = @event.SourcePosition;
                var nextPos = @event.EffectPosition;

                bool isLetterGroup = false;
                int currentGroupRushCount = 0;

                // 第一次（前两个）读条，用于分组
                if (_snowballRushCastCount < 2)
                {
                    if (Vector3.DistanceSquared(sourcePos, InitialPosLetterGroup) < Vector3.DistanceSquared(sourcePos, InitialPosNumberGroup))
                    {
                        isLetterGroup = true;
                        _letterGroupNextPos = nextPos;
                        currentGroupRushCount = _letterGroupRushCount++;
                        if(Enable_Developer_Mode) accessory.Log.Debug("雪球狂奔: 字母组已确定。");
                    }
                    else
                    {
                        isLetterGroup = false;
                        _numberGroupNextPos = nextPos;
                        currentGroupRushCount = _numberGroupRushCount++;
                        if(Enable_Developer_Mode) accessory.Log.Debug("雪球狂奔: 数字组已确定。");
                    }
                }
                // 后续读条，用于追踪路径
                else
                {
                    if (_letterGroupNextPos.HasValue && Vector3.DistanceSquared(sourcePos, _letterGroupNextPos.Value) < 1.0f)
                    {
                        isLetterGroup = true;
                        _letterGroupNextPos = nextPos;
                        currentGroupRushCount = _letterGroupRushCount++;
                        if(Enable_Developer_Mode) accessory.Log.Debug("雪球狂奔: 字母组路径更新。");
                    }
                    else if (_numberGroupNextPos.HasValue && Vector3.DistanceSquared(sourcePos, _numberGroupNextPos.Value) < 1.0f)
                    {
                        isLetterGroup = false;
                        _numberGroupNextPos = nextPos;
                        currentGroupRushCount = _numberGroupRushCount++;
                        if(Enable_Developer_Mode) accessory.Log.Debug("雪球狂奔: 数字组路径更新。");
                    }
                    else
                    {
                        // Fallback in case something goes wrong
                        if(Enable_Developer_Mode) accessory.Log.Error("雪球狂奔: 无法匹配到路径。");
                        return;
                    }
                }

                // --- 颜色判断逻辑 ---
                bool isSafe = false;
                // currentGroupRushCount 是从0开始计数的 (0=第1次, 1=第2次, 2=第3次)
                switch (SelectedStrategy)
                {
                    case StrategySelection.ABC_123:
                        if (isLetterGroup)
                        {
                            if ((MyTeam == TeamSelection.A && currentGroupRushCount == 0) ||
                                (MyTeam == TeamSelection.B && currentGroupRushCount == 1) ||
                                (MyTeam == TeamSelection.C && currentGroupRushCount == 2))
                            {
                                isSafe = true;
                            }
                        }
                        else // isNumberGroup
                        {
                            if ((MyTeam == TeamSelection.One && currentGroupRushCount == 0) ||
                                (MyTeam == TeamSelection.Two && currentGroupRushCount == 1) ||
                                (MyTeam == TeamSelection.Three && currentGroupRushCount == 2))
                            {
                                isSafe = true;
                            }
                        }
                        break;

                    case StrategySelection.Pos_152463:
                        if (isLetterGroup) // 1组(A), 2组(B), 3组(C)
                        {
                            if ((MyPosition == PositionSelection.Pos1 && currentGroupRushCount == 0) || // 1组对应A组(第1次)
                                (MyPosition == PositionSelection.Pos2 && currentGroupRushCount == 1) || // 2组对应B组(第2次)
                                (MyPosition == PositionSelection.Pos3 && currentGroupRushCount == 2))   // 3组对应C组(第3次)
                            {
                                isSafe = true;
                            }
                        }
                        else // isNumberGroup 4组(One), 5组(Two), 6组(Three)
                        {
                            if ((MyPosition == PositionSelection.Pos4 && currentGroupRushCount == 0) || // 4组对应One组(第1次)
                                (MyPosition == PositionSelection.Pos5 && currentGroupRushCount == 1) || // 5组对应Two组(第2次)
                                (MyPosition == PositionSelection.Pos6 && currentGroupRushCount == 2))   // 6组对应Three组(第3次)
                            {
                                isSafe = true;
                            }
                        }
                        break;
                    case StrategySelection.LemonCookie:
                        if (isLetterGroup) // 1组(A), 2组(B), 3组(C)
                        {
                            if ((MyLemonCookiePosition == PositionSelection.Pos1 && currentGroupRushCount == 0) || // 1组对应A组(第1次)
                                (MyLemonCookiePosition == PositionSelection.Pos2 && currentGroupRushCount == 1) || // 2组对应B组(第2次)
                                (MyLemonCookiePosition == PositionSelection.Pos3 && currentGroupRushCount == 2))   // 3组对应C组(第3次)
                            {
                                isSafe = true;
                            }
                        }
                        else // isNumberGroup 4组(One), 5组(Two), 6组(Three)
                        {
                            if ((MyLemonCookiePosition == PositionSelection.Pos4 && currentGroupRushCount == 0) || // 4组对应One组(第1次)
                                (MyLemonCookiePosition == PositionSelection.Pos5 && currentGroupRushCount == 1) || // 5组对应Two组(第2次)
                                (MyLemonCookiePosition == PositionSelection.Pos6 && currentGroupRushCount == 2))   // 6组对应Three组(第3次)
                            {
                                isSafe = true;
                            }
                        }
                        break;
                }

                var color = isSafe ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;

                // 绘制矩形AOE
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"SnowballRush_{_snowballRushCastCount}";
                dp.Position = sourcePos;
                dp.TargetPosition = nextPos;
                dp.Scale = new Vector2(10); // 宽度为10
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = color; // 使用计算出的颜色
                dp.DestoryAt = 7000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp);

                _snowballRushCastCount++;
            }
        }
        [ScriptMethod(
            name: "凝冰冲击 - 记录连线源",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:00F6"],
            userControl: false
        )]
        public void OnGlacialImpactTether(Event @event, ScriptAccessory accessory)
        {
            if (PoliceMode)
            {
                var target = accessory.Data.Objects.SearchById(@event.TargetId);
                if (target != null)
                {
                    accessory.Method.SendChat($"/e 雪球连线点名: {target.Name}");
                }
            }
            if (@event.TargetId == accessory.Data.Me)
            {
                _tetherSourceId = @event.SourceId;
                if(Enable_Developer_Mode) accessory.Log.Debug($"凝冰冲击: 玩家被连线，来源ID: {_tetherSourceId}");
            }
        }
        [ScriptMethod(
            name: "凝冰冲击 (指路)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:42451"]
        )]
        public async void GlacialImpact(Event @event, ScriptAccessory accessory)
        {
            await Task.Delay(1000);
            if(Enable_Developer_Mode) accessory.Log.Debug("凝冰冲击: 开始读条，触发指路绘制。");
            DrawGlacialImpactGuide(accessory);
        }



        private void DrawGlacialImpactGuide(ScriptAccessory accessory)
        {
            Vector3? safePosition = null;
            Vector3? finalDropPos = null;

            // 优先处理连线情况
            if (_tetherSourceId != 0)
            {
                var tetherSource = accessory.Data.Objects.SearchById(_tetherSourceId);
                if (tetherSource != null)
                {
                    var direction = Vector3.Normalize(SnowballArenaCenter - tetherSource.Position);
                    safePosition = SnowballArenaCenter + direction * 5;
                    if(Enable_Developer_Mode) accessory.Log.Debug("凝冰冲击: 检测到连线，计算特殊安全点。");
                }
                else
                {
                    if(Enable_Developer_Mode) accessory.Log.Error("凝冰冲击: 找不到连线来源单位。");
                }
            }
            else // 如果没有被连线，则执行分组逻辑
            {
                switch (SelectedStrategy)
                {
                    case StrategySelection.ABC_123:
                        bool isUserInLetterGroup = MyTeam == TeamSelection.A || MyTeam == TeamSelection.B || MyTeam == TeamSelection.C;
                        finalDropPos = isUserInLetterGroup ? _letterGroupNextPos : _numberGroupNextPos;
                        break;

                    case StrategySelection.Pos_152463:
                        bool isPosInLetterGroup = MyPosition == PositionSelection.Pos1 || MyPosition == PositionSelection.Pos2 || MyPosition == PositionSelection.Pos3;
                        finalDropPos = isPosInLetterGroup ? _letterGroupNextPos : _numberGroupNextPos;
                        break;
                    case StrategySelection.LemonCookie:
                        bool isLemonPosInLetterGroup = MyLemonCookiePosition == PositionSelection.Pos4 || MyLemonCookiePosition == PositionSelection.Pos5 || MyLemonCookiePosition == PositionSelection.Pos6;
                        finalDropPos = isLemonPosInLetterGroup ? _letterGroupNextPos : _numberGroupNextPos;
                        break;
                }

                if (finalDropPos != null)
                {
                    var direction = Vector3.Normalize(finalDropPos.Value - SnowballArenaCenter);
                    safePosition = SnowballArenaCenter - direction * 5;
                }
                else
                {
                    if(Enable_Developer_Mode) accessory.Log.Error("凝冰冲击: 找不到雪球最终落点。");
                }
            }

            // 如果成功计算出安全点，则绘制指路
            if (safePosition.HasValue)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "GlacialImpact_Guide";
                dp.Owner = accessory.Data.Me;
                dp.TargetPosition = safePosition.Value;
                dp.Scale = new Vector2(1.5f);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.DestoryAt = 5000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }

        [ScriptMethod(
            name: "凝冰冲击 - 结束",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:42451"],
            userControl: false
        )]
        public void GlacialImpactEnd(Event @event, ScriptAccessory accessory)
        {
            // 机制结束后重置连线状态
            _tetherSourceId = 0;
            if(Enable_Developer_Mode) accessory.Log.Debug("凝冰冲击: 机制结束，重置连线状态。");
        }

        //火球塔DataId=2014637

        [ScriptMethod(
            name: "火球预站位(指路)",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["Operate:Add", "DataId:2014637"]
        )]
        public void FireballPrePosition(Event @event, ScriptAccessory accessory)
        {
            // 使用锁来确保线程安全
            lock (_fireballLock)
            {
                _fireballPositions.Clear();
                var fireballs = accessory.Data.Objects.Where(o => o.DataId == 2014637).ToList();
                if (!fireballs.Any()) return;

                foreach (var fireball in fireballs)
                {
                    _fireballPositions.Add(fireball.Position);
                }

                // 根据新记录的位置列表绘制指引
                ProcessFireballs(accessory);
            }
        }


        [ScriptMethod(
            name: "火球安全点重绘(指路)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:42434"],
            suppress: 2000
        )]
        public void RedrawFireballGuides(Event @event, ScriptAccessory accessory)
        {
            // 使用第一次事件中储存的位置信息进行重绘
            if (!_fireballPositions.Any())
            {
                if(Enable_Developer_Mode) accessory.Log.Error("火球重绘: 找不到第一轮的火球坐标。");
                return;
            }
            ProcessFireballs(accessory);
        }
        private void ProcessFireballs(ScriptAccessory accessory)
        {
            bool isUserInLetterGroup = MyTeam == TeamSelection.A || MyTeam == TeamSelection.B || MyTeam == TeamSelection.C;
            switch (SelectedStrategy)
            {
                case StrategySelection.ABC_123:
                    isUserInLetterGroup = MyTeam == TeamSelection.A || MyTeam == TeamSelection.B || MyTeam == TeamSelection.C;
                    break;
                case StrategySelection.Pos_152463:
                    isUserInLetterGroup = MyPosition == PositionSelection.Pos1 || MyPosition == PositionSelection.Pos2 || MyPosition == PositionSelection.Pos3;
                    break;
                case StrategySelection.LemonCookie:
                    isUserInLetterGroup = MyLemonCookiePosition == PositionSelection.Pos1 || MyLemonCookiePosition == PositionSelection.Pos2 || MyLemonCookiePosition == PositionSelection.Pos3;
                    break;
            }
            int fireballIndex = 0;
            foreach (var fireballPos in _fireballPositions)
            {
                bool isLetterFireball = LetterGroupFireballCoords.Any(coord => Vector3.DistanceSquared(fireballPos, coord) < 1.0f);
                bool isNumberFireball = NumberGroupFireballCoords.Any(coord => Vector3.DistanceSquared(fireballPos, coord) < 1.0f);

                if ((isUserInLetterGroup && isLetterFireball) || (!isUserInLetterGroup && isNumberFireball))
                {
                    DrawPrePositionGuides(accessory, fireballPos, fireballIndex);
                }
                fireballIndex++;
            }
        }

        private void DrawPrePositionGuides(ScriptAccessory accessory, Vector3 fireballPos, int uniqueId)
        {
            var player = accessory.Data.MyObject;
            if (player == null) return;
            var directionToFireball = Vector3.Normalize(fireballPos - SnowballArenaCenter);

            if (IsDps(player))
            {
                var safePos = fireballPos + directionToFireball * 6;
                var dp = accessory.Data.GetDefaultDrawProperties();
                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Fireball_DPS_SafeZone_{uniqueId}";
                dp.Position = safePos;
                dp.Scale = new Vector2(1);
                dp.Color = new Vector4(0, 1, 0, 0.6f); // Green
                dp.DestoryAt = 10000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
                dp2.Name = "Guide_to_safepos";
                dp2.Scale = new Vector2(1);
                dp2.Owner = accessory.Data.Me;
                dp2.Color = new Vector4(0, 1, 0, 0.6f);
                dp2.ScaleMode |= ScaleMode.YByDistance;
                dp2.DestoryAt = 10000;
                dp2.TargetPosition = safePos;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
            }
            else if (IsHealer(player))
            {
                var perpendicularDir1 = new Vector3(-directionToFireball.Z, 0, directionToFireball.X);
                var perpendicularDir2 = new Vector3(directionToFireball.Z, 0, -directionToFireball.X);
                var safePos1 = fireballPos + perpendicularDir1 * 6;
                var safePos2 = fireballPos + perpendicularDir2 * 6;

                var dp1 = accessory.Data.GetDefaultDrawProperties();
                dp1.Name = $"Fireball_Healer_SafeZone1_{uniqueId}";
                dp1.Position = safePos2;
                dp1.Scale = new Vector2(1);
                dp1.Color = new Vector4(0, 1, 0, 0.6f); // Green
                dp1.DestoryAt = 10000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp1);
                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Name = "Guide_to_safepos";
                dp2.Scale = new Vector2(1);
                dp2.Owner = accessory.Data.Me;
                dp2.Color = new Vector4(0, 1, 0, 0.6f);
                dp2.DestoryAt = 10000;
                dp2.ScaleMode |= ScaleMode.YByDistance;
                dp2.TargetPosition = safePos2;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
                /*
                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Name = $"Fireball_Healer_SafeZone2_{fireball.EntityId}";
                dp2.Position = safePos2;
                dp2.Scale = new Vector2(1);
                dp2.Color = new Vector4(0, 1, 0, 0.6f); // Green
                dp2.DestoryAt = 35000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp2);
                */

            }
            else if (IsTank(player))
            {
                var directionToCenter = Vector3.Normalize(SnowballArenaCenter - fireballPos);
                var rotation = MathF.Atan2(directionToCenter.X, directionToCenter.Z);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Fireball_Tank_SafeZone_{uniqueId}";
                dp.Position = fireballPos;
                dp.Rotation = rotation;
                dp.Scale = new Vector2(5);
                dp.Radian = 15 * MathF.PI / 180.0f;
                dp.Color = new Vector4(0, 1, 0, 0.6f); // Green
                dp.DestoryAt = 10000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Name = "Guide_to_safepos";
                dp2.Scale = new Vector2(1);
                dp2.Owner = accessory.Data.Me;
                dp2.Color = new Vector4(0, 1, 0, 0.6f);
                dp2.DestoryAt = 10000;
                dp2.TargetPosition = fireballPos;
                dp2.ScaleMode |= ScaleMode.YByDistance;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
            }
        }
        [ScriptMethod(
            name: "地热破裂 (指路)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:42441"]
        )]
        public void GeothermalRupture(Event @event, ScriptAccessory accessory)
        {
            if (_fireballPositions.Count == 0)
            {
                if(Enable_Developer_Mode) accessory.Log.Error("地热破裂: 未能获取到火球位置信息。");
                return;
            }

            int pathIndex = 0;

            // 为每个记录的DPS位置绘制路径
            foreach (var fireballPos in _fireballPositions)
            {
                var directionToFireball = Vector3.Normalize(fireballPos - SnowballArenaCenter);
                var startPos = fireballPos + directionToFireball * 6;
                // 计算路径点
                var point1 = RotatePoint(startPos, fireballPos, MathF.PI / 2); // 顺时针90度
                var point2 = RotatePoint(startPos, fireballPos, MathF.PI);   // 顺时针180度

                // 绘制从起点到90度点的路径（黄）
                var dp1 = accessory.Data.GetDefaultDrawProperties();
                dp1.Name = $"GeothermalRupture_Path1_{pathIndex}";
                dp1.Position = startPos;
                dp1.TargetPosition = point1;
                dp1.Scale = new Vector2(1.5f);
                dp1.ScaleMode |= ScaleMode.YByDistance;
                dp1.Color = new Vector4(1f, 1f, 0f, 1f);
                dp1.DestoryAt = 5000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
                // 绘制从起点到90度点的路径（绿）
                var dp4 = accessory.Data.GetDefaultDrawProperties();
                dp4.Name = $"GeothermalRupture_Path1_{pathIndex}";
                dp4.Position = startPos;
                dp4.TargetPosition = point1;
                dp4.Scale = new Vector2(1.5f);
                dp4.ScaleMode |= ScaleMode.YByDistance;
                dp4.Color = new Vector4(0f, 1f, 0f, 1f);
                dp4.Delay = 5000;
                dp4.DestoryAt = 3000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp4);

                // 绘制从90度点到180度点的路径（黄）
                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Name = $"GeothermalRupture_Path2_{pathIndex}";
                dp2.Position = point1;
                dp2.TargetPosition = point2;
                dp2.Scale = new Vector2(1.5f);
                dp2.ScaleMode |= ScaleMode.YByDistance;
                dp2.Color = new Vector4(1f, 1f, 0f, 1f);
                dp2.DestoryAt = 8000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
                // 绘制从90度点到180度点的路径（绿）
                var dp3 = accessory.Data.GetDefaultDrawProperties();
                dp3.Name = $"GeothermalRupture_Path2_{pathIndex}";
                dp3.Position = point1;
                dp3.TargetPosition = point2;
                dp3.Scale = new Vector2(1.5f);
                dp3.ScaleMode |= ScaleMode.YByDistance;
                dp3.Color = new Vector4(0f, 1f, 0f, 1f);
                dp3.Delay = 8000;
                dp3.DestoryAt = 3000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);





                pathIndex++;
            }
        }

        #endregion
        #region 老三
        [ScriptMethod(
            name: "初始化老三",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:30705"],
            userControl: false
        )]
        public void OnInitializeBoss3Draw(Event @event, ScriptAccessory accessory)
        {
            // 初始化老三的状态

            accessory.Method.RemoveDraw(".*");
            if(Enable_Developer_Mode) accessory.Log.Debug("老三初始化完成。");
            _puddles.Clear();
        }
        [ScriptMethod(
            name: "龙态行动",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:30657"]
        )]
        public void OnDraconiformMotionDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "DraconiformMotion_Danger_Zone1";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(60);
            dp.Radian = 90f * MathF.PI / 180f;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode = ScaleMode.YByTime;
            dp.DestoryAt = 4800;
            dp2.Name = "DraconiformMotion_Danger_Zone2";
            dp2.Owner = @event.SourceId;
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.Scale = new Vector2(60);
            dp2.Radian = 90f * MathF.PI / 180f;
            dp2.Rotation = MathF.PI;
            dp2.ScaleMode = ScaleMode.YByTime;
            dp2.DestoryAt = 4800;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp2);
        }
        [ScriptMethod(
            name: "俯冲",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:37819"]
        )]
        public void OnFrigidDiveDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "FrigidDive_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(20, 60);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.DestoryAt = 8000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            if (EnableTTS)
            {
                accessory.Method.EdgeTTS("俯冲");
            }
        }
        [ScriptMethod(
            name: "水滩 - 记录",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["Operate:Add", "DataId:regex:^(2014546|2014547)$"],
            userControl: false
        )]
        public void OnPuddleSpawn(Event @event, ScriptAccessory accessory)
        {
            var id = @event.SourceId;
            var dataId = uint.Parse(@event["DataId"]);

            if (dataId == 2014546)
            {
                _puddles[id] = PuddleType.Circle;
            }
            else if (dataId == 2014547)
            {
                _puddles[id] = PuddleType.Cross;
            }
        }
        [ScriptMethod(
            name: "水滩 - 移除",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["Operate:Remove", "DataId:regex:^(2014546|2014547)$"],
            userControl: false
        )]
        public void OnPuddleDespawn(Event @event, ScriptAccessory accessory)
        {
            _puddles.TryRemove(@event.SourceId, out _);
        }
        [ScriptMethod(
            name: "水滩(钢铁十字)",
            eventType: EventTypeEnum.ObjectEffect,
            eventCondition: ["Id1:16", "Id2:32"]
        )]
        public void OnPuddleEffect(Event @event, ScriptAccessory accessory)
        {
            var sourceId = @event.SourceId;
            var source = accessory.Data.Objects.SearchById(sourceId);

            // 检查单位是否存在，是否在场地内，以及是否是我们记录的水滩
            if (source == null ||
                Vector3.Distance(source.Position, Boss3ArenaCenter) > 30 ||
                !_puddles.TryGetValue(sourceId, out var type))
            {
                return;
            }

            switch (type)
            {
                case PuddleType.Circle:
                    var dpCircle = accessory.Data.GetDefaultDrawProperties();
                    dpCircle.Name = $"Puddle_Circle_{sourceId}";
                    dpCircle.Owner = sourceId;
                    dpCircle.Scale = new Vector2(20);
                    dpCircle.Color = accessory.Data.DefaultDangerColor;
                    dpCircle.ScaleMode = ScaleMode.ByTime;
                    dpCircle.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
                    break;

                case PuddleType.Cross:
                    // 绘制第一条直线
                    var dpCross1 = accessory.Data.GetDefaultDrawProperties();
                    dpCross1.Name = $"Puddle_Cross1_{sourceId}";
                    dpCross1.Owner = sourceId;
                    dpCross1.Scale = new Vector2(16, 120);
                    dpCross1.Color = accessory.Data.DefaultDangerColor;
                    dpCross1.ScaleMode = ScaleMode.ByTime;
                    dpCross1.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dpCross1);

                    // 绘制第二条垂直的直线
                    var dpCross2 = accessory.Data.GetDefaultDrawProperties();
                    dpCross2.Name = $"Puddle_Cross2_{sourceId}";
                    dpCross2.Owner = sourceId;
                    dpCross2.Scale = new Vector2(16, 120);
                    dpCross2.Rotation = MathF.PI / 2; // 旋转90度
                    dpCross2.Color = accessory.Data.DefaultDangerColor;
                    dpCross2.ScaleMode = ScaleMode.ByTime;
                    dpCross2.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dpCross2);
                    break;
            }
        }
        [ScriptMethod(
            name: "龙态行动预站位(指路)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(30063|30264)$"]
        )]
        //{-336.98, -840.00, 165.53}
        public void OnDraconiformMotionGuide(Event @event, ScriptAccessory accessory)
        {
            // 绘制指路
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "DraconiformMotion_Guide";
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition = new Vector3(-336.98f, -840.00f, 165.53f); // Boss背后
            dp.Scale = new Vector2(1.5f);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.DestoryAt = 8000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            if (EnableTTS)
            {
                accessory.Method.EdgeTTS("预站位");
            }
        }
        [ScriptMethod(
            name: "踩冰塔 - 出现(指路)",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["Operate:Add", "DataId:2014548"]
        )]
        public void OnIceTowerSpawn(Event @event, ScriptAccessory accessory)
        {
            var tower = accessory.Data.Objects.SearchById(@event.SourceId);
            if (tower == null) return;

            List<Vector3> teamTowerCoords = new();
            switch (SelectedStrategy)
            {
                case StrategySelection.ABC_123:
                    TowerPositions_ABC123.TryGetValue(MyTeam, out teamTowerCoords);
                    break;
                case StrategySelection.Pos_152463:
                    TowerPositions_123456.TryGetValue(MyPosition, out teamTowerCoords);
                    break;
                case StrategySelection.LemonCookie:
                    TowerPosition_Lemon.TryGetValue(MyLemonCookiePosition, out teamTowerCoords);
                    break;
            }

            if (teamTowerCoords != null)
            {
                // 检查出现的塔是否是自己队伍的塔
                foreach (var coord in teamTowerCoords)
                {
                    if (Vector3.DistanceSquared(tower.Position, coord) < 1.0f)
                    {
                        // 是自己的塔，进行绘制
                        var dpCircle = accessory.Data.GetDefaultDrawProperties();
                        dpCircle.Name = $"IceTower_Circle_{tower.EntityId}";
                        dpCircle.Position = tower.Position;
                        dpCircle.Scale = new Vector2(4);
                        dpCircle.Color = new Vector4(0, 1, 0, 1); // Green
                        dpCircle.DestoryAt = 22000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dpCircle);
                        // 找到后即可退出循环
                        break;
                    }
                }
            }
        }
        [ScriptMethod(
            name: "踩冰塔 - 效果触发(指路)",
            eventType: EventTypeEnum.ObjectEffect,
            eventCondition: ["Id1:16", "Id2:32"]
        )]
        public void OnIceTowerEffect(Event @event, ScriptAccessory accessory)
        {
            var tower = accessory.Data.Objects.SearchById(@event.SourceId);

            if (tower == null || tower.DataId != 2014548) return;

            List<Vector3> teamTowerCoords = null;
            switch (SelectedStrategy)
            {
                case StrategySelection.ABC_123:
                    TowerPositions_ABC123.TryGetValue(MyTeam, out teamTowerCoords);
                    break;
                case StrategySelection.Pos_152463:
                    TowerPositions_123456.TryGetValue(MyPosition, out teamTowerCoords);
                    break;
                case StrategySelection.LemonCookie:
                    TowerPosition_Lemon.TryGetValue(MyLemonCookiePosition, out teamTowerCoords);
                    break;
            }

            if (teamTowerCoords != null)
            {
                foreach (var coord in teamTowerCoords)
                {
                    if (Vector3.DistanceSquared(tower.Position, coord) < 1.0f)
                    {
                        var dpGuide = accessory.Data.GetDefaultDrawProperties();
                        dpGuide.Name = $"IceTower_Guide_{tower.EntityId}";
                        dpGuide.Owner = accessory.Data.Me;
                        dpGuide.TargetObject = tower.EntityId;
                        dpGuide.Scale = new Vector2(1.5f);
                        dpGuide.ScaleMode |= ScaleMode.YByDistance;
                        dpGuide.Color = accessory.Data.DefaultSafeColor;
                        dpGuide.DestoryAt = 4000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpGuide);
                        break;
                    }
                }
            }
        }
        [ScriptMethod(
            name: "小怪分组(指路)",
            eventType: EventTypeEnum.AddCombatant,
            eventCondition: ["DataId:14730"]
        )]
        public void OnGroupMarkerSpawn(Event @event, ScriptAccessory accessory)
        {
            var marker = accessory.Data.Objects.SearchById(@event.SourceId);
            if (marker == null) return;

            TeamSelection targetGroup = MyTeam; // Default for ABC_123
            bool shouldDraw = false;


        switch (SelectedStrategy)
        {
            case StrategySelection.ABC_123:
                shouldDraw = true;
                targetGroup = MyTeam;
                break;
                
            case StrategySelection.Pos_152463:
                shouldDraw = true;
                switch (MyPosition)
                {
                    case PositionSelection.Pos1: targetGroup = TeamSelection.A; break;
                    case PositionSelection.Pos5: targetGroup = TeamSelection.B; break;
                    case PositionSelection.Pos2: targetGroup = TeamSelection.C; break;
                    case PositionSelection.Pos3: targetGroup = TeamSelection.One; break;
                    case PositionSelection.Pos6: targetGroup = TeamSelection.Two; break;
                    case PositionSelection.Pos4: targetGroup = TeamSelection.Three; break;
                }
                break;
                
            case StrategySelection.LemonCookie:
                shouldDraw = true;
                // 柠檬松饼分组攻略
                // 152463组号到柠檬松饼组号的映射：1->1, 2->3, 3->6, 4->4, 5->2, 6->5
                // 这里需要反向映射：柠檬松饼组号 -> 对应的152463位置 -> 对应的TeamSelection
                switch (MyLemonCookiePosition)
                {
                    case PositionSelection.Pos1: // 柠檬松饼1组 -> 152463的1组 -> TeamSelection.A
                        targetGroup = TeamSelection.A; 
                        break;
                    case PositionSelection.Pos2: // 柠檬松饼2组 -> 152463的5组 -> TeamSelection.B  
                        targetGroup = TeamSelection.B;
                        break;
                    case PositionSelection.Pos3: // 柠檬松饼3组 -> 152463的2组 -> TeamSelection.C
                        targetGroup = TeamSelection.C;
                        break;
                    case PositionSelection.Pos4: // 柠檬松饼4组 -> 152463的4组 -> TeamSelection.Three
                        targetGroup = TeamSelection.Three;
                        break;
                    case PositionSelection.Pos5: // 柠檬松饼5组 -> 152463的6组 -> TeamSelection.Two  
                        targetGroup = TeamSelection.Two;
                        break;
                    case PositionSelection.Pos6: // 柠檬松饼6组 -> 152463的3组 -> TeamSelection.One
                        targetGroup = TeamSelection.One;
                        break;
                }
                break;
        }


            if (shouldDraw)
            {
                foreach (var groupEntry in GroupMarkerPositions)
                {
                    if (Vector3.DistanceSquared(marker.Position, groupEntry.Value) < 1.0f)
                    {
                        if (groupEntry.Key == targetGroup)
                        {
                            var dpCircle = accessory.Data.GetDefaultDrawProperties();
                            dpCircle.Name = $"GroupMarker_Circle_{marker.EntityId}";
                            dpCircle.Owner = marker.EntityId;
                            dpCircle.Scale = new Vector2(3);
                            dpCircle.Color = new Vector4(0, 1, 0, 1);
                            dpCircle.DestoryAt = 4000;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dpCircle);

                            var dpGuide = accessory.Data.GetDefaultDrawProperties();
                            dpGuide.Name = $"GroupMarker_Guide_{marker.EntityId}";
                            dpGuide.Owner = accessory.Data.Me;
                            dpGuide.TargetObject = marker.EntityId;
                            dpGuide.Scale = new Vector2(1.5f);
                            dpGuide.ScaleMode |= ScaleMode.YByDistance;
                            dpGuide.Color = accessory.Data.DefaultSafeColor;
                            dpGuide.DestoryAt = 4000;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpGuide);
                            break;
                        }
                    }
                }
            }
        }
        #endregion
        #region 尾王
        [ScriptMethod(
            name: "尾王 - 初始化",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41572"],
            userControl: false
        )]
        public void OnInitializeBoss4Draw(Event @event, ScriptAccessory accessory)
        {
            // 初始化尾王的状态
            accessory.Method.RemoveDraw(".*");
            if(Enable_Developer_Mode) accessory.Log.Debug("尾王初始化完成。");
            _holyWeaponType = HolyWeaponType.None;
            lock(_preyCheckLock)
            {
                _checkedPreyPlayers.Clear(); // 重置已检查列表
            }
            lock(_lanceShareLock)
            {
                _lanceShareAssignments.Clear(); // 重置枪分摊记录
            }
            _sacredBowPreyRecordedPlayers.Clear();
        }

        [ScriptMethod(
            name: "封印解除",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41538|41537)$"]
        )]
        public void UnsealAlert(Event @event, ScriptAccessory accessory)
        {
            var ActionId = @event.ActionId;
            if (ActionId == 41538) //枪
            {
                if(EnableTextBanner) accessory.Method.TextInfo("枪，远平A", 5000);
                if (EnableTTS)
                {
                    accessory.Method.EdgeTTS("远平A，3下");
                }
            }
            else
            {
                if(EnableTextBanner) accessory.Method.TextInfo("斧，近平A", 5000);
                if (EnableTTS)
                {
                    accessory.Method.EdgeTTS("近平A，3下");
                }
            }
        }
        [ScriptMethod(
            name: "两岐之怒",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41573"]
        )]
        public void OnForkedFuryAlert(Event @event, ScriptAccessory accessory)
        {
            if(EnableTextBanner) accessory.Method.TextInfo("远近死刑", 5000);
            if (EnableTTS)
            {
                accessory.Method.EdgeTTS("远近死刑，然后两下平A");
            }
        }
        [ScriptMethod(
            name: "暗杀短剑",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41569"] 
        )]
        public void AssassinsDagger(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;


            var directionVector = @event.EffectPosition - caster.Position;
            var initialAngle = MathF.Atan2(directionVector.X, directionVector.Z);
            var distance = directionVector.Length();
            var rotationOffset = -50 * MathF.PI / 180.0f;

            for (int i = 0; i < 6; i++)
            {
                var currentAngle = initialAngle + i * rotationOffset;
                var delay = 1100 + (long)(i * 3900);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"AssassinsDagger_{i}";
                dp.Position = caster.Position;
                dp.Scale = new Vector2(6, distance);
                dp.Rotation = currentAngle;
                dp.Delay = delay;
                dp.DestoryAt = 6100;
                dp.Color = accessory.Data.DefaultDangerColor;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }

        [ScriptMethod(
            name: "致命枪斧组合AOE",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41547|41543)$"]
        )]
        public void CriticalBlow(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            bool isLance = @event.ActionId == 41547; // 枪

            var mainShapeDuration = isLance ? 6400 : 6100;
            var squareColor = isLance ? accessory.Data.DefaultDangerColor : new Vector4(0f, 0.6f, 0f, 0.8f);

            // 绘制主AOE (月环或钢铁)
            if (isLance)
            {
                var dpDonut = accessory.Data.GetDefaultDrawProperties();
                dpDonut.Name = "CriticalLanceblow_Donut";
                dpDonut.Owner = caster.EntityId;
                dpDonut.Scale = new Vector2(32);
                dpDonut.InnerScale = new Vector2(10);
                dpDonut.Radian = 2f * MathF.PI;
                dpDonut.Color = accessory.Data.DefaultDangerColor;
                dpDonut.DestoryAt = mainShapeDuration;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dpDonut);
                // 为枪击绘制指路
                var player = accessory.Data.MyObject;
                if (player != null)
                {
                    Vector3 closestPos = CriticalLanceSafePositions[0];
                    float minDistanceSq = Vector3.DistanceSquared(player.Position, closestPos);
                    for (int i = 1; i < CriticalLanceSafePositions.Count; i++)
                    {
                        float distSq = Vector3.DistanceSquared(player.Position, CriticalLanceSafePositions[i]);
                        if (distSq < minDistanceSq)
                        {
                            minDistanceSq = distSq;
                            closestPos = CriticalLanceSafePositions[i];
                        }
                    }
                }
                if (EnableTTS)
                {
                    accessory.Method.EdgeTTS("月环");
                }
            }
            else // 斧击
            {
                var dpCircle = accessory.Data.GetDefaultDrawProperties();
                dpCircle.Name = "CriticalAxeblow_Circle";
                dpCircle.Owner = caster.EntityId;
                dpCircle.Scale = new Vector2(20);
                dpCircle.Color = new Vector4(1f, 0f, 0f, 1.5f);
                dpCircle.DestoryAt = mainShapeDuration;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);
                // 为斧击绘制指路
                var player = accessory.Data.MyObject;
                if (player != null)
                {
                    Vector3 closestPos = CriticalAxeSafePositions[0];
                    float minDistanceSq = Vector3.DistanceSquared(player.Position, closestPos);
                    for (int i = 1; i < CriticalAxeSafePositions.Count; i++)
                    {
                        float distSq = Vector3.DistanceSquared(player.Position, CriticalAxeSafePositions[i]);
                        if (distSq < minDistanceSq)
                        {
                            minDistanceSq = distSq;
                            closestPos = CriticalAxeSafePositions[i];
                        }
                    }
                }
                if (EnableTTS)
                {
                    accessory.Method.EdgeTTS("钢铁");
                }
            }
            // 绘制三个方形AOE
            for (int i = 0; i < SquarePositions.Count; i++)
            {
                var dpSquare = accessory.Data.GetDefaultDrawProperties();
                dpSquare.Name = $"Square_AOE_{i}";
                dpSquare.Position = SquarePositions[i];
                dpSquare.Rotation = SquareAngles[i];
                dpSquare.Scale = new Vector2(20, 20);
                dpSquare.Color = squareColor;
                dpSquare.DestoryAt = mainShapeDuration;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dpSquare);
            }
        }
        /*
        [ScriptMethod(
            name: "致命枪斧（指路）",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41547|41543)$"]
        )]
        public void CriticalBlowGuide(Event @event, ScriptAccessory accessory)
        {
            var player = accessory.Data.MyObject;
            if (player == null) return;

            bool isLance = @event.ActionId == 41547; // 枪
            var safePositions = isLance ? CriticalLanceSafePositions : CriticalAxeSafePositions;
            var duration = isLance ? 6400 : 6100;

            Vector3 closestPos = safePositions[0];
            float minDistanceSq = Vector3.DistanceSquared(player.Position, closestPos);
            for (int i = 1; i < safePositions.Count; i++)
            {
                float distSq = Vector3.DistanceSquared(player.Position, safePositions[i]);
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    closestPos = safePositions[i];
                }
            }

            var dpGuide = accessory.Data.GetDefaultDrawProperties();
            dpGuide.Name = isLance ? "CriticalLance_Guide" : "CriticalAxe_Guide";
            dpGuide.Owner = player.EntityId;
            dpGuide.TargetPosition = closestPos;
            dpGuide.Scale = new Vector2(1.5f);
            dpGuide.ScaleMode |= ScaleMode.YByDistance;
            dpGuide.Color = accessory.Data.DefaultSafeColor;
            dpGuide.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpGuide);
        }
        */
        [ScriptMethod(
            name: "大斧猎物 (9秒)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:4337"]
        )]
        public void GreatAxePrey(Event @event, ScriptAccessory accessory)
        {
            if (PoliceMode)
            {
                if (float.TryParse(@event["Duration"], out var duration1) && Math.Abs(duration1 - 9.0f) < 0.1f)
                {
                    var target = accessory.Data.Objects.SearchById(@event.TargetId);
                    if (target != null)
                    {
                        accessory.Method.SendChat($"/e 大圈（9秒）点名: {target.Name}");
                    }
                }
            }
            // 确认是自己获得了这个状态
            if (@event.TargetId != accessory.Data.Me) return;

            // 检查buff持续时间是否为9秒
            if (float.TryParse(@event["Duration"], out var duration) && Math.Abs(duration - 9.0f) < 0.1f)
            {
                var player = accessory.Data.MyObject;
                if (player == null) return;

                // 找到最近的坐标点
                Vector3 closestPos = GreataxePreyPositions[0];
                float minDistanceSq = Vector3.DistanceSquared(player.Position, closestPos);

                for (int i = 1; i < GreataxePreyPositions.Count; i++)
                {
                    float distSq = Vector3.DistanceSquared(player.Position, GreataxePreyPositions[i]);
                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        closestPos = GreataxePreyPositions[i];
                    }
                }

                // 绘制指路
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "GreataxePrey_Guide";
                dp.Owner = player.EntityId;
                dp.TargetPosition = closestPos;
                dp.Scale = new Vector2(1.5f);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.DestoryAt = 9000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }
        [ScriptMethod(
            name: "大斧猎物 (21秒)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:4337"]
        )]
        public async void GreatAxePreyLong(Event @event, ScriptAccessory accessory)
        {
            if (PoliceMode)
            {
                CheckPreyPosition(accessory, @event.TargetId);
                if (float.TryParse(@event["Duration"], out var duration1) && Math.Abs(duration1 - 21.0f) < 0.1f)
                {
                    var target = accessory.Data.Objects.SearchById(@event.TargetId);
                    if (target != null)
                    {
                        accessory.Method.SendChat($"/e 大圈（21秒）点名: {target.Name}");
                    }
                }
            }
            // 确认是自己获得了这个状态
            if (@event.TargetId != accessory.Data.Me) return;

            // 检查buff持续时间是否为9秒
            if (float.TryParse(@event["Duration"], out var duration) && Math.Abs(duration - 21.0f) < 0.1f)
            {
                var player = accessory.Data.MyObject;
                if (player == null) return;
                await Task.Delay(15000);
                // 找到最近的坐标点
                Vector3 closestPos = GreataxePreyPositions[0];
                float minDistanceSq = Vector3.DistanceSquared(player.Position, closestPos);

                for (int i = 1; i < GreataxePreyPositions.Count; i++)
                {
                    float distSq = Vector3.DistanceSquared(player.Position, GreataxePreyPositions[i]);
                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        closestPos = GreataxePreyPositions[i];
                    }
                }

                // 绘制指路
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "GreataxePrey_Guide";
                dp.Owner = player.EntityId;
                dp.TargetPosition = closestPos;
                dp.Scale = new Vector2(1.5f);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.DestoryAt = 6000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }
        [ScriptMethod(
            name: "小斧猎物",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:4336"]
        )]
        public void LesserAxePrey(Event @event, ScriptAccessory accessory)
        {
            if (PoliceMode)
            {
                CheckPreyPosition(accessory, @event.TargetId);
                if (float.TryParse(@event["Duration"], out var duration1) && Math.Abs(duration1 - 13.0f) < 0.1f)
                {
                    var target = accessory.Data.Objects.SearchById(@event.TargetId);
                    if (target != null)
                    {
                        accessory.Method.SendChat($"/e 小圈（13秒）点名: {target.Name}");
                    }
                }
                else if (Math.Abs(duration1 - 21.0f) < 0.1f)
                {
                    var target = accessory.Data.Objects.SearchById(@event.TargetId);
                    if (target != null)
                    {
                        accessory.Method.SendChat($"/e 小圈（21秒）点名: {target.Name}");
                    }
                }
            }
            // 确认是自己获得了这个状态
            if (@event.TargetId != accessory.Data.Me) return;
            var player = accessory.Data.MyObject;
            if (float.TryParse(@event["Duration"], out var duration))
            {
                // 检查buff持续时间是否为13秒
                if (Math.Abs(duration - 13.0f) < 0.1f)
                {
                    // 找到最近的坐标点
                    Vector3 closestPos = SquarePositions[0];
                    float minDistanceSq = Vector3.DistanceSquared(player.Position, closestPos);

                    for (int i = 1; i < SquarePositions.Count; i++)
                    {
                        float distSq = Vector3.DistanceSquared(player.Position, SquarePositions[i]);
                        if (distSq < minDistanceSq)
                        {
                            minDistanceSq = distSq;
                            closestPos = SquarePositions[i];
                        }
                    }                    
                    // 绘制指路
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = "LesseraxePrey_Guide";
                    dp.Owner = player.EntityId;
                    dp.TargetPosition = closestPos;
                    dp.Scale = new Vector2(1.5f);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 13000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);                    
                    
                    /*
                    // 在三个方形AOE中心点绘制绿色安全圈
                    for (int i = 0; i < SquarePositions.Count; i++)
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"LittleAxePrey_SafeZone_13s_{i}";
                        dp.Position = SquarePositions[i];
                        dp.Scale = new Vector2(3);
                        dp.Color = new Vector4(0, 1, 0, 1);
                        dp.DestoryAt = 13000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
                    }
                    */
                }
                // 检查buff持续时间是否为21秒
                else if (Math.Abs(duration - 21.0f) < 0.1f)
                {
                    if (LongPointName)
                    {
                        var dp1 = accessory.Data.GetDefaultDrawProperties();
                        dp1.Name = "LittleAxePrey_SafeZone_21s_2";
                        dp1.Owner = player.EntityId;
                        dp1.TargetPosition = SquarePositions[1];
                        dp1.Scale = new Vector2(1.5f);
                        dp1.ScaleMode |= ScaleMode.YByDistance;
                        dp1.Color = accessory.Data.DefaultSafeColor;
                        dp1.Delay = 15000;
                        dp1.DestoryAt = 6000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
                        var dp3 = accessory.Data.GetDefaultDrawProperties();
                        dp3.Name = "LittleAxePrey_SafeZone_21s_3";
                        dp3.Owner = player.EntityId;
                        dp3.TargetPosition = SquarePositions[2];
                        dp3.Scale = new Vector2(1.5f);
                        dp3.ScaleMode |= ScaleMode.YByDistance;
                        dp3.Color = accessory.Data.DefaultSafeColor;
                        dp3.Delay = 15000;
                        dp3.DestoryAt = 6000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);
                    }
                    else
                    {
                        // 在第2个方形AOE中心点绘制绿色安全圈
                        var dp1 = accessory.Data.GetDefaultDrawProperties();
                        dp1.Name = "LittleAxePrey_SafeZone_21s_2";
                        dp1.Position = SquarePositions[1];
                        dp1.Scale = new Vector2(3);
                        dp1.Color = new Vector4(0, 1, 0, 1);
                        dp1.Delay = 15000;
                        dp1.DestoryAt = 6000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp1);

                        // 在第3个方形AOE中心点绘制绿色安全圈
                        var dp3 = accessory.Data.GetDefaultDrawProperties();
                        dp3.Name = "LittleAxePrey_SafeZone_21s_3";
                        dp3.Position = SquarePositions[2];
                        dp3.Scale = new Vector2(3);
                        dp3.Color = new Vector4(0, 1, 0, 1);
                        dp3.Delay = 15000;
                        dp3.DestoryAt = 6000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp3);
                    }
                }
            }
        }
        /*
        [ScriptMethod(
            name: "圣枪分摊点名",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:4338"],
            userControl: false)]
        public void SacredBowPrey(Event @event, ScriptAccessory accessory)
        {
            if (PoliceMode)
            {
                CheckPreyPosition(accessory, @event.TargetId);
                if (float.TryParse(@event["Duration"], out var duration1) && Math.Abs(duration1 - 17.0f) < 0.1f)
                {
                    var target = accessory.Data.Objects.SearchById(@event.TargetId);
                    if (target != null)
                    {
                        accessory.Method.SendChat($"/e 圣枪分摊(17秒）点名: {target.Name}");
                    }
                }
                else if (Math.Abs(duration1 - 25.0f) < 0.1f)
                {
                    var target = accessory.Data.Objects.SearchById(@event.TargetId);
                    if (target != null)
                    {
                        accessory.Method.SendChat($"/e 圣枪分摊(25秒）点名: {target.Name}");
                    }
                }
                else if (Math.Abs(duration1 - 33.0f) < 0.1f)
                {
                    var target = accessory.Data.Objects.SearchById(@event.TargetId);
                    if (target != null)
                    {
                        accessory.Method.SendChat($"/e 圣枪分摊(33秒）点名: {target.Name}");
                    }
                }
            }
        }
        */
        [ScriptMethod(
            name: "圣枪（指路）",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41557"]
        )]
        public void HolyLanceGuide(Event @event, ScriptAccessory accessory)
        {
            lock (_sacredBowPreyLock)
            {
                _sacredBowPreyRecordedPlayers.Clear(); // 新一轮圣枪机制开始，清空记录
            }
            var path = new List<DisplacementContainer>();
            // 覆盖逻辑：仅当选择了“左上/右上/下”时，强制使用 A/B/C 的路径块；
            // 选择 None 时，保持原有 SelectedStrategy 的完整分支，不做任何改动。
            if (HolyLanceGroupOverride != LanceGuideOverride.None)
            {
                TeamSelection forcedTeam = TeamSelection.A;
                switch (HolyLanceGroupOverride)
                {
                    case LanceGuideOverride.左上: forcedTeam = TeamSelection.A; break;
                    case LanceGuideOverride.右上: forcedTeam = TeamSelection.B; break;
                    case LanceGuideOverride.下:   forcedTeam = TeamSelection.C; break;
                }

                switch (forcedTeam)
                {
                    case TeamSelection.A:
                        path.Add(new DisplacementContainer(SquarePositions[2], 0, 10000));
                        path.Add(new DisplacementContainer(CriticalLanceSafePositions[1], 0, 5000));
                        path.Add(new DisplacementContainer(SquarePositions[2], 0, 17000));
                        path.Add(new DisplacementContainer(RectSideInA, 0, 3000));
                        path.Add(new DisplacementContainer(RectSideOutA, 0, 6000));
                        break;
                    case TeamSelection.B:
                        path.Add(new DisplacementContainer(SquarePositions[1], 0, 10000));
                        path.Add(new DisplacementContainer(CriticalLanceSafePositions[2], 0, 5000));
                        path.Add(new DisplacementContainer(RectSideInB, 0, 4000));
                        path.Add(new DisplacementContainer(RectSideOutB, 0, 6000));
                        path.Add(new DisplacementContainer(SquarePositions[1], 0, 14000));
                        break;
                    case TeamSelection.C:
                        path.Add(new DisplacementContainer(SquarePositions[0], 0, 10000));
                        path.Add(new DisplacementContainer(CriticalLanceSafePositions[0], 0, 5000));
                        path.Add(new DisplacementContainer(SquarePositions[0], 0, 9000));
                        path.Add(new DisplacementContainer(RectSideInC, 0, 3000));
                        path.Add(new DisplacementContainer(RectSideOutC, 0, 6000));
                        path.Add(new DisplacementContainer(SquarePositions[0], 0, 6000));
                        break;
                }
            }
            else
            {
                // None：保留原有策略分支（SelectedStrategy 完整生效）
                // 根据分组确定路径
                switch (SelectedStrategy)
                {
                    case StrategySelection.ABC_123:
                        switch (MyTeam)
                        {
                            case TeamSelection.A:
                            case TeamSelection.One:
                                path.Add(new DisplacementContainer(SquarePositions[2], 0, 10000));
                                path.Add(new DisplacementContainer(CriticalLanceSafePositions[1], 0, 5000));
                                path.Add(new DisplacementContainer(SquarePositions[2], 0, 17000));
                                path.Add(new DisplacementContainer(RectSideInA, 0, 3000));
                                path.Add(new DisplacementContainer(RectSideOutA, 0, 6000));
                                break;
                            case TeamSelection.B:
                            case TeamSelection.Two:
                                path.Add(new DisplacementContainer(SquarePositions[1], 0, 10000));
                                path.Add(new DisplacementContainer(CriticalLanceSafePositions[2], 0, 5000));
                                path.Add(new DisplacementContainer(RectSideInB, 0, 4000));
                                path.Add(new DisplacementContainer(RectSideOutB, 0, 6000));
                                path.Add(new DisplacementContainer(SquarePositions[1], 0, 14000));
                                break;
                            case TeamSelection.C:
                            case TeamSelection.Three:
                                path.Add(new DisplacementContainer(SquarePositions[0], 0, 10000));
                                path.Add(new DisplacementContainer(CriticalLanceSafePositions[0], 0, 5000));
                                path.Add(new DisplacementContainer(SquarePositions[0], 0, 9000));
                                path.Add(new DisplacementContainer(RectSideInC, 0, 3000));
                                path.Add(new DisplacementContainer(RectSideOutC, 0, 6000));
                                path.Add(new DisplacementContainer(SquarePositions[0], 0, 6000));
                                break;
                        }
                        break;
                    case StrategySelection.Pos_152463:
                        switch (MyPosition)
                        {
                            case PositionSelection.Pos1:
                            case PositionSelection.Pos2:
                                path.Add(new DisplacementContainer(SquarePositions[2], 0, 10000));
                                path.Add(new DisplacementContainer(CriticalLanceSafePositions[1], 0, 5000));
                                path.Add(new DisplacementContainer(SquarePositions[2], 0, 17000));
                                path.Add(new DisplacementContainer(RectSideInA, 0, 3000));
                                path.Add(new DisplacementContainer(RectSideOutA, 0, 6000));
                                break;
                            case PositionSelection.Pos5:
                            case PositionSelection.Pos6:
                                path.Add(new DisplacementContainer(SquarePositions[1], 0, 10000));
                                path.Add(new DisplacementContainer(CriticalLanceSafePositions[2], 0, 5000));
                                path.Add(new DisplacementContainer(RectSideInB, 0, 4000));
                                path.Add(new DisplacementContainer(RectSideOutB, 0, 6000));
                                path.Add(new DisplacementContainer(SquarePositions[1], 0, 14000));
                                break;
                            case PositionSelection.Pos3:
                            case PositionSelection.Pos4:
                                path.Add(new DisplacementContainer(SquarePositions[0], 0, 10000));
                                path.Add(new DisplacementContainer(CriticalLanceSafePositions[0], 0, 5000));
                                path.Add(new DisplacementContainer(SquarePositions[0], 0, 9000));
                                path.Add(new DisplacementContainer(RectSideInC, 0, 3000));
                                path.Add(new DisplacementContainer(RectSideOutC, 0, 6000));
                                path.Add(new DisplacementContainer(SquarePositions[0], 0, 6000));
                                break;
                        }
                        break;
                    case StrategySelection.LemonCookie:
                        switch (MyLemonCookiePosition)
                        {
                            case PositionSelection.Pos1:
                            case PositionSelection.Pos2:
                                path.Add(new DisplacementContainer(SquarePositions[2], 0, 10000));
                                path.Add(new DisplacementContainer(CriticalLanceSafePositions[1], 0, 5000));
                                path.Add(new DisplacementContainer(SquarePositions[2], 0, 17000));
                                path.Add(new DisplacementContainer(RectSideInA, 0, 3000));
                                path.Add(new DisplacementContainer(RectSideOutA, 0, 6000));
                                break;
                            case PositionSelection.Pos5:
                            case PositionSelection.Pos6:
                                path.Add(new DisplacementContainer(SquarePositions[1], 0, 10000));
                                path.Add(new DisplacementContainer(CriticalLanceSafePositions[2], 0, 5000));
                                path.Add(new DisplacementContainer(RectSideInB, 0, 4000));
                                path.Add(new DisplacementContainer(RectSideOutB, 0, 6000));
                                path.Add(new DisplacementContainer(SquarePositions[1], 0, 14000));
                                break;
                            case PositionSelection.Pos3:
                            case PositionSelection.Pos4:
                                path.Add(new DisplacementContainer(SquarePositions[0], 0, 10000));
                                path.Add(new DisplacementContainer(CriticalLanceSafePositions[0], 0, 5000));
                                path.Add(new DisplacementContainer(SquarePositions[0], 0, 9000));
                                path.Add(new DisplacementContainer(RectSideInC, 0, 3000));
                                path.Add(new DisplacementContainer(RectSideOutC, 0, 6000));
                                path.Add(new DisplacementContainer(SquarePositions[0], 0, 6000));
                                break;
                        }
                        break;                        
                }
            }

            if (path.Count > 0)
            {
                var props = new MultiDisDrawProp
                {
                    Color_GoNow = new Vector4(0, 1, 0, 1), // Green
                    Color_GoLater = new Vector4(1, 1, 0, 1), // Yellow
                    DrawMode = DrawModeEnum.Imgui
                };
                accessory.MultiDisDraw(path, props);
                if(Enable_Developer_Mode) accessory.Log.Debug($"圣枪机制：覆盖={HolyLanceGroupOverride}，策略={SelectedStrategy}。");
            }
        }
        [ScriptMethod(
            name: "神圣 - 记录武器",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:4339"],
            userControl: false
        )]
        public void OnSealMeltStatus(Event @event, ScriptAccessory accessory)
        {
            if (int.TryParse(@event["Param"], out int paramValue))
            {
                if (paramValue == 851)
                {
                    _holyWeaponType = HolyWeaponType.Axe;
                    if(Enable_Developer_Mode) accessory.Log.Debug("神圣机制：记录为斧头。");
                }
                else if (paramValue == 852)
                {
                    _holyWeaponType = HolyWeaponType.Lance;
                    if(Enable_Developer_Mode) accessory.Log.Debug("神圣机制：记录为长枪。");
                }
            }
        }

        [ScriptMethod(
            name: "灵气爆 - 提示",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41562"],
            suppress:2000
        )]
        public void OnHallowedPlumeCast(Event @event, ScriptAccessory accessory)
        {
            string hintText = "";
            switch (_holyWeaponType)
            {
                case HolyWeaponType.Axe:
                    hintText = "打黄色罐子";
                    break;
                case HolyWeaponType.Lance:
                    hintText = "打蓝色罐子";
                    break;
                default:
                    accessory.Log.Error("神圣机制：未能获取到武器类型。");
                    return;
            }
            if(EnableTextBanner) accessory.Method.TextInfo(hintText, 5000);
            if (EnableTTS)
            {
                accessory.Method.EdgeTTS(hintText);
            }

            // 重置状态
            _holyWeaponType = HolyWeaponType.None;
        }
        [ScriptMethod(
            name: "神圣 - 提示",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41563"],
            suppress: 2000
        )]
        public void OnHolyCast(Event @event, ScriptAccessory accessory)
        {
            string hintText = "";
            switch (_holyWeaponType)
            {
                case HolyWeaponType.Axe:
                    hintText = "打黄色罐子";
                    break;
                case HolyWeaponType.Lance:
                    hintText = "打蓝色罐子";
                    break;
                default:
                    accessory.Log.Error("神圣机制：未能获取到武器类型。");
                    return;
            }
            if(EnableTextBanner) accessory.Method.TextInfo(hintText, 5000);
            if (EnableTTS)
            {
                accessory.Method.EdgeTTS(hintText);
            }

            // 重置状态
            _holyWeaponType = HolyWeaponType.None;
        }
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
            }
            if (float.TryParse(@event["Duration"], out var duration))
            {
                int platformIndex = -1;
                lock (_lanceShareLock)
                {
                    bool alreadyRecorded = _lanceShareAssignments.Values.Any(list => list.Any(p => p.PlayerId == player.EntityId));
                    if (!alreadyRecorded)
                    {
                        for (int i = 0; i < SquarePositions.Count; i++)
                        {
                            if (IsPointInRotatedRect(player.Position, SquarePositions[i], 20, 20, SquareAngles[i]))
                            {
                                platformIndex = i;
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
                            if (Enable_Developer_Mode)
                            {
                                accessory.Log.Debug($"圣枪分摊记录: {player.Name.TextValue} 在平台 {platformIndex + 1}，持续时间 {duration:F2}s");
                            }
                        }
                        else
                        {
                            if (Enable_Developer_Mode)
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
                        if (IsPointInRotatedRect(player.Position, SquarePositions[i], 21, 21, SquareAngles[i]))
                        {
                            reportPlatformIndex = i;
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
            if (player == null) return; // 如果找不到该玩家（可能已离开范围），则不处理
        
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
        [ScriptMethod(
            name: "检查复活",
            eventType: EventTypeEnum.Chat,
            eventCondition: ["Type:regex:^(Echo|Party)$"]
        )]
        public async void CheckResurrection(Event @event, ScriptAccessory accessory)
        {
            string channel = @event["Type"].ToLower();
            if (!ReceivePartyCheckRequest && channel == "party") return;

            string message = @event["Message"];
            if (!message.StartsWith("复活检查")) return;
            string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            List<int> targetCounts = new();
            if (parts.Length > 1)
            {
                // 支持多个数字参数，如“复活检查 1 2 3”
                foreach (var part in parts.Skip(1))
                {
                    if (int.TryParse(part, out int c))
                        targetCounts.Add(c);
                }
            }
            else
            {
                // 默认检查0~3次
                targetCounts.AddRange(Enumerable.Range(0, 4));
            }
            var allResurrectionData = new List<Tuple<string, string, string, int>>();

            foreach (var gameObject in accessory.Data.Objects)
            {
                if (gameObject is IPlayerCharacter player)
                {
                    string playerName = player.Name.TextValue;
                    string classJob = player.ClassJob.Value.Name.ToString();
                    string supportJob = GetSupportJob(player);
                    int resurrectionCount = 0;

                    var resStatus = player.StatusList.FirstOrDefault(s => s.StatusId == 4262 || s.StatusId == 4263);
                    if (resStatus != null)
                    {
                        resurrectionCount = resStatus.Param;
                        allResurrectionData.Add(new Tuple<string, string, string, int>(playerName, classJob, supportJob, resurrectionCount));
                    }
                }
            }

            accessory.Method.SendChat($"/{channel} --- 开始复活检查 ---");
            foreach (var count in targetCounts)
            {
                await Task.Delay(200);
                await OutputResurrectionCheck(accessory, channel, allResurrectionData, count);
            }
        }
        private static async Task OutputResurrectionCheck(ScriptAccessory accessory, string channel, List<Tuple<string, string, string, int>> allResurrectionData, int targetCount)
        {
            var filteredData = allResurrectionData.Where(t => t.Item4 == targetCount).ToList();
            if (filteredData.Count > 0)
            {
                accessory.Method.SendChat($"/{channel} --- 复活次数为 {targetCount} 的玩家（共{filteredData.Count}人) ---");

                foreach (var data in filteredData)
                {
                    await Task.Delay(100);
                    accessory.Method.SendChat($"/{channel} {data.Item1} ({data.Item2} | {data.Item3})");
                }
            }
            else
            {
                accessory.Method.SendChat($"/{channel} --- 未找到复活次数为 {targetCount} 的玩家 ---");
            }
        }
        [ScriptMethod(
            name: "检查食物",
            eventType: EventTypeEnum.Chat,
            eventCondition: ["Type:regex:^(Echo|Party)$", "Message:食物检查"]
        )]
        public async void CheckFoodStatus(Event @event, ScriptAccessory accessory)
        {
            string channel = @event["Type"].ToLower();
            if (!ReceivePartyCheckRequest && channel == "party") return;

            int towerPlayerCount = 0;
            var foodStatusData = new List<Tuple<string, string, string, string>>();

            foreach (var gameObject in accessory.Data.Objects)
            {
                if (gameObject is IPlayerCharacter player
                    && player.HasStatusAny([4262, 4263])
                    )
                {
                    towerPlayerCount++;
                    string playerName = player.Name.TextValue;
                    string classJob = player.ClassJob.Value.Name.ToString();
                    string supportJob = GetSupportJob(player);

                    var foodStatus = player.StatusList.FirstOrDefault(s => s.StatusId == 48);

                    if(foodStatus == null)
                    {
                        foodStatusData.Add(new Tuple<string, string, string, string>(playerName, classJob, supportJob, "未进食"));
                    }
                    else if(foodStatus.RemainingTime <= FoodRemainingTimeThreshold * 60)
                    {
                        foodStatusData.Add(new Tuple<string, string, string, string>(playerName, classJob, supportJob, $"食物剩余时间不足{Math.Ceiling(foodStatus.RemainingTime / 60)}分钟"));
                    }
                }
            }

            accessory.Method.SendChat($"/{channel} --- 开始对塔内的{towerPlayerCount}名玩家进行食物检查 ---");

            if (foodStatusData.Count > 0)
            {
                var sortedData = foodStatusData.OrderBy(t => t.Item4).ToList();

                foreach (var data in sortedData)
                {
                    await Task.Delay(100);
                    accessory.Method.SendChat($"/{channel} {data.Item1} ({data.Item2} | {data.Item3}): {data.Item4}");
                }
            }
            else
            {
                await Task.Delay(100);
                accessory.Method.SendChat($"/{channel} 所有玩家均已进食");
            }
        }
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

            // 确保来源和目标都存在，且目标不是玩家
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
                await Task.Delay(100);
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

            // 修正：确保来源和目标都是玩家
            if (source == null || target == null || !(source is IPlayerCharacter) || !(target is IPlayerCharacter))
                return;

            string sourcePlayerName = source.Name.TextValue;
            string targetPlayerName = target.Name.TextValue;

            lock (_bluePotionLock)
            {
                // 外层Key是目标玩家，内层Key是来源玩家
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

                var partyMemberNames = Partycheck ? 
                    accessory.Data.PartyList.Select(id => accessory.Data.Objects.SearchById(id)?.Name.TextValue).Where(name => name != null).ToHashSet()
                    : null;

                sortedData = new Dictionary<string, List<KeyValuePair<string, int>>>();
                foreach (var bossEntry in _bluePotionCounts)
                {
                    var filteredPlayers = Partycheck ? 
                        bossEntry.Value.Where(kvp => partyMemberNames.Contains(kvp.Key) && partyMemberNames.Contains(bossEntry.Key)).ToList() 
                        : bossEntry.Value.ToList();

                    if(filteredPlayers.Count > 0)
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

        [ScriptMethod(
            name: "藏宝图",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["Operate:Add", "DataId:regex:^(1754|1755)$"]
        )]
        public void CheckTreasureChest(Event @event, ScriptAccessory accessory)
        {
            if(Enable_Developer_Mode) accessory.Log.Debug("找到箱子");
            //var chestid = @event.SourceId;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Chest";
            dp.Owner = accessory.Data.Me;
            dp.TargetObject = @event.SourceId;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Scale = new Vector2(1.5f);
            dp.DestoryAt = 120000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
        }

        [ScriptMethod(
            name: "宝箱移除",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["Operate:Remove", "DataId:regex:^(1754|1755)$"],
            userControl: false
        )]
        public void RemoveTreasureChest(Event @event, ScriptAccessory accessory)
        {
            //var chestid = @event.SourceId;
            accessory.Method.RemoveDraw("Chest");
        }
        [ScriptMethod(
            name: "标记药师",
            eventType: EventTypeEnum.Chat,
            eventCondition: ["Type:Echo"]
        )]
        public async void MarkChemists(Event @event, ScriptAccessory accessory)
        {
            if (@event["Message"] != "标记药师") return;
        
            if (Enable_Developer_Mode) accessory.Log.Debug("检测到'标记药师'指令...");
            accessory.Method.MarkClear();
            await Task.Delay(1000); // 等待标记清除
        
            var markType = MarkType.Attack1;
            int chemistsFound = 0;
        
            foreach (var gameObject in accessory.Data.Objects)
            {
                if (gameObject is IPlayerCharacter player)
                {
                    bool isChemist = false;
                    foreach (var status in player.StatusList)
                    {
                        if (status.StatusId == 4367) // 辅助药剂师 Status ID
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
        #region Helper_Functions

        private bool IsTank(IPlayerCharacter player)
        {
            if (player?.ClassJob.Value == null) return false;
            // 坦克的职业 Role ID 为 1
            return player.ClassJob.Value.Role == 1;
        }

        private bool IsHealer(IPlayerCharacter player)
        {
            if (player?.ClassJob.Value == null) return false;
            // 治疗的职业 Role ID 为 4
            return player.ClassJob.Value.Role == 4;
        }

        private bool IsDps(IPlayerCharacter player)
        {
            if (player?.ClassJob.Value == null) return false;
            // 只要不是坦克和治疗，就是DPS
            return !IsTank(player) && !IsHealer(player);
        }
        private Vector3 RotatePoint(Vector3 point, Vector3 center, float angleRad)
        {
            float s = MathF.Sin(angleRad);
            float c = MathF.Cos(angleRad);

            // Translate point back to origin
            point.X -= center.X;
            point.Z -= center.Z;

            // Rotate point
            float xnew = point.X * c - point.Z * s;
            float znew = point.X * s + point.Z * c;

            // Translate point back
            point.X = xnew + center.X;
            point.Z = znew + center.Z;
            return point;
        }
        #endregion


    }
    #region Helper_Classes_And_Methods

    public class DisplacementContainer
    {
        public Vector3 Pos;
        public long Delay;
        public long DestoryAt;

        public DisplacementContainer(Vector3 pos, long delay, long destoryAt)
        {
            Pos = pos;
            Delay = delay;
            DestoryAt = destoryAt;
        }
    }

    public class MultiDisDrawProp
    {
        public Vector4 Color_GoNow;
        public Vector4 Color_GoLater;
        public long BaseDelay;
        public float Width;
        public float EndCircleRadius;
        public DrawModeEnum DrawMode;

        public MultiDisDrawProp()
        {
            this.Color_GoNow = new(1, 1, 1, 1);
            this.Color_GoLater = new(0, 1, 1, 1);
            this.BaseDelay = 0;
            this.Width = 1.2f;
            this.EndCircleRadius = 0.65f;
            this.DrawMode = DrawModeEnum.Default;
        }
    }

    public static class HelperExtensions
    {
        internal static void MultiDisDraw(this ScriptAccessory accessory, List<DisplacementContainer> list, MultiDisDrawProp prop)
        {
            long startTimeMillis = prop.BaseDelay;
            const long preMs = 270;
            string guid = Guid.NewGuid().ToString();
            for (int i = 0; i < list.Count; i++)
            {
                int count = 0;
                DisplacementContainer dis = list[i];
                string name = $"_MultiDisDraw Part {i} : {guid} / ";

                // go now 直线引导部分
                DrawPropertiesEdit dp_goNowLine = accessory.Data.GetDefaultDrawProperties();
                dp_goNowLine.Name = name + count++;
                dp_goNowLine.Owner = (ulong)accessory.Data.Me;
                dp_goNowLine.Scale = new(prop.Width);
                dp_goNowLine.Delay = startTimeMillis;
                dp_goNowLine.DestoryAt = dis.DestoryAt;
                dp_goNowLine.ScaleMode |= ScaleMode.YByDistance;
                dp_goNowLine.TargetPosition = dis.Pos;
                dp_goNowLine.Color = prop.Color_GoNow;
                accessory.Method.SendDraw(prop.DrawMode, DrawTypeEnum.Displacement, dp_goNowLine);

                if (prop.EndCircleRadius > 0)
                {
                    DrawPropertiesEdit dp_goNowCircle = accessory.Data.GetDefaultDrawProperties();
                    dp_goNowCircle.Name = name + count++;
                    dp_goNowCircle.Position = dis.Pos;
                    dp_goNowCircle.Scale = new(prop.EndCircleRadius);
                    dp_goNowCircle.Delay = dp_goNowLine.Delay;
                    dp_goNowCircle.DestoryAt = dp_goNowLine.DestoryAt;
                    dp_goNowCircle.Color = prop.Color_GoNow;
                    accessory.Method.SendDraw(prop.DrawMode, DrawTypeEnum.Circle, dp_goNowCircle);
                }

                //如果当前点位不是最后一个点位，则进行go later部分
                if (i < list.Count - 1)
                {
                    DrawPropertiesEdit dp_goLaterLine = accessory.Data.GetDefaultDrawProperties();
                    dp_goLaterLine.Name = name + count++;
                    dp_goLaterLine.Position = list[i].Pos;
                    dp_goLaterLine.TargetPosition = list[i + 1].Pos;
                    dp_goLaterLine.Scale = new(prop.Width);
                    dp_goLaterLine.ScaleMode |= ScaleMode.YByDistance;
                    dp_goLaterLine.Delay = dp_goNowLine.Delay;
                    dp_goLaterLine.DestoryAt = dp_goNowLine.DestoryAt;
                    dp_goLaterLine.Color = prop.Color_GoLater;
                    accessory.Method.SendDraw(prop.DrawMode, DrawTypeEnum.Displacement, dp_goLaterLine);

                    if (prop.EndCircleRadius > 0)
                    {
                        DrawPropertiesEdit dp_goLaterCircle = accessory.Data.GetDefaultDrawProperties();
                        dp_goLaterCircle.Name = name + count++;
                        dp_goLaterCircle.Position = list[i + 1].Pos;
                        dp_goLaterCircle.Scale = new(prop.EndCircleRadius);
                        dp_goLaterCircle.Delay = dp_goLaterLine.Delay;
                        dp_goLaterCircle.DestoryAt = dp_goLaterLine.DestoryAt;
                        dp_goLaterCircle.Color = prop.Color_GoLater;
                        accessory.Method.SendDraw(prop.DrawMode, DrawTypeEnum.Circle, dp_goLaterCircle);
                    }
                }
                startTimeMillis += dis.DestoryAt;
            }
        }
    }

    #endregion
}
