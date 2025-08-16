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
    version: "0.0.1",
    author: "XSZYYS",
    note: "测试版，请选择自己小队的分组\r\n老一:\r\nAOE绘制：旋转，压溃\r\n指路：陨石点名，第一次踩塔\r\n老二：\r\nAOE绘制：死刑，扇形"
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



        public void Init(ScriptAccessory accessory)
        {
            accessory.Log.Debug("力之塔脚本已加载。");
            accessory.Method.RemoveDraw(".*");

            // 初始化陨石机制状态
            _hasCometeorStatus = false;
            _cometeorTargetId = 0;
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
            dp.Scale = new Vector2(1.5f); // 箭头样式(宽度1.5)
            dp.ScaleMode |= ScaleMode.YByDistance; // 箭头长度根据距离自动调整
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.DestoryAt = 10200;
            dp2.Color = accessory.Data.DefaultSafeColor;
            dp2.Scale = new Vector2(4);
            dp2.DestoryAt = 10200;
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
