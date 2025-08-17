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
    version: "0.0.3",
    author: "XSZYYS",
    note: "测试版，请选择自己小队的分组\r\n老一:\r\nAOE绘制：旋转，压溃\r\n指路：陨石点名（分摊自己看半场击退），第一次踩塔，第二次踩塔\r\n老二：\r\nAOE绘制：死刑，扇形，冰火爆炸\r\n指路：雪球机制，火球预站位"
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
        private static readonly Vector3 InitialPosLetterGroup = new(-800.00f, -876.00f, 349.50f);
        private static readonly Vector3 InitialPosNumberGroup = new(-809.09f, -876.00f, 365.25f);
        private static readonly Vector3 SnowballArenaCenter = new(-800.00f, -876.00f, 360.00f);
        private ulong _tetherSourceId = 0;
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
        }
        #region 老一
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
                    dp.Scale = new Vector2(30, 30);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = 10500;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
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
            accessory.Method.TextInfo("击退", 5000);
        }

        [ScriptMethod(
            name: "踩塔(指路)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41720"]
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
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
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
            eventCondition: ["ActionId:41707"]
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
            eventCondition: ["ActionId:regex:^(41713|41711)$"]
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
            name: "浮空",
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
            name: "雪球狂奔",
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
                dp.DestoryAt = 5000;
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
            name: "凝冰冲击 (雪球后站位)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:42451"]
        )]
        public void GlacialImpact(Event @event, ScriptAccessory accessory)
        {
            Vector3? safePosition = null;

            // 优先处理连线情况
            if (_tetherSourceId != 0)
            {
                var tetherSource = accessory.Data.Objects.SearchById(_tetherSourceId);
                if (tetherSource != null)
                {
                    var direction = Vector3.Normalize(SnowballArenaCenter - tetherSource.Position);
                    safePosition = SnowballArenaCenter - direction * 5;
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
                dp.DestoryAt = 4000;
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
            name: "火球预站位",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["Operate:Add", "DataId:2014637"]
        )]
        public void FireballPrePosition(Event @event, ScriptAccessory accessory)
        {
            var fireball = accessory.Data.Objects.SearchById(@event.SourceId);
            var player = accessory.Data.MyObject;
            if (fireball == null || player == null) return;

            int myIndex = accessory.Data.PartyList.IndexOf(player.EntityId);
            if (myIndex == -1) return;

            var fireballPos = fireball.Position;
            var directionToFireball = Vector3.Normalize(fireballPos - SnowballArenaCenter);

            if (IsDps(myIndex))
            {

                var safePos = fireballPos + directionToFireball * 6;
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Fireball_DPS_SafeZone_{fireball.EntityId}";
                dp.Position = safePos;
                dp.Scale = new Vector2(2);
                dp.Color = new Vector4(0, 1, 0, 0.8f);
                dp.DestoryAt = 18000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
            }
            else if (IsHealer(myIndex))
            {

                var perpendicularDir1 = new Vector3(-directionToFireball.Z, 0, directionToFireball.X);
                var perpendicularDir2 = new Vector3(directionToFireball.Z, 0, -directionToFireball.X);

                var safePos1 = fireballPos + perpendicularDir1 * 6;
                var safePos2 = fireballPos + perpendicularDir2 * 6;

                var dp1 = accessory.Data.GetDefaultDrawProperties();
                dp1.Name = $"Fireball_Healer_SafeZone1_{fireball.EntityId}";
                dp1.Position = safePos1;
                dp1.Scale = new Vector2(2);
                dp1.Color = new Vector4(0, 1, 0, 0.8f); 
                dp1.DestoryAt = 18000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp1);

                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Name = $"Fireball_Healer_SafeZone2_{fireball.EntityId}";
                dp2.Position = safePos2;
                dp2.Scale = new Vector2(2);
                dp2.Color = new Vector4(0, 1, 0, 0.8f); 
                dp2.DestoryAt = 18000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp2);
            }
            else if (IsTank(myIndex))
            {

                var directionToCenter = Vector3.Normalize(SnowballArenaCenter - fireballPos);
                var rotation = MathF.Atan2(directionToCenter.X, directionToCenter.Z);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"Fireball_Tank_SafeZone_{fireball.EntityId}";
                dp.Owner = fireball.EntityId;
                dp.Rotation = rotation;
                dp.Scale = new Vector2(4);
                dp.Radian = 15 * MathF.PI / 180.0f;
                dp.Color = new Vector4(0, 1, 0, 0.8f); 
                dp.DestoryAt = 18000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
            }
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

        #endregion
    }
}
