using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;
using Dalamud.Interface.ManagedFontAtlas;
using ECommons;
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
using KodakkuAssist.Module.Draw.Manager;

namespace KodakkuAssistXSZYYS
{
    public enum TeamSelection
    {
        A,
        B,
        C,
        One,
        Two,
        Three
    }
    [ScriptType(
    name: "力之塔",
    guid: "874D3ECF-BD6B-448F-BB42-AE7F082E4805",
    territorys: [1252],
    version: "0.0.19",
    author: "XSZYYS",
    note: "测试版，请选择自己小队的分组，指路基于玉子烧攻略\r\n老一:\r\nAOE绘制：旋转，压溃\r\n指路：陨石点名，第一次踩塔，第二次踩塔\r\n老二：\r\nAOE绘制：死刑，扇形，冰火爆炸\r\n指路：雪球，火球\r\n老三：\r\nAOE绘制：龙态行动，冰圈，俯冲\r\n指路：龙态行动预站位，踩塔，小怪\r\n尾王：\r\nAOE绘制：致命斧/枪，暗杀短剑\r\n指路：符文之斧，圣枪"
    )]

    public class 力之塔
    {
        #region User_Settings_用户设置
        [UserSetting("-----全局设置----- (此设置无实际意义)")]
        public bool _____Global_Settings_____ { get; set; } = true;
        [UserSetting("请选择您在团队中被分配到的分组")]
        public TeamSelection MyTeam { get; set; } = TeamSelection.A;

        [UserSetting("-----开发者设置----- (此设置无实际意义)")]
        public bool _____Developer_Settings_____ { get; set; } = true;

        [UserSetting("启用开发者模式")]
        public bool Enable_Developer_Mode { get; set; } = false;

        #endregion

        // 用于老一的状态变量
        // 用于陨石机制的状态变量
        private bool _hasCometeorStatus = false;
        private ulong _cometeorTargetId = 0;
        private const uint PortentousCometeorDataId = 2014582;
        private const float ArenaCenterZ = 379f; // 定义老一场地中心Z轴坐标
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
        private static readonly Dictionary<TeamSelection, List<Vector3>> TowerPositions = new()
        {
            { TeamSelection.A, new List<Vector3> { new(-346.00f, -840.00f, 151.00f), new(-343.00f, -840.00f, 148.00f), new(-355.5f, -840.0f, 138.5f), new(-337.0f, -840.0f, 131.0f) } },
            { TeamSelection.B, new List<Vector3> { new(-337.00f, -840.00f, 151.00f), new(-343.00f, -840.00f, 157.00f), new(-337.0f, -840.0f, 131.0f), new(-318.5f, -840.0f, 138.5f) } },
            { TeamSelection.C, new List<Vector3> { new(-328.00f, -840.00f, 151.00f), new(-331.00f, -840.00f, 148.00f), new(-318.5f, -840.0f, 138.5f), new(-311.0f, -840.0f, 157.0f) } },
            { TeamSelection.One, new List<Vector3> { new(-328.00f, -840.00f, 163.00f), new(-331.00f, -840.00f, 166.00f), new(-318.5f, -840.0f, 175.5f), new(-337.0f, -840.0f, 183.0f) } },
            { TeamSelection.Two, new List<Vector3> { new(-337.00f, -840.00f, 163.00f), new(-331.00f, -840.00f, 157.00f), new(-337.0f, -840.0f, 183.0f), new(-355.5f, -840.0f, 175.5f) } },
            { TeamSelection.Three, new List<Vector3> { new(-346.00f, -840.00f, 163.00f), new(-343.00f, -840.00f, 166.00f), new(-355.5f, -840.0f, 175.5f), new(-363.0f, -840.0f, 157.0f) } }
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
        private static readonly Vector3[] SquarePositions =
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
        public void Init(ScriptAccessory accessory)
        {
            accessory.Log.Debug("力之塔脚本已加载。");
            accessory.Method.RemoveDraw(".*");

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
            // 清除之前的绘制
            accessory.Method.RemoveDraw(".*");
            accessory.Log.Debug("老一初始化完成。");
        }



        [ScriptMethod(
            name: "降落",
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
            accessory.Method.TextInfo("击退", 5000);

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
            switch (MyTeam)
            {
                case TeamSelection.A:
                    targetPosition = new Vector3(704.49f, -481.01f, 365.38f);
                    break;
                case TeamSelection.B:
                    targetPosition = new Vector3(699.98f, -481.01f, 355.49f);
                    break;
                case TeamSelection.C:
                    targetPosition = new Vector3(695.49f, -481.01f, 365.38f);
                    break;
                case TeamSelection.One:
                    targetPosition = new Vector3(695.49f, -481.01f, 392.60f);
                    break;
                case TeamSelection.Two:
                    targetPosition = new Vector3(699.98f, -481.01f, 402.49f);
                    break;
                case TeamSelection.Three:
                    targetPosition = new Vector3(704.49f, -481.01f, 392.60f);
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
        }




        [ScriptMethod(
            name: "陨石1(指路)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:4354"]
        )]
        public void OnCometeorStatusAdd(Event @event, ScriptAccessory accessory)
        {
            if (@event.TargetId != accessory.Data.Me) return;
            _hasCometeorStatus = true;
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
            Vector3 targetPosition;

            // 根据队伍选择是字母队还是数字队的目标点
            if (MyTeam == TeamSelection.A || MyTeam == TeamSelection.B || MyTeam == TeamSelection.C)
            {
                targetPosition = new Vector3(700.24f, -481.00f, 360.46f);
            }
            else // One, Two, Three
            {
                targetPosition = new Vector3(700.02f, -481.00f, 398.08f);
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
        public void AbcTeamHalfArenaGuide(Event @event, ScriptAccessory accessory)
        {
            // 检查玩家分组是否为A, B, C
            if (MyTeam != TeamSelection.A && MyTeam != TeamSelection.B && MyTeam != TeamSelection.C)
                return;

            // 检查是否已记录半场信息
            if (_isCasterInUpperHalf == null)
            {
                if (Enable_Developer_Mode) accessory.Log.Error("字母队指路: 未能获取到之前的半场信息。");
                return;
            }

            Vector3 targetPosition = new Vector3();

            // 根据玩家分组和记录的半场位置，选择目标坐标
            switch (MyTeam)
            {
                case TeamSelection.A:
                    targetPosition = _isCasterInUpperHalf.Value ? Pos_One : Pos_A;
                    break;
                case TeamSelection.B:
                    targetPosition = _isCasterInUpperHalf.Value ? Pos_Two : Pos_B;
                    break;
                case TeamSelection.C:
                    targetPosition = _isCasterInUpperHalf.Value ? Pos_Three : Pos_C;
                    break;
            }

            // 绘制指路
            var dpGuide = accessory.Data.GetDefaultDrawProperties();
            dpGuide.Name = $"Abc_Team_Guide_Arrow_{MyTeam}";
            dpGuide.Owner = accessory.Data.Me;
            dpGuide.TargetPosition = targetPosition;
            dpGuide.Scale = new Vector2(1.5f);
            dpGuide.ScaleMode |= ScaleMode.YByDistance;
            dpGuide.Color = accessory.Data.DefaultSafeColor;
            dpGuide.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpGuide);

            // 在目标点绘制绿色圆圈
            var dpCircle = accessory.Data.GetDefaultDrawProperties();
            dpCircle.Name = $"Abc_Team_Guide_Circle_{MyTeam}";
            dpCircle.Position = targetPosition;
            dpCircle.Scale = new Vector2(4);
            dpCircle.Color = new Vector4(0, 1, 0, 0.6f); // 绿色
            dpCircle.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dpCircle);

            if (Enable_Developer_Mode)
            {
                accessory.Log.Debug($"字母队指路: 队伍 {MyTeam}, 指向 {targetPosition}");
            }

            // 重置状态，为下一次机制做准备
            _isCasterInUpperHalf = null;
        }
        [ScriptMethod(
            name: "数字队地面塔(指路)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41713|41711)$"],
            suppress: 1000
        )]
        public void NumberTeamHalfArenaGuide(Event @event, ScriptAccessory accessory)
        {
            // 检查玩家分组是否为数字队
            if (MyTeam != TeamSelection.One && MyTeam != TeamSelection.Two && MyTeam != TeamSelection.Three)
                return;

            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            // 判断施法者在哪个半场
            bool isCasterInUpperHalf = caster.Position.Z > ArenaCenterZ;

            Vector3 targetPosition = new Vector3();

            // 根据玩家分组和施法者半场位置，选择目标坐标
            switch (MyTeam)
            {
                case TeamSelection.One:
                    targetPosition = isCasterInUpperHalf ? Pos_One : Pos_A;
                    break;
                case TeamSelection.Two:
                    targetPosition = isCasterInUpperHalf ? Pos_Two : Pos_B;
                    break;
                case TeamSelection.Three:
                    targetPosition = isCasterInUpperHalf ? Pos_Three : Pos_C;
                    break;
            }

            // 绘制指路
            var dpGuide = accessory.Data.GetDefaultDrawProperties();
            dpGuide.Name = $"Number_Team_Guide_Arrow_{MyTeam}";
            dpGuide.Owner = accessory.Data.Me;
            dpGuide.TargetPosition = targetPosition;
            dpGuide.Scale = new Vector2(1.5f);
            dpGuide.ScaleMode |= ScaleMode.YByDistance;
            dpGuide.Color = accessory.Data.DefaultSafeColor;
            dpGuide.DestoryAt = 21000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpGuide);

            // 在目标点绘制绿色圆圈
            var dpCircle = accessory.Data.GetDefaultDrawProperties();
            dpCircle.Name = $"Number_Team_Guide_Circle_{MyTeam}";
            dpCircle.Position = targetPosition;
            dpCircle.Scale = new Vector2(4);
            dpCircle.Color = new Vector4(0, 1, 0, 0.6f); // 绿色
            dpCircle.DestoryAt = 21000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dpCircle);

            if (Enable_Developer_Mode)
            {
                accessory.Log.Debug($"数字队指路: 队伍 {MyTeam}, 施法者在 {(isCasterInUpperHalf ? "上半场" : "下半场")}, 指向 {targetPosition}");
            }
        }


        [ScriptMethod(
            name: "浮空(指路)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41707"]
        )]
        public void AbcTeamSafeZone(Event @event, ScriptAccessory accessory)
        {
            // 检查玩家分组是否为A, B, C
            if (MyTeam != TeamSelection.A && MyTeam != TeamSelection.B && MyTeam != TeamSelection.C)
                return;

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
                accessory.Log.Debug("为字母队绘制安全区。");
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
            accessory.Log.Debug("老二初始化完成。");
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
                dpBlue.Color = new Vector4(0.2f, 0.5f, 1f, 0.6f);
                dpBlue.Delay = delay;
                dpBlue.DestoryAt = displayDuration;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpBlue);

                // 绘制红色圈
                var dpRed = accessory.Data.GetDefaultDrawProperties();
                dpRed.Name = $"PrimordialChaos_Red_{_pairsProcessed}";
                dpRed.Position = redPos;
                dpRed.Scale = new Vector2(22);
                dpRed.Color = new Vector4(1f, 0.2f, 0.2f, 0.6f);
                dpRed.Delay = delay;
                dpRed.DestoryAt = displayDuration;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpRed);

                accessory.Log.Debug($"绘制第 {_pairsProcessed + 1} 对冰火圈");

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
                        accessory.Log.Debug("雪球狂奔: 字母组已确定。");
                    }
                    else
                    {
                        isLetterGroup = false;
                        _numberGroupNextPos = nextPos;
                        currentGroupRushCount = _numberGroupRushCount++;
                        accessory.Log.Debug("雪球狂奔: 数字组已确定。");
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
                        accessory.Log.Debug("雪球狂奔: 字母组路径更新。");
                    }
                    else if (_numberGroupNextPos.HasValue && Vector3.DistanceSquared(sourcePos, _numberGroupNextPos.Value) < 1.0f)
                    {
                        isLetterGroup = false;
                        _numberGroupNextPos = nextPos;
                        currentGroupRushCount = _numberGroupRushCount++;
                        accessory.Log.Debug("雪球狂奔: 数字组路径更新。");
                    }
                    else
                    {
                        // Fallback in case something goes wrong
                        accessory.Log.Error("雪球狂奔: 无法匹配到路径。");
                        return;
                    }
                }

                // --- 颜色判断逻辑 ---
                bool isSafe = false;
                // currentGroupRushCount 是从0开始计数的 (0=第1次, 1=第2次, 2=第3次)
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
            if (@event.TargetId == accessory.Data.Me)
            {
                _tetherSourceId = @event.SourceId;
                accessory.Log.Debug($"凝冰冲击: 玩家被连线，来源ID: {_tetherSourceId}");
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
            accessory.Log.Debug("凝冰冲击: 开始读条，触发指路绘制。");
            DrawGlacialImpactGuide(accessory);
        }



        private void DrawGlacialImpactGuide(ScriptAccessory accessory)
        {
            Vector3? safePosition = null;

            // 优先处理连线情况
            if (_tetherSourceId != 0)
            {
                var tetherSource = accessory.Data.Objects.SearchById(_tetherSourceId);
                if (tetherSource != null)
                {
                    var direction = Vector3.Normalize(SnowballArenaCenter - tetherSource.Position);
                    safePosition = SnowballArenaCenter + direction * 5;
                    accessory.Log.Debug("凝冰冲击: 检测到连线，计算特殊安全点。");
                }
                else
                {
                    accessory.Log.Error("凝冰冲击: 找不到连线来源单位。");
                }
            }
            else // 如果没有被连线，则执行分组逻辑
            {
                bool isUserInLetterGroup = MyTeam == TeamSelection.A || MyTeam == TeamSelection.B || MyTeam == TeamSelection.C;
                Vector3? finalDropPos = isUserInLetterGroup ? _letterGroupNextPos : _numberGroupNextPos;

                if (finalDropPos != null)
                {
                    var direction = Vector3.Normalize(finalDropPos.Value - SnowballArenaCenter);
                    safePosition = SnowballArenaCenter - direction * 5;
                }
                else
                {
                    accessory.Log.Error("凝冰冲击: 找不到雪球最终落点。");
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
            accessory.Log.Debug("凝冰冲击: 机制结束，重置连线状态。");
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
                accessory.Log.Error("火球重绘: 找不到第一轮的火球坐标。");
                return;
            }
            ProcessFireballs(accessory);
        }
        private void ProcessFireballs(ScriptAccessory accessory)
        {
            bool isUserInLetterGroup = MyTeam == TeamSelection.A || MyTeam == TeamSelection.B || MyTeam == TeamSelection.C;

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
            int myIndex = accessory.Data.PartyList.IndexOf(player.EntityId);
            if (myIndex == -1) return;

            var directionToFireball = Vector3.Normalize(fireballPos - SnowballArenaCenter);

            if (IsDps(myIndex))
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
            else if (IsHealer(myIndex))
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
            else if (IsTank(myIndex))
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
                accessory.Log.Error("地热破裂: 未能获取到火球位置信息。");
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
                dp1.DestoryAt = 4000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
                // 绘制从起点到90度点的路径（绿）
                var dp4 = accessory.Data.GetDefaultDrawProperties();
                dp4.Name = $"GeothermalRupture_Path1_{pathIndex}";
                dp4.Position = startPos;
                dp4.TargetPosition = point1;
                dp4.Scale = new Vector2(1.5f);
                dp4.ScaleMode |= ScaleMode.YByDistance;
                dp4.Color = new Vector4(0f, 1f, 0f, 1f);
                dp4.Delay = 4000;
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
                dp2.DestoryAt = 7000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
                // 绘制从90度点到180度点的路径（绿）
                var dp3 = accessory.Data.GetDefaultDrawProperties();
                dp3.Name = $"GeothermalRupture_Path2_{pathIndex}";
                dp3.Position = point1;
                dp3.TargetPosition = point2;
                dp3.Scale = new Vector2(1.5f);
                dp3.ScaleMode |= ScaleMode.YByDistance;
                dp3.Color = new Vector4(0f, 1f, 0f, 1f);
                dp3.Delay = 7000;
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
            accessory.Log.Debug("老三初始化完成。");
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

            // 获取用户选择的队伍对应的坐标列表
            if (TowerPositions.TryGetValue(MyTeam, out var teamTowerCoords))
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


            if (TowerPositions.TryGetValue(MyTeam, out var teamTowerCoords))
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


            foreach (var groupEntry in GroupMarkerPositions)
            {

                if (Vector3.DistanceSquared(marker.Position, groupEntry.Value) < 1.0f)
                {

                    if (groupEntry.Key == MyTeam)
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
            accessory.Log.Debug("尾王初始化完成。");
            _holyWeaponType = HolyWeaponType.None;
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
                
            }
            else // 斧击
            {
                var dpCircle = accessory.Data.GetDefaultDrawProperties();
                dpCircle.Name = "CriticalAxeblow_Circle";
                dpCircle.Owner = caster.EntityId;
                dpCircle.Scale = new Vector2(20);
                dpCircle.Color = new Vector4(1f, 0f, 0f, 1f);
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
            }
            // 绘制三个方形AOE
            for (int i = 0; i < SquarePositions.Length; i++)
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
            name: "小斧猎物 (13秒)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:4336"]
        )]
        public void LesserAxePrey(Event @event, ScriptAccessory accessory)
        {
            // 确认是自己获得了这个状态
            if (@event.TargetId != accessory.Data.Me) return;

            if (float.TryParse(@event["Duration"], out var duration))
            {
                // 检查buff持续时间是否为13秒
                if (Math.Abs(duration - 13.0f) < 0.1f)
                {
                    // 在三个方形AOE中心点绘制绿色安全圈
                    for (int i = 0; i < SquarePositions.Length; i++)
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"LittleAxePrey_SafeZone_13s_{i}";
                        dp.Position = SquarePositions[i];
                        dp.Scale = new Vector2(3);
                        dp.Color = new Vector4(0, 1, 0, 1);
                        dp.DestoryAt = 13000;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
                    }
                }
                // 检查buff持续时间是否为21秒
                else if (Math.Abs(duration - 21.0f) < 0.1f)
                {
                    // 在第1个方形AOE中心点绘制绿色安全圈
                    var dp1 = accessory.Data.GetDefaultDrawProperties();
                    dp1.Name = "LittleAxePrey_SafeZone_21s_1";
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
        [ScriptMethod(
            name: "圣枪（指路）",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41557"]
        )]
        public void HolyLanceGuide(Event @event, ScriptAccessory accessory)
        {
            var path = new List<DisplacementContainer>();

            // 根据分组确定路径
            switch (MyTeam)
            {
                case TeamSelection.A:
                case TeamSelection.One:
                    path.Add(new DisplacementContainer(SquarePositions[2], 0, 5000));
                    path.Add(new DisplacementContainer(CriticalLanceSafePositions[1], 0, 10000));
                    path.Add(new DisplacementContainer(SquarePositions[2], 0, 17000));
                    path.Add(new DisplacementContainer(RectSideInA, 0, 4000));
                    path.Add(new DisplacementContainer(RectSideOutA, 0, 6000));
                    break;
                case TeamSelection.B:
                case TeamSelection.Two:
                    path.Add(new DisplacementContainer(SquarePositions[1], 0, 5000));
                    path.Add(new DisplacementContainer(CriticalLanceSafePositions[2], 0, 10000));
                    path.Add(new DisplacementContainer(RectSideInB, 0, 5000));
                    path.Add(new DisplacementContainer(RectSideOutB, 0, 6000));
                    path.Add(new DisplacementContainer(SquarePositions[1], 0, 14000));
                    break;
                case TeamSelection.C:
                case TeamSelection.Three:
                    path.Add(new DisplacementContainer(SquarePositions[0], 0, 5000));
                    path.Add(new DisplacementContainer(CriticalLanceSafePositions[0], 0, 10000));
                    path.Add(new DisplacementContainer(SquarePositions[0], 0, 9000));
                    path.Add(new DisplacementContainer(RectSideInC, 0, 4000));
                    path.Add(new DisplacementContainer(RectSideOutC, 0, 6000));
                    path.Add(new DisplacementContainer(SquarePositions[0], 0, 6000));
                    break;
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
                accessory.Log.Debug($"圣枪机制触发：为 {MyTeam} 组绘制路径。");
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
                    accessory.Log.Debug("神圣机制：记录为斧头。");
                }
                else if (paramValue == 852)
                {
                    _holyWeaponType = HolyWeaponType.Lance;
                    accessory.Log.Debug("神圣机制：记录为长枪。");
                }
            }
        }

        [ScriptMethod(
            name: "神圣 - 提示",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41562"]
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

            accessory.Method.TextInfo(hintText, 5000);

            // 重置状态
            _holyWeaponType = HolyWeaponType.None;
        }
        #endregion
        #region Helper_Functions

        private bool IsTank(int partyIndex)
        {
            return partyIndex is 0 or 1;
        }

        private bool IsHealer(int partyIndex)
        {
            return partyIndex is 2 or 3;
        }

        private bool IsDps(int partyIndex)
        {
            return partyIndex is >= 4 and <= 7;
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
