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
    version: "0.0.2",
    author: "XSZYYS",
    note: "测试版，请选择自己小队的分组\r\n老一:\r\nAOE绘制：旋转，压溃\r\n指路：陨石点名（分摊自己看半场击退），第一次踩塔，第二次踩塔\r\n老二：\r\nAOE绘制：死刑，扇形"
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

        public void Init(ScriptAccessory accessory)
        {
            accessory.Log.Debug("力之塔脚本已加载。");
            accessory.Method.RemoveDraw(".*");

            // 初始化陨石机制状态
            _hasCometeorStatus = false;
            _cometeorTargetId = 0;
            _isCasterInUpperHalf = null;
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










        #endregion

    }
}
