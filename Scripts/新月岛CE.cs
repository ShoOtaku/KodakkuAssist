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

namespace EurekaOrthosCeScripts
{
    [ScriptType(
        name: "新月岛CE",
        guid: "15725518-8F8E-413A-BEA8-E19CC861CF93",
        territorys: [1252],
        version: "0.0.17",
        author: "XSZYYS",
        note: "新月岛部分CE绘制已完成：\r\n死亡爪（地板出现小怪未绘制，其余均绘制）\r\n神秘土偶（全部画完）\r\n黑色连队（全部画完）\r\n水晶龙（全部画完）\r\n狂战士（全部画完）\r\n指令罐（全部画完）\r\n回廊恶魔（全部画完）\r\n未测试：\r\n进化加鲁拉（运动会未测试）\r\n鬼火苗（有问题，待修复）\r\n石质骑士团（转转手未写，地火未测试）\r\n复原狮\r\n鲨鱼\r\n未写：\r\n金钱龟\r\n跃立狮\r\n夺心魔"
    )]
    public class 新月岛CE
    {
        /// <summary>
        /// 脚本初始化
        /// </summary>
        // --- 进化加鲁拉 状态变量 ---
        private Vector3? _noiseComplaintArenaCenter;
        private bool _noiseComplaintCenterRecorded = false;
        private bool? _lightningIsCardinal;
        private readonly Queue<ulong> _activeBirds = new();
        private ulong _bossId = 0;
        private ulong _birdserkRushTargetId = 0;
        private ulong _rushingRumbleRampageTargetId = 0;
        private readonly int[] _rampageDelays = { 5200, 3200 };
        private readonly List<FlurryLine> _flurryLines = new();
        private int _activeMechanicId; // 41175 for Rumble, 41176 for Birdserk, 41177 for Rampage
        private Vector3 _chargeStartPosition; // 存储冲锋开始时的位置
        // --- “Rushing Rumble Rampage”连续冲锋机制专用状态变量 ---
        private bool _isRampageSequenceRunning = false;
        private int _rampageChargeIndex = 0;
        private Vector3 _rampageNextChargeStartPos;
        private readonly object _mechanicLock = new();
        private readonly object _surgeLock = new();
        private static readonly Vector3 SharkArenaCenter = new(-117f, 1, -850f);
        // 存储所有能量球的列表
        private readonly List<IGameObject> _spheres = new(12);
        // 专门存储“石”属性能量球的列表
        private readonly List<IGameObject> _spheresStone = new(6);
        // 专门存储“风”属性能量球的列表
        private readonly List<IGameObject> _spheresWind = new(6);
        private readonly List<(ulong ActorID, string DrawName)> _surgeAoes = new();
        private class PendingMechanic
        {
            public Vector3 Position { get; set; }
            public uint ShapeActionId { get; init; } // 用来区分是十字、月环还是击退
            public DateTime ActivationTime { get; init; }
        }

        private readonly List<PendingMechanic> _pendingMechanics = new(4);
        private bool _isFirstSequence = true;
        private readonly List<(string Name, int Delay)> _tidalGuillotineAoes = new(3);
        private readonly List<(Vector3 Position, float Radius, int Delay)> _openWaterAoes = new();
        private int _openWaterCastsDone = 0;
        private DateTime _openWaterCastStartTime;
        private class FlurryLine
        {
            public string ID { get; init; }
            public Vector3 NextExplosionPosition { get; set; }
            public Vector3 Direction { get; init; }
            public int ExplosionsLeft { get; set; }
            public float Radius { get; init; } = 6f;
            public float StepDistance { get; init; } = 7f;
            public int DelayMs { get; init; } = 1000;
        }
        public void Init(ScriptAccessory accessory)
        {
            accessory.Log.Debug("新月岛CE脚本已加载。");
            accessory.Method.RemoveDraw(".*"); // 清理旧的绘图
            // 初始化进化加鲁拉机制的状态
            //_noiseComplaintArenaCenter = null;
            //_noiseComplaintCenterRecorded = false;
            _lightningIsCardinal = null;
            _activeBirds.Clear();
            _tidalGuillotineAoes.Clear(); // 初始化时清空潮汐断头台列表
            _openWaterAoes.Clear(); // 初始化时清空旋转月环列表
            _openWaterCastsDone = 0;
            _openWaterCastStartTime = default;
        }

        // --- 黑色连队 ---


        [ScriptMethod(
            name: "陆行鸟冲 (黑色连队)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41163"]
        )]
        public void ChocoBeak(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "BlackRegiment_ChocoBeak_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(4, 70);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }


        [ScriptMethod(
            name: "陆行鸟乱羽 (黑色连队)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41164"]
        )]
        public void ChocoMaelfeather(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "BlackRegiment_ChocoMaelfeather_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(8);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "陆行鸟风暴 (黑色连队)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41147"]
        )]
        public void ChocoWindstorm(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "BlackRegiment_ChocoWindstorm_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(16);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 7000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "陆行鸟气旋 (黑色连队)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41148"]
        )]
        public void ChocoCyclone(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "BlackRegiment_ChocoCyclone_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(30, 30);
            dp.InnerScale = new Vector2(8, 8);
            dp.Radian = MathF.PI * 2;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }

        [ScriptMethod(
            name: "陆行鸟屠杀 (黑色连队)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41151"]
        )]
        public void ChocoSlaughterFirst(Event @event, ScriptAccessory accessory)
        {
            var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var srot = JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);

            var positions = new List<Vector3>();
            var currentPos = spos;
            for (int i = 0; i < 5; i++)
            {
                currentPos = new Vector3(
                    currentPos.X + (float)Math.Sin(srot) * 5,
                    currentPos.Y,
                    currentPos.Z + (float)Math.Cos(srot) * 5
                );
                positions.Add(currentPos);
            }

            const int firstExplosionTime = 5000;
            const int subsequentInterval = 1100;
            const int warningDuration = 2000;
            const int lingerDuration = 500;

            for (int i = 0; i < positions.Count; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"BlackRegiment_ChocoSlaughter_Danger_Zone_{i}";
                dp.Position = positions[i];
                dp.Scale = new Vector2(5);
                dp.Color = accessory.Data.DefaultDangerColor;

                int explosionTime = firstExplosionTime + ((i + 1) * subsequentInterval);

                dp.Delay = explosionTime - warningDuration;
                dp.DestoryAt = warningDuration + lingerDuration;
                dp.ScaleMode |= ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }

        [ScriptMethod(
            name: "神秘热量 (神秘土偶)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41137"]
        )]
        public void MysticHeat(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "FromTimesBygone_MysticHeat_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(40);
            dp.Radian = 60 * MathF.PI / 180.0f;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(
            name: "大爆炸 (神秘土偶)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41130"]
        )]
        public void BigBurst(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "FromTimesBygone_BigBurst_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(26);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 8000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }


        [ScriptMethod(
            name: "死亡射线 (神秘土偶)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41133"]
        )]
        public void DeathRay(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "FromTimesBygone_DeathRay_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(60);
            dp.Radian = 90 * MathF.PI / 180.0f;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 8000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(
            name: "钢铁之击 (神秘土偶)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41131"]
        )]
        public void Steelstrike(Event @event, ScriptAccessory accessory)
        {
            // 循环4次，每次旋转45度，绘制4条直线形成米字
            for (int i = 0; i < 4; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"FromTimesBygone_Steelstrike_Danger_Zone_{i}";
                dp.Owner = @event.SourceId;
                dp.Scale = new Vector2(10, 100);
                dp.Rotation = i * (MathF.PI / 4);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 8000;
                dp.ScaleMode |= ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
            }
        }

        [ScriptMethod(
            name: "奥术之球 (神秘土偶)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41135"]
        )]
        public void ArcaneOrbTelegraph(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);

            dp.Name = $"FromTimesBygone_ArcaneOrb_Danger_Zone_{pos.X}_{pos.Z}";
            dp.Position = pos;
            dp.Scale = new Vector2(6);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 3200;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        // --- 指令罐 ---


        [ScriptMethod(
            name: "指令 - 圆形 (目标) (指令罐)",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:012F"]
        )]
        public void RockSlideStoneSwell_CircleTarget(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"指令 (Tether 012F) 触发: 绘制圆形AOE, 目标: {@event.TargetId}");
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"ExtremePrejudice_Circle_{@event.TargetId}";
            dp.Owner = @event.TargetId;
            dp.Scale = new Vector2(16);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 6100;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "指令 - 十字 (目标) (指令罐)",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:0130"]
        )]
        public void RockSlideStoneSwell_Cross(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"指令 (Tether 0130) 触发: 绘制十字AOE, 目标: {@event.TargetId}");
            // 绘制第一条直线
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = $"ExtremePrejudice_Cross1_{@event.TargetId}";
            dp1.Owner = @event.TargetId;
            dp1.Scale = new Vector2(10, 80);
            dp1.Color = accessory.Data.DefaultDangerColor;
            dp1.DestoryAt = 6100;
            dp1.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);

            // 绘制第二条垂直的直线
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = $"ExtremePrejudice_Cross2_{@event.TargetId}";
            dp2.Owner = @event.TargetId;
            dp2.Scale = new Vector2(10, 80);
            dp2.Rotation = MathF.PI / 2;
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.DestoryAt = 6100;
            dp2.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
        }

        [ScriptMethod(
            name: "指令 - 圆形 (目标) (指令罐)",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:0131"]
        )]
        public void RockSlideStoneSwell_CircleSource(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"指令 (Tether 0131) 触发: 绘制圆形AOE, 目标: {@event.TargetId}");
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"ExtremePrejudice_Circle_{@event.TargetId}";
            dp.Owner = @event.TargetId;
            dp.Scale = new Vector2(16);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 6100;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }


        [ScriptMethod(
            name: "指令-绘图移除",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(41420|39470|41421|39471)$"],
            userControl: false
        )]
        public void RemoveRockSlideStoneSwell(Event @event, ScriptAccessory accessory)
        {
            uint actionId = @event.ActionId;
            if (actionId == 41421 || actionId == 39471)
            {
                accessory.Method.RemoveDraw($"ExtremePrejudice_Cross1_{@event.SourceId}");
                accessory.Method.RemoveDraw($"ExtremePrejudice_Cross2_{@event.SourceId}");
            }
            else // StoneSwell (圆形)
            {
                accessory.Method.RemoveDraw($"ExtremePrejudice_Circle_{@event.SourceId}");
            }
        }
        #region 进化加鲁拉
        // --- 进化加鲁拉 ---


        [ScriptMethod(
            name: "场地中心记录 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41188"],
            userControl: false
        )]
        public void AgitatedGroanVisual(Event @event, ScriptAccessory accessory)
        {
            if (!_noiseComplaintCenterRecorded)
            {
                _noiseComplaintArenaCenter = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                _noiseComplaintCenterRecorded = true;
                accessory.Log.Debug($"进化加鲁拉 Arena Center recorded at: {_noiseComplaintArenaCenter}");
            }
        }

        [ScriptMethod(
            name: "雷圈 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41186"]
        )]
        public void EpicenterShock(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "NoiseComplaint_EpicenterShock_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(12);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "核爆 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41187"]
        )]
        public void MammothBolt(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "NoiseComplaint_MammothBolt_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(25);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 6000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(
            name: "雷光十字 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41184"]
        )]
        public void LightningCrossing(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "NoiseComplaint_LightningCrossing_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(70);
            dp.Radian = 45 * MathF.PI / 180.0f;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 4000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(
            name: "猛挥 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(43262|41180)$"]
        )]
        public void Heave(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"NoiseComplaint_Heave_Danger_Zone_{@event.ActionId}";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(60);
            dp.Radian = 120 * MathF.PI / 180.0f;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = (@event.ActionId == 43262) ? 4000 : 2000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(
            name: "冲锋-方向记录 (进化加鲁拉)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:2193"],
            userControl: false
        )]
        public void RushingRumbleRampage_Status(Event @event, ScriptAccessory accessory)
        {
            // Extra == "0x350" is Cardinal, "0x351" is Intercardinal
            _lightningIsCardinal = @event["Param"] == "848";
            TryDrawMechanics(accessory);
        }
        [ScriptMethod(
            name: "冲锋-小鸟记录 (进化加鲁拉)",
            eventType: EventTypeEnum.TargetIcon,
            eventCondition: ["Id:0242"],
            userControl: false
        )]
        public void RushingRumbleRampage_Icon(Event @event, ScriptAccessory accessory)
        {
            // 如果连续冲锋序列正在进行，这个图标会触发下一次冲锋。
            if (_isRampageSequenceRunning)
            {
                accessory.Log.Debug($"检测到连续冲锋的小鸟标记。冲锋索引: {_rampageChargeIndex}。");
                var bird = accessory.Data.Objects.SearchById(@event.TargetId);
                if (bird == null)
                {
                    accessory.Log.Error($"未能找到用于连续冲锋 {_rampageChargeIndex} 的小鸟 {@event.TargetId}。");
                    return;
                }

                // 绘制这一段的连续冲锋。
                DrawRampageCharge(accessory, _rampageNextChargeStartPos, bird, _rampageChargeIndex);

                // 为下一次冲锋更新状态。
                _rampageNextChargeStartPos = bird.Position; // 下一次冲锋从这只鸟的位置开始。
                _rampageChargeIndex++;

                accessory.Log.Debug($"已绘制连续冲锋 {_rampageChargeIndex - 1}。下一次冲锋将从 {bird.Position} 开始。");

                // 如果所有冲锋都已完成（通常是3次），则重置状态。
                if (_rampageChargeIndex >= 3)
                {
                    accessory.Log.Debug("连续冲锋序列完成。");
                    ResetState();
                }
            }
            // 对于其他机制，仅将小鸟入队并尝试绘制。
            else
            {
                _activeBirds.Enqueue(@event.TargetId);
                accessory.Log.Debug($"小鸟ID: {@event.TargetId} 已为非连续冲锋机制入队。队列数量: {_activeBirds.Count}");
                TryDrawMechanics(accessory);
            }
        }

        [ScriptMethod(
            name: "RushingRumble (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41175"]
        )]
        public void OnRushingRumbleStart(Event @event, ScriptAccessory accessory)
        {
            _activeMechanicId = 41175;
            _bossId = @event.SourceId;
            var boss = accessory.Data.Objects.SearchById(_bossId);
            if (boss != null)
            {
                _chargeStartPosition = boss.Position; // 记录冲锋起始位置
                accessory.Log.Debug($"单次冲锋启动，记录起始位置: {_chargeStartPosition}");
            }
            TryDrawMechanics(accessory);
        }
        /*
        public void DrawRushingRumbleIfReady(Event @event, ScriptAccessory accessory)
        {
            if (_lightningIsCardinal == null || _activeBirds.Count == 0)
                return;

            var bird = accessory.Data.Objects.SearchById(_activeBirds.Dequeue());
            if (bird == null) return;

            var boss = accessory.Data.Objects.SearchById(@event.SourceId);
            if (boss == null) return;
            _bossId = boss.EntityId;

            //var arenaCenter = _noiseComplaintArenaCenter ?? new Vector3(461f, 0, -363f); //进岛后根据实际情况修改该场地中心
            var birdPos = bird.Position;
            var destination = birdPos - boss.Position;

            // 1. 直线冲锋
            var dpRumble = accessory.Data.GetDefaultDrawProperties();
            dpRumble.Name = $"NoiseComplaint_Rumble_{bird.EntityId}";
            dpRumble.Owner = _bossId;
            dpRumble.TargetPosition = destination;
            dpRumble.Scale = new Vector2(8, 100);
            dpRumble.ScaleMode |= ScaleMode.YByDistance | ScaleMode.ByTime;
            dpRumble.Color = accessory.Data.DefaultDangerColor;
            dpRumble.DestoryAt = 6300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpRumble);

            // 2. 落地大圈
            var dpCircle = accessory.Data.GetDefaultDrawProperties();
            dpCircle.Name = $"NoiseComplaint_Rush_Circle_{bird.EntityId}";
            dpCircle.Position = destination;
            dpCircle.Scale = new Vector2(30);
            dpCircle.Color = accessory.Data.DefaultDangerColor;
            dpCircle.Delay = 0;
            dpCircle.DestoryAt = 9400;
            dpCircle.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);

            // 3. 四连扇形
            var dirToBoss = boss.Position - destination;
            var initialAngle = MathF.Atan2(dirToBoss.X, dirToBoss.Z);
            if (_lightningIsCardinal == false) // 如果是斜点
            {
                initialAngle += 45 * MathF.PI / 180.0f;
            }

            for (int i = 0; i < 4; i++)
            {
                var dpCone = accessory.Data.GetDefaultDrawProperties();
                dpCone.Name = $"NoiseComplaint_Cone_{bird.EntityId}_{i}";
                dpCone.Position = destination;
                dpCone.Scale = new Vector2(70);
                dpCone.Radian = 45 * MathF.PI / 180.0f;
                dpCone.Rotation = initialAngle + (i * 90 * MathF.PI / 180.0f);
                dpCone.Color = accessory.Data.DefaultDangerColor;
                dpCone.Delay = 5400;
                dpCone.DestoryAt = 5000;
                dpCone.ScaleMode |= ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpCone);
            }

            // 清理本次处理的状态
            _activeBirds.Clear();
            _lightningIsCardinal = null;
        }
        */

        [ScriptMethod(
            name: "狂鸟冲锋 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41176"]
        )]
        public void OnBirdserkRushStart(Event @event, ScriptAccessory accessory)
        {
            _activeMechanicId = 41176;
            _bossId = @event.SourceId;
            TryDrawMechanics(accessory);
        }
        /*
        public void BirdserkRush(Event @event, ScriptAccessory accessory)
        {
            var bird = accessory.Data.Objects.SearchById(_activeBirds.Dequeue());
            if (bird == null) return;
            _birdserkRushTargetId = bird.EntityId;
            // 绘制冲锋路径
            var dpCharge = accessory.Data.GetDefaultDrawProperties();
            dpCharge.Name = $"NoiseComplaint_BirdserkRush_Charge_{@event.SourceId}";
            dpCharge.Owner = @event.SourceId;
            dpCharge.TargetObject = _birdserkRushTargetId;
            dpCharge.Scale = new Vector2(8, 100);
            dpCharge.ScaleMode |= ScaleMode.YByDistance | ScaleMode.ByTime;
            dpCharge.Color = accessory.Data.DefaultDangerColor;
            dpCharge.DestoryAt = 6300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpCharge);

            // 绘制最终的扇形AOE
            var dpCone = accessory.Data.GetDefaultDrawProperties();
            dpCone.Name = $"NoiseComplaint_BirdserkRush_Cone_{@event.SourceId}";
            dpCone.Owner = @event.SourceId;
            dpCone.TargetObject = _birdserkRushTargetId;
            dpCone.Scale = new Vector2(60);
            dpCone.Radian = 120 * MathF.PI / 180.0f;
            dpCone.Color = accessory.Data.DefaultDangerColor;
            dpCone.DestoryAt = 11000;
            dpCone.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpCone);

            // 重置小鸟ID，为下一次机制做准备
            _activeBirds.Clear();
        }
        */
        [ScriptMethod(
            name: "Rushing Rumble Rampage (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41177"]
        )]
        public void OnRampageStart(Event @event, ScriptAccessory accessory)
        {
            _activeMechanicId = 41177;
            _bossId = @event.SourceId;
            _isRampageSequenceRunning = true;
            _rampageChargeIndex = 0;

            var boss = accessory.Data.Objects.SearchById(_bossId);
            if (boss != null)
            {
                _rampageNextChargeStartPos = boss.Position;
                accessory.Log.Debug($"连续冲锋初始位置已设置为Boss位置: {_rampageNextChargeStartPos}");
            }
            else
            {
                accessory.Log.Error("序列开始时未能找到“Rushing Rumble Rampage”的Boss。正在重置状态。");
                ResetState();
            }
        }
        private void TryDrawMechanics(ScriptAccessory accessory)
        {
            // 如果连续冲锋正在进行或没有激活的机制，则不执行任何操作。
            if (_isRampageSequenceRunning || _activeMechanicId == 0) return;

            switch (_activeMechanicId)
            {
                // 单次冲锋：需要方向和一只小鸟。
                case 41175 when _lightningIsCardinal != null && _activeBirds.Count > 0:
                    DrawRushingRumble(accessory);
                    ResetState();
                    break;

                // 狂鸟冲锋：只需要一只小鸟。
                case 41176 when _activeBirds.Count > 0:
                    DrawBirdserkRush(accessory);
                    ResetState();
                    break;
            }
        }
        private void ResetState()
        {
            _lightningIsCardinal = null;
            _activeBirds.Clear();
            _bossId = 0;
            _activeMechanicId = 0;
            _isRampageSequenceRunning = false;
            _rampageChargeIndex = 0;
            _chargeStartPosition = Vector3.Zero;
        }
        private void DrawRushingRumble(ScriptAccessory accessory)
        {
            if (!_activeBirds.TryDequeue(out var birdId)) return;
            var bird = accessory.Data.Objects.SearchById(birdId);
            var boss = accessory.Data.Objects.SearchById(_bossId);
            if (bird == null || boss == null) return;

            var destination = bird.Position;

            // 1. 从Boss到小鸟的直线冲锋
            var dpRumble = accessory.Data.GetDefaultDrawProperties();
            dpRumble.Name = $"NoiseComplaint_Rumble_{bird.EntityId}";
            dpRumble.Owner = _bossId;
            dpRumble.TargetPosition = destination;
            dpRumble.Scale = new Vector2(8, 100);
            dpRumble.ScaleMode |= ScaleMode.YByDistance;
            dpRumble.Color = accessory.Data.DefaultDangerColor;
            dpRumble.DestoryAt = 6300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpRumble);

            // 2. 落地大圈
            var dpCircle = accessory.Data.GetDefaultDrawProperties();
            dpCircle.Name = $"NoiseComplaint_Rush_Circle_{bird.EntityId}";
            dpCircle.Position = destination;
            dpCircle.Scale = new Vector2(30);
            dpCircle.Color = accessory.Data.DefaultDangerColor;
            dpCircle.DestoryAt = 9400;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);

            // 3. 四连扇形
            var dirToBoss = boss.Position - destination;
            var initialAngle = MathF.Atan2(dirToBoss.X, dirToBoss.Z);
            if (_lightningIsCardinal == false) // 如果是斜角方向则调整
            {
                initialAngle += 45 * MathF.PI / 180.0f;
            }

            for (int i = 0; i < 4; i++)
            {
                var dpCone = accessory.Data.GetDefaultDrawProperties();
                dpCone.Name = $"NoiseComplaint_Cone_{bird.EntityId}_{i}";
                dpCone.Position = destination;
                dpCone.Scale = new Vector2(70);
                dpCone.Radian = 45 * MathF.PI / 180.0f;
                dpCone.Rotation = initialAngle + (i * 90 * MathF.PI / 180.0f);
                dpCone.Color = accessory.Data.DefaultDangerColor;
                dpCone.Delay = 0;
                dpCone.DestoryAt = 104000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpCone);
            }
        }

        private void DrawBirdserkRush(ScriptAccessory accessory)
        {
            if (!_activeBirds.TryDequeue(out var birdId)) return;
            var bird = accessory.Data.Objects.SearchById(birdId);
            if (bird == null) return;

            // 从Boss到小鸟的直线冲锋
            var dpCharge = accessory.Data.GetDefaultDrawProperties();
            dpCharge.Name = $"NoiseComplaint_BirdserkRush_Charge_{_bossId}";
            dpCharge.Owner = _bossId;
            dpCharge.TargetObject = bird.EntityId;
            dpCharge.Scale = new Vector2(8, 100);
            dpCharge.ScaleMode |= ScaleMode.YByDistance;
            dpCharge.Color = accessory.Data.DefaultDangerColor;
            dpCharge.DestoryAt = 6300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpCharge);

            // 从Boss朝向小鸟的大范围扇形顺劈
            var dpCone = accessory.Data.GetDefaultDrawProperties();
            dpCone.Name = $"NoiseComplaint_BirdserkRush_Cone_{_bossId}";
            dpCone.Owner = _bossId;
            dpCone.TargetObject = bird.EntityId;
            dpCone.Scale = new Vector2(60);
            dpCone.Radian = 120 * MathF.PI / 180.0f;
            dpCone.Color = accessory.Data.DefaultDangerColor;
            dpCone.DestoryAt = 11000;
            dpCone.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpCone);
        }
        private void DrawRampageCharge(ScriptAccessory accessory, Vector3 chargeStartPos, IGameObject bird, int chargeIndex)
        {
            if (_lightningIsCardinal == null)
            {
                accessory.Log.Error("无法绘制连续冲锋，雷电方向未知。");
                return;
            }

            var destination = bird.Position;

            // 1. 直线冲锋
            var dpCharge = accessory.Data.GetDefaultDrawProperties();
            dpCharge.Name = $"NoiseComplaint_Rampage_Charge_{chargeIndex}";
            dpCharge.Position = chargeStartPos;
            dpCharge.TargetPosition = destination;
            dpCharge.Scale = new Vector2(8, 100);
            dpCharge.ScaleMode |= ScaleMode.YByDistance;
            dpCharge.Color = accessory.Data.DefaultDangerColor;
            dpCharge.DestoryAt = 6300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpCharge);

            // 2. 目标点的大圆形AOE
            var dpCircle = accessory.Data.GetDefaultDrawProperties();
            dpCircle.Name = $"NoiseComplaint_Rampage_Circle_{chargeIndex}";
            dpCircle.Position = destination;
            dpCircle.Scale = new Vector2(30);
            dpCircle.Color = accessory.Data.DefaultDangerColor;
            dpCircle.DestoryAt = 9400;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);

            // 3. 从目标点发出的四个扇形AOE
            var chargeDirectionVector = destination - chargeStartPos;
            var initialAngle = MathF.Atan2(chargeDirectionVector.X, chargeDirectionVector.Z);
            if (_lightningIsCardinal == false) // 如果是斜角方向则调整
            {
                initialAngle += 45 * MathF.PI / 180.0f;
            }

            for (int i = 0; i < 4; i++)
            {
                var dpCone = accessory.Data.GetDefaultDrawProperties();
                dpCone.Name = $"NoiseComplaint_Rampage_Cone_{chargeIndex}_{i}";
                dpCone.Position = destination;
                dpCone.Scale = new Vector2(70);
                dpCone.Radian = 45 * MathF.PI / 180.0f;
                dpCone.Rotation = initialAngle + (i * 90 * MathF.PI / 180.0f);
                dpCone.Color = accessory.Data.DefaultDangerColor;
                dpCone.Delay = 0;
                dpCone.DestoryAt = 10400;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpCone);
            }
        }
        /*
        private async Task StartRampageSequenceAsync(ScriptAccessory accessory)
        {
            accessory.Log.Debug("Rushing Rumble Rampage (41177) sequence initiated.");
            var boss = accessory.Data.Objects.SearchById(_bossId);
            if (boss == null)
            {
                accessory.Log.Error("Could not find boss for Rushing Rumble Rampage.");
                ResetState();
                return;
            }

            await ProcessNextRampageCharge(accessory, boss.Position, 0);

            // 整个序列结束后，重置状态
            ResetState();
        }

        private async Task ProcessNextRampageCharge(ScriptAccessory accessory, Vector3 chargeStartPos, int chargeIndex)
        {
            if (_activeBirds.Count == 0)
            {
                accessory.Log.Debug("Rampage sequence finished or no more birds in queue.");
                return;
            }

            var birdId = _activeBirds.Dequeue();
            var bird = accessory.Data.Objects.SearchById(birdId);
            if (bird == null)
            {
                accessory.Log.Error($"Could not find bird {birdId} for Rampage charge {chargeIndex}. Skipping.");
                // 继续处理下一只鸟
                if (chargeIndex < _rampageDelays.Length)
                {
                    await ProcessNextRampageCharge(accessory, chargeStartPos, chargeIndex + 1);
                }
                return;
            }

            var destination = bird.Position;

            // 1. 直线冲锋
            var dpCharge = accessory.Data.GetDefaultDrawProperties();
            dpCharge.Name = $"NoiseComplaint_Rampage_Charge_{chargeIndex}";
            dpCharge.Position = chargeStartPos;
            dpCharge.TargetPosition = destination;
            dpCharge.Scale = new Vector2(8, 100);
            dpCharge.ScaleMode |= ScaleMode.YByDistance;
            dpCharge.Color = accessory.Data.DefaultDangerColor;
            dpCharge.DestoryAt = 6300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpCharge);

            // 2. 落地大圈
            var dpCircle = accessory.Data.GetDefaultDrawProperties();
            dpCircle.Name = $"NoiseComplaint_Rampage_Circle_{chargeIndex}";
            dpCircle.Position = destination;
            dpCircle.Scale = new Vector2(30);
            dpCircle.Color = accessory.Data.DefaultDangerColor;
            dpCircle.DestoryAt = 9400;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);

            // 3. 四连扇形
            var chargeDirectionVector = destination - chargeStartPos;
            var initialAngle = MathF.Atan2(chargeDirectionVector.X, chargeDirectionVector.Z);
            if (_lightningIsCardinal == false) // 如果是斜点
            {
                initialAngle += 45 * MathF.PI / 180.0f;
            }

            for (int i = 0; i < 4; i++)
            {
                var dpCone = accessory.Data.GetDefaultDrawProperties();
                dpCone.Name = $"NoiseComplaint_Rampage_Cone_{chargeIndex}_{i}";
                dpCone.Position = destination;
                dpCone.Scale = new Vector2(70);
                dpCone.Radian = 45 * MathF.PI / 180.0f;
                dpCone.Rotation = initialAngle + (i * 90 * MathF.PI / 180.0f);
                dpCone.Color = accessory.Data.DefaultDangerColor;
                dpCone.Delay = 5400;
                dpCone.DestoryAt = 5000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpCone);
            }

            // 如果还有下一次冲锋，则等待并递归调用
            if (chargeIndex < _rampageDelays.Length)
            {
                int delay = _rampageDelays[chargeIndex];
                await Task.Delay(delay);
                await ProcessNextRampageCharge(accessory, destination, chargeIndex + 1);
            }
        }
        */
        /*
        public void HandleRushingRumbleRampage(Event @event, ScriptAccessory accessory)
        {
            _ = StartRampageSequenceAsync(@event, accessory);
        }

        private async Task StartRampageSequenceAsync(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug("Rushing Rumble Rampage (41177) sequence initiated.");

            // Find and store the boss's ID
            var boss = accessory.Data.Objects.SearchById(@event.SourceId);
            if (boss == null)
            {
                accessory.Log.Error("Could not find boss for Rushing Rumble Rampage.");
                _activeBirds.Clear();
                _lightningIsCardinal = null;
                return;
            }
            _bossId = boss.EntityId;

            await ProcessNextRampageCharge(accessory, boss.Position, 0);


        }

        private async Task ProcessNextRampageCharge(ScriptAccessory accessory, Vector3 chargeStartPos, int chargeIndex)


        {
            if (_activeBirds.Count == 0)
            {
                _lightningIsCardinal = null;
                _bossId = 0;
                return;
            }

            var birdId = _activeBirds.Dequeue();
            var bird = accessory.Data.Objects.SearchById(birdId);
            var boss = accessory.Data.Objects.SearchById(_bossId);
            if (bird == null || boss == null)
            {
                accessory.Log.Error($"Could not find bird {birdId} for Rushing Rumble Rampage.");
                _activeBirds.Clear();
                await ProcessNextRampageCharge(accessory, chargeStartPos, chargeIndex);  //尝试再执行
                return;
            }

            var destination = bird.Position;
            //const int chargeTimeMs = 6300;
            //const int chargeDelayMs = 3500;
            //const int delayBetweenChargesMs = 5200;

            var dpCharge = accessory.Data.GetDefaultDrawProperties();
            dpCharge.Name = $"NoiseComplaint_Rampage_Charge";
            dpCharge.Position = chargeStartPos;
            dpCharge.TargetPosition = destination;
            dpCharge.Scale = new Vector2(8, 100);
            dpCharge.Color = accessory.Data.DefaultDangerColor;
            dpCharge.DestoryAt = 6300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpCharge);

            // 2. 落地大圈
            var dpCircle = accessory.Data.GetDefaultDrawProperties();
            dpCircle.Name = $"NoiseComplaint_Rush_Circle";
            dpCircle.Position = destination;
            dpCircle.Scale = new Vector2(30);
            dpCircle.Color = accessory.Data.DefaultDangerColor;
            dpCircle.Delay = 0;
            dpCircle.DestoryAt = 9400;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpCircle);

            // 3. 四连扇形
            var chargeDirectionVector = destination - chargeStartPos;
            //var dirToBoss = boss.Position - destination;
            var initialAngle = MathF.Atan2(chargeDirectionVector.X, chargeDirectionVector.Z);
            if (_lightningIsCardinal == false) // 如果是斜点
            {
                initialAngle += 45 * MathF.PI / 180.0f;
            }

            for (int i = 0; i < 4; i++)
            {
                var dpCone = accessory.Data.GetDefaultDrawProperties();
                dpCone.Name = $"NoiseComplaint_Cone_{i}";
                dpCone.Position = destination;
                dpCone.Scale = new Vector2(70);
                dpCone.Radian = 45 * MathF.PI / 180.0f;
                dpCone.Rotation = initialAngle + (i * 90 * MathF.PI / 180.0f);
                dpCone.Color = accessory.Data.DefaultDangerColor;
                dpCone.Delay = 0;
                dpCone.DestoryAt = 10500;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dpCone);

            }
            if (chargeIndex < _rampageDelays.Length)
            {
                int delay = _rampageDelays[chargeIndex];
                await Task.Delay(delay);
                await ProcessNextRampageCharge(accessory, destination, chargeIndex + 1);
            }
        }
        */

        [ScriptMethod(
            name: "绘图移除 (进化加鲁拉)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(41178|41180|41179|42984)$"],
            userControl: false
        )]
        public void RemoveRush(Event @event, ScriptAccessory accessory)
        {
            switch (@event.ActionId)
            {
                case 41178:
                    accessory.Method.RemoveDraw($"NoiseComplaint_BirdserkRush_Charge_{@event.SourceId}");
                    accessory.Method.RemoveDraw($"NoiseComplaint_Rampage_Charge");
                    break;
                case 41180:
                    accessory.Method.RemoveDraw($"NoiseComplaint_BirdserkRush_Cone_{@event.SourceId}");
                    break;
                case 41179:
                    accessory.Method.RemoveDraw($"NoiseComplaint_Rush_Circle");
                    break;
                case 42984:
                    for (int i = 0; i < 4; i++)
                    {
                        accessory.Method.RemoveDraw($"NoiseComplaint_Cone_{i}");
                    }
                    break;
            }
        }
        #endregion

        #region 死亡爪
        [ScriptMethod(
            name: "爪痕 (死亡爪)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41315|41316|41317)$"]
        )]

        public void LethalNails(Event @event, ScriptAccessory accessory)
        {
            var nailId = @event.ActionId;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"死亡爪_LethalNails_{nailId}";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(7, 60);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode |= ScaleMode.ByTime;
            switch (nailId)
            {
                case 41315: // 第一种类型
                    dp.DestoryAt = 2000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
                case 41316: // 第二种类型
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
                case 41317: // 第三种类型
                    dp.Delay = 2000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
            }
        }

        [ScriptMethod(
            name: "垂直交错 (死亡爪)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41323"]
        )]
        public void VerticalCrosshatch(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"垂直交错 (VerticalCrosshatch) 触发. ActionId: {@event.ActionId}, SourceId: {@event.SourceId}");
            const int frontBackDelay = 0;
            const int leftRightDelay = 2500;
            const int duration = 5000;

            float[] rotations = { 0, MathF.PI, MathF.PI / 2, -MathF.PI / 2 };
            int[] delays = { frontBackDelay, frontBackDelay, leftRightDelay, leftRightDelay };
            string[] names = { "Front", "Back", "Right", "Left" };

            for (int i = 0; i < 4; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"死亡爪_Crosshatch_{names[i]}_{@event.SourceId}";
                dp.Owner = @event.SourceId;
                dp.Scale = new Vector2(50);
                dp.Radian = 90 * MathF.PI / 180.0f;
                dp.Rotation = rotations[i];
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delays[i];
                dp.DestoryAt = duration;
                dp.ScaleMode |= ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                accessory.Log.Debug($"  绘制扇形: {dp.Name}, Rotation: {dp.Rotation}, Delay: {dp.Delay}ms");
            }
        }
        [ScriptMethod(
            name: "垂直交错长 (死亡爪)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41330"]
        )]
        public void VerticalCrosslonghatch(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"垂直交错 (VerticalCrosshatch) 触发. ActionId: {@event.ActionId}, SourceId: {@event.SourceId}");
            const int frontBackDelay = 0;
            const int leftRightDelay = 2500;
            const int duration = 7500;

            float[] rotations = { 0, MathF.PI, MathF.PI / 2, -MathF.PI / 2 };
            int[] delays = { frontBackDelay, frontBackDelay, leftRightDelay, leftRightDelay };
            string[] names = { "Front", "Back", "Right", "Left" };

            for (int i = 0; i < 4; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"死亡爪_Crosshatch_{names[i]}_{@event.SourceId}";
                dp.Owner = @event.SourceId;
                dp.Scale = new Vector2(50);
                dp.Radian = 90 * MathF.PI / 180.0f;
                dp.Rotation = rotations[i];
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delays[i];
                dp.DestoryAt = duration;
                dp.ScaleMode |= ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                accessory.Log.Debug($"  绘制扇形: {dp.Name}, Rotation: {dp.Rotation}, Delay: {dp.Delay}ms");
            }
        }

        [ScriptMethod(
            name: "水平交错 (死亡爪)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41324"]
        )]
        public void HorizontalCrosshatch(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"水平交错 (HorizontalCrosshatch) 触发. ActionId: {@event.ActionId}, SourceId: {@event.SourceId}");
            const int frontBackDelay = 2500;
            const int leftRightDelay = 0;
            const int duration = 5000;

            float[] rotations = { 0, MathF.PI, MathF.PI / 2, -MathF.PI / 2 };
            int[] delays = { frontBackDelay, frontBackDelay, leftRightDelay, leftRightDelay };
            string[] names = { "Front", "Back", "Right", "Left" };

            for (int i = 0; i < 4; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"死亡爪_Crosshatch_{names[i]}_{@event.SourceId}";
                dp.Owner = @event.SourceId;
                dp.Scale = new Vector2(50);
                dp.Radian = 90 * MathF.PI / 180.0f;
                dp.Rotation = rotations[i];
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delays[i];
                dp.DestoryAt = duration;
                dp.ScaleMode |= ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                accessory.Log.Debug($"  绘制扇形: {dp.Name}, Rotation: {dp.Rotation}, Delay: {dp.Delay}ms");
            }
        }
        [ScriptMethod(
            name: "水平交错长 (死亡爪)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41331"]
        )]
        public void HorizontalCrosslonghatch(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"水平交错 (HorizontalCrosshatch) 触发. ActionId: {@event.ActionId}, SourceId: {@event.SourceId}");
            const int frontBackDelay = 2500;
            const int leftRightDelay = 0;
            const int duration = 7500;

            float[] rotations = { 0, MathF.PI, MathF.PI / 2, -MathF.PI / 2 };
            int[] delays = { frontBackDelay, frontBackDelay, leftRightDelay, leftRightDelay };
            string[] names = { "Front", "Back", "Right", "Left" };

            for (int i = 0; i < 4; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"死亡爪_Crosshatch_{names[i]}_{@event.SourceId}";
                dp.Owner = @event.SourceId;
                dp.Scale = new Vector2(50);
                dp.Radian = 90 * MathF.PI / 180.0f;
                dp.Rotation = rotations[i];
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = delays[i];
                dp.DestoryAt = duration;
                dp.ScaleMode |= ScaleMode.ByTime;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                accessory.Log.Debug($"  绘制扇形: {dp.Name}, Rotation: {dp.Rotation}, Delay: {dp.Delay}ms");
            }
        }
        /*
        [ScriptMethod(
            name: "SkulkingOrders (死亡爪)(未完成）",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId: regex: ^(41326|41329)$"]
        )]
        public void SkulkingOrders(Event @event, ScriptAccessory accessory)
        {
            
        }
        */

        #endregion

        #region 水晶龙
        [ScriptMethod(
            name: "PrismaticWing（钢铁月环）(水晶龙)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(42766|42767|42768|42769)$"]
        )]
        public void PrismaticWing(Event @event, ScriptAccessory accessory)
        {
            var AID = @event.ActionId;
            switch (AID)
            {
                case 42766: // 钢铁
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"水晶龙_PrismaticWing_{AID}";
                        dp.Owner = @event.SourceId;
                        dp.Scale = new Vector2(22, 22);
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.DestoryAt = 7000;
                        dp.ScaleMode |= ScaleMode.ByTime;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                        break;
                    }
                case 42768:
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"水晶龙_PrismaticWing_{AID}";
                        dp.Owner = @event.SourceId;
                        dp.Scale = new Vector2(22, 22);
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.DestoryAt = 4500;
                        dp.ScaleMode |= ScaleMode.ByTime;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                        break;
                    }
                case 42767: // 月环
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"水晶龙_PrismaticWing_{AID}";
                        dp.Owner = @event.SourceId;
                        dp.Scale = new Vector2(31, 31);
                        dp.InnerScale = new Vector2(5, 5);
                        dp.Radian = 360 * MathF.PI / 180.0f;
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.DestoryAt = 7000;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                        break;
                    }
                case 42769: // 月环
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"水晶龙_PrismaticWing_{AID}";
                        dp.Owner = @event.SourceId;
                        dp.Scale = new Vector2(31, 31);
                        dp.InnerScale = new Vector2(5, 5);
                        dp.Radian = 360 * MathF.PI / 180.0f;
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.DestoryAt = 4500;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                        break;
                    }
            }
        }
        [ScriptMethod(
            name: "结晶能量/混沌 (水晶龙)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(42728|42729|42730|42731|42732|42733|42734|42735|41758|41759|41760|41761)$"]
        )]
        public void CrystallizedEnergyAndChaos(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"CrawlingDeath_Crystallized_{@event.ActionId}";
            dp.Owner = @event.SourceId;
            dp.Color = accessory.Data.DefaultDangerColor;

            switch (@event.ActionId)
            {

                case 42728:
                    dp.Scale = new Vector2(7, 7);
                    dp.Delay = 3000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    break;
                case 42729:
                    dp.Scale = new Vector2(13, 13);
                    dp.InnerScale = new Vector2(7, 7);
                    dp.Radian = 360 * MathF.PI / 180.0f;
                    dp.Delay = 3000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
                case 42730:
                    dp.Scale = new Vector2(19, 19);
                    dp.InnerScale = new Vector2(13, 13);
                    dp.Radian = 360 * MathF.PI / 180.0f;
                    dp.Delay = 3000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
                case 42731:
                    dp.Scale = new Vector2(25, 25);
                    dp.InnerScale = new Vector2(19, 19);
                    dp.Radian = 360 * MathF.PI / 180.0f;
                    dp.Delay = 3000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;


                case 42732:
                    dp.Scale = new Vector2(7, 7);
                    dp.Delay = 6000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    break;
                case 42733:
                    dp.Scale = new Vector2(13, 13);
                    dp.InnerScale = new Vector2(7, 7);
                    dp.Radian = 360 * MathF.PI / 180.0f;
                    dp.Delay = 6000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
                case 42734:
                    dp.Scale = new Vector2(19, 19);
                    dp.InnerScale = new Vector2(13, 13);
                    dp.Radian = 360 * MathF.PI / 180.0f;
                    dp.Delay = 6000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
                case 42735:
                    dp.Scale = new Vector2(25, 25);
                    dp.InnerScale = new Vector2(19, 19);
                    dp.Radian = 360 * MathF.PI / 180.0f;
                    dp.Delay = 6000;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;


                case 41758:
                    dp.Scale = new Vector2(7, 7);
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    break;
                case 41759:
                    dp.Scale = new Vector2(13, 13);
                    dp.InnerScale = new Vector2(7, 7);
                    dp.Radian = 360 * MathF.PI / 180.0f;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
                case 41760:
                    dp.Scale = new Vector2(19, 19);
                    dp.InnerScale = new Vector2(13, 13);
                    dp.Radian = 360 * MathF.PI / 180.0f;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
                case 41761:
                    dp.Scale = new Vector2(25, 25);
                    dp.InnerScale = new Vector2(19, 19);
                    dp.Radian = 360 * MathF.PI / 180.0f;
                    dp.DestoryAt = 4000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    break;
            }
        }

        #endregion

        #region 新月狂战士
        [ScriptMethod(
           name: "严厉扫荡 (新月狂战士)",
           eventType: EventTypeEnum.StartCasting,
           eventCondition: ["ActionId:42691"]
       )]

        public void ScathingSweep(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "新月狂战士_Sweep_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(60, 60);
            dp.Color = new Vector4(1f, 0f, 0f, 1f);
            dp.DestoryAt = 6000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        [ScriptMethod(
            name: "狂怒1(新月狂战士)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:37323"]
        )]
        public void 新月狂战士Rage1(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "新月狂战士_Rage1_Danger_Zone";
            dp.Position = @event.TargetPosition;
            dp.Scale = new Vector2(8, 8);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 8000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "新月狂战士_BedrockUplift1_Danger_Zone";
            dp2.Position = @event.TargetPosition;
            dp2.Scale = new Vector2(60, 60);
            dp2.InnerScale = new Vector2(8, 8);
            dp2.Color = new Vector4(1f, 0f, 0f, 1f);
            dp2.Radian = 360 * MathF.PI / 180.0f;
            dp2.Delay = 6500;
            dp2.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
        }
        [ScriptMethod(
            name: "狂怒2(新月狂战士)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:30872"]
        )]
        public void 新月狂战士Rage2(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "新月狂战士_Rage2_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(24, 24);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 9000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "新月狂战士_BedrockUplift2_Danger_Zone";
            dp2.Owner = @event.SourceId;
            dp2.Scale = new Vector2(60, 60);
            dp2.InnerScale = new Vector2(24, 24);
            dp2.Radian = 360 * MathF.PI / 180.0f;
            dp2.Color = new Vector4(1f, 0f, 0f, 1f);
            dp2.Delay = 7500;
            dp2.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
        }
        [ScriptMethod(
            name: "狂怒3(新月狂战士)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:30873"]
        )]
        public void 新月狂战士Rage3(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "新月狂战士_Rage3_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(16, 16);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 9000;
            dp.DestoryAt = 6500;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "新月狂战士_BedrockUplift3_Danger_Zone";
            dp2.Owner = @event.SourceId;
            dp2.Scale = new Vector2(60, 60);
            dp2.InnerScale = new Vector2(16, 16);
            dp2.Radian = 360 * MathF.PI / 180.0f;
            dp2.Color = new Vector4(1f, 0f, 0f, 1f);
            dp2.Delay = 14000;
            dp2.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
        }
        [ScriptMethod(
            name: "狂怒4(新月狂战士)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:30874"]
        )]
        public void 新月狂战士Rage4(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "新月狂战士_Rage4_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(8, 8);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 12000;
            dp.DestoryAt = 6500;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "新月狂战士_BedrockUplift4_Danger_Zone";
            dp2.Owner = @event.SourceId;
            dp2.Scale = new Vector2(60, 60);
            dp2.InnerScale = new Vector2(16, 16);
            dp2.Color = new Vector4(1f, 0f, 0f, 1f);
            dp2.Radian = 360 * MathF.PI / 180.0f;
            dp2.Delay = 21000;
            dp2.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
        }
        [ScriptMethod(
            name: "激烈爆发(新月狂战士)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:37804"]
        )]
        public void 新月狂战士Fury(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "新月狂战士_Fury_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(13, 13);
            dp.Color = new Vector4(1f, 0f, 0f, 1f);
            dp.DestoryAt = 6000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        #endregion
        #region 回廊恶魔
        [ScriptMethod(
            name: "爆炸 (回廊恶魔)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41357"]
        )]
        public void Explosion(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "回廊恶魔_Explosion_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(22, 22);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(
            name: "潮汐吐息(回廊恶魔)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41360"]
        )]
        public void TidalBreath(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "回廊恶魔_TidalBreath_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(40);
            dp.Radian = 180 * MathF.PI / 180.0f;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 7000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        #endregion
        #region CompanyOfStone
        [ScriptMethod(
            name: "双拳连击 (CompanyOfStone)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41828"]
        )]
        public void DualfistFlurry(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null)
            {
                accessory.Log.Error($"无法找到双拳连击的施法者: {@event.SourceId}");
                return;
            }

            // 获取初始位置和前进方向
            var currentPos = JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
            var direction = new Vector3(MathF.Sin(caster.Rotation), 0, MathF.Cos(caster.Rotation));

            // 定义技能参数
            const int totalExplosions = 9;      
            const float radius = 6f;            
            const float stepDistance = 6f;      // 每次前进距离
            const int firstExplosionTime = 10000;// 第一次爆炸时间 (基于6秒咏唱)
            const int subsequentInterval = 1000;// 后续爆炸间隔
            const int warningDuration = 2000;   // 警告显示时间
            const int lingerDuration = 500;     // 爆炸后残留时间

            // 循环创建所有爆炸的绘图
            for (int i = 0; i < totalExplosions; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"CompanyOfStone_DualfistFlurry_Danger_Zone_{i}";
                dp.Position = currentPos;
                dp.Scale = new Vector2(radius);
                dp.Color = accessory.Data.DefaultDangerColor;

                // 计算每次爆炸的精确时间
                int explosionTime = firstExplosionTime + (i * subsequentInterval);

                // 设置绘图的延迟显示和销毁时间
                dp.Delay = explosionTime - warningDuration;
                dp.DestoryAt = warningDuration + lingerDuration;
                dp.ScaleMode |= ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                // 为下一次循环计算新的位置
                currentPos += direction * stepDistance;
            }
        }
        #endregion
        #region 鬼火苗
        [ScriptMethod(
            name: "分身机制（鬼火苗）",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41397|42033|42035)$"]
        )]
        
        public void 分身机制Draw(Event @event, ScriptAccessory accessory)
        {
            var ActionId = @event.ActionId;
            var dp = accessory.Data.GetDefaultDrawProperties();
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"鬼火苗_分身机制_{ActionId}";
            dp2.Name = $"鬼火苗_分身机制_{ActionId}_2";
            switch (ActionId)
            {
                case 41397: // 击退
                    dp.Scale = new Vector2(1.5f, 20f); // 线条宽度2，末端圆圈半径1
                    dp.Color = new(0.3f, 1.0f, 0f, 1.5f);
                    dp.Owner = accessory.Data.Me;
                    dp.TargetPosition = @event.SourcePosition;
                    dp.Rotation = MathF.PI;
                    dp.DestoryAt = 3000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
                    accessory.Log.Debug($"绘制击退: Name={dp.Name}s");
                    break;
                case 42033: // 月环
                    dp.Owner = @event.SourceId;
                    dp.Scale = new Vector2(50);
                    dp.InnerScale = new Vector2(7);
                    dp.Radian = 360 * MathF.PI / 180.0f;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = 3000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                    accessory.Log.Debug($"绘制月环AOE: Name={dp.Name}s");
                    break;
                case 42035: // 十字
                    dp.Owner = @event.SourceId;
                    dp.Scale = new Vector2(15, 200);
                    dp.Rotation = 90 * MathF.PI / 180.0f;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = 3000;
                    dp2.Owner = @event.SourceId;
                    dp2.Scale = new Vector2(15, 200);
                    dp2.Color = accessory.Data.DefaultDangerColor;
                    dp2.DestoryAt = 3000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
                    accessory.Log.Debug($"绘制十字AOE: Name={dp.Name}s");
                    accessory.Log.Debug($"绘制十字AOE: Name={dp2.Name}s");
                    break;

            }
        }



        /*
                [ScriptMethod(
                    name: "预告 - 记录机制（鬼火苗）",
                    eventType: EventTypeEnum.StartCasting,
                    eventCondition: ["ActionId:regex:^(41377|41374|41379)$"],
                    userControl: false
                )]
                public void OnTelegraph(Event @event, ScriptAccessory accessory)
                {
                    var casterPos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

                    // 根据当前是第几次机制和已记录的机制数量，来确定延迟时间
                    float delaySeconds = (_pendingMechanics.Count, _isFirstSequence) switch
                    {
                        (0, true) => 16.1f,
                        (1, true) => 18.4f,
                        (2, true) => 20.8f,
                        (3, true) => 23.2f,
                        (0, false) => 14.9f,
                        (1, false) => 15.3f,
                        (2, false) => 22.1f,
                        (3, false) => 22.5f,
                        _ => 0
                    };

                    if (delaySeconds == 0)
                    {
                        accessory.Log.Error($"无法确定机制延迟时间: Count={_pendingMechanics.Count}, isFirst={_isFirstSequence}");
                        return;
                    }

                    var activationTime = DateTime.Now.AddSeconds(delaySeconds);

                    var pending = new PendingMechanic
                    {
                        Position = casterPos,
                        ShapeActionId = @event.ActionId,
                        ActivationTime = activationTime
                    };

                    _pendingMechanics.Add(pending);
                    accessory.Log.Debug($"记录了新的待处理机制: 类型={pending.ShapeActionId}, 位置={pending.Position}, 激活时间={pending.ActivationTime:HH:mm:ss.fff}");

                    // 如果记录了4个，说明第一轮预告结束
                    if (_pendingMechanics.Count == 4)
                    {
                        _isFirstSequence = false;
                        accessory.Log.Debug("第一轮预告记录完毕，切换到第二轮时间轴。");
                    }
                }

                [ScriptMethod(
                    name: "激活外壳 - 绘制AOE/击退（鬼火苗）",
                    eventType: EventTypeEnum.ActionEffect,
                    eventCondition: ["ActionId:41371"] // ActivateHusk
                )]
                public void OnActivateHusk(Event @event, ScriptAccessory accessory)
                {
                    var activatorPos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                    var targetId = @event.TargetId;
                    var husk = accessory.Data.Objects.SearchById(targetId);
                    if (husk == null)
                    {
                        accessory.Log.Error($"找不到被激活的外壳: ID={targetId}");
                        return;
                    }

                    var huskPos = husk.Position;
                    accessory.Log.Debug($"外壳被激活: ID={targetId}, 位置={huskPos}");
                    PendingMechanic mech;
                    // 在访问和修改共享列表时使用锁
                    lock (_mechanicLock)
                    {
                        // 通过匹配“激活者”的位置来找到对应的“待处理”机制
                        mech = _pendingMechanics.OrderBy(p => Vector3.Distance(p.Position, activatorPos)).FirstOrDefault();

                        // 使用一个小的容差(1.0f)来确认位置匹配
                        if (mech == null || Vector3.Distance(mech.Position, activatorPos) > 1.0f)
                        {
                            accessory.Log.Error($"在位置 {activatorPos} 附近找不到匹配的待处理机制。");
                            return;
                        }

                        // 从列表中移除已处理的机制
                        _pendingMechanics.Remove(mech);
                        accessory.Log.Debug($"匹配并移除机制: 类型={mech.ShapeActionId}, 原始位置={mech.Position}");
                    }

                    // 从列表中移除已处理的机制
                    _pendingMechanics.Remove(mech);
                    accessory.Log.Debug($"匹配并移除机制: 类型={mech.ShapeActionId}, 位置={mech.Position}");

                    // 计算从现在到激活时间的剩余毫秒数
                    var remainingTime = (int)(mech.ActivationTime - DateTime.Now).TotalMilliseconds;
                    if (remainingTime < 0) remainingTime = 0;

                    switch (mech.ShapeActionId)
                    {
                        // 绘制十字AOE
                        case 41377:
                            {
                                var baseName = $"鬼火苗_Cross_{husk.EntityId}";
                                DrawCrossAOE(accessory, baseName, huskPos, husk.Rotation, remainingTime, 5000);
                                accessory.Log.Debug($"绘制十字AOE: Name={baseName}, Delay={remainingTime}ms");
                                break;
                            }
                        // 绘制月环AOE
                        case 41374:
                            {
                                var dp = accessory.Data.GetDefaultDrawProperties();
                                dp.Name = $"鬼火苗_Donut_{husk.EntityId}";
                                dp.Position = huskPos;
                                dp.Scale = new Vector2(50);
                                dp.InnerScale = new Vector2(7);
                                dp.Radian = 360 * MathF.PI / 180.0f;
                                dp.Color = accessory.Data.DefaultDangerColor;
                                dp.Delay = remainingTime;
                                dp.DestoryAt = 5000;
                                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                                accessory.Log.Debug($"绘制月环AOE: Name={dp.Name}, Delay={dp.Delay}ms");
                                break;
                            }
                        // 绘制击退预测
                        case 41379:
                            {
                                var dp = accessory.Data.GetDefaultDrawProperties();
                                dp.Name = $"鬼火苗_Knockback_{husk.EntityId}";
                                dp.Owner = accessory.Data.Me; // 从玩家自己开始绘制
                                dp.TargetPosition = huskPos;  // 击退源是外壳的位置
                                dp.Rotation = 20f;            // 击退距离为20
                                dp.Scale = new Vector2(2f, 1f); // 线条宽度2，末端圆圈半径1
                                dp.Color = new(0.3f, 1.0f, 0f, 1.5f);
                                dp.Delay = remainingTime;
                                dp.DestoryAt = 5000; // 持续时间

                                // 发送位移绘制指令
                                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
                                accessory.Log.Debug($"绘制击退预测: Name={dp.Name}, From=Me, To={huskPos}, Distance=20, Delay={dp.Delay}ms");
                                break;
                            }
                    }
                }
        */
        [ScriptMethod(
            name: "Boss直接AOE（鬼火苗）",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(42034|42032)$"]
        )]
        public void OnBossAOE(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            if (@event.ActionId == 42034)
            {
                var baseName = "鬼火苗_Boss_Cross";
                DrawCrossAOE(accessory, baseName, caster.Position, caster.Rotation, 0, 5800);
                accessory.Log.Debug($"绘制Boss直接十字AOE: {baseName}");
            }
            else // ShadesNestBoss
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "鬼火苗_Boss_Donut";
                dp.Owner = @event.SourceId;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Radian = 360 * MathF.PI / 180.0f;
                dp.DestoryAt = 5800;
                dp.Scale = new Vector2(50);
                dp.InnerScale = new Vector2(7);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                accessory.Log.Debug($"绘制Boss直接月环AOE: {dp.Name}");
            }
        }
/*
        [ScriptMethod(
            name: "移除绘图（鬼火苗）",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(42035|42033|41397|42034|42032)$"],
            userControl: false
        )]
        public void OnCastFinished(Event @event, ScriptAccessory accessory)
        {
            switch (@event.ActionId)
            {
                case 42035:
                case 42034:
                    var crossName = @event.ActionId == 42035 ? $"鬼火苗_Cross_{@event.SourceId}" : "鬼火苗_Boss_Cross";
                    accessory.Method.RemoveDraw($"{crossName}_1");
                    accessory.Method.RemoveDraw($"{crossName}_2");
                    accessory.Log.Debug($"移除十字绘图: {crossName}");
                    break;

                case 42033:
                    accessory.Method.RemoveDraw($"鬼火苗_Donut_{@event.SourceId}");
                    accessory.Log.Debug($"移除月环绘图: 鬼火苗_Donut_{@event.SourceId}");
                    break;

                case 42032:
                    accessory.Method.RemoveDraw("鬼火苗_Boss_Donut");
                    accessory.Log.Debug("移除Boss月环绘图");
                    break;

                case 41397:
                    accessory.Method.RemoveDraw($"鬼火苗_Knockback_{@event.SourceId}");
                    accessory.Log.Debug($"移除击退绘图: 鬼火苗_Knockback_{@event.SourceId}");
                    break;
            }
        }
*/

        private void DrawCrossAOE(ScriptAccessory accessory, string baseName, Vector3 position, float rotation, int delay, int duration)
        {
            // 水平部分
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = $"{baseName}_1";
            dp1.Position = position;
            dp1.Rotation = rotation;
            dp1.Scale = new Vector2(15, 200); // 宽度15, 长度 200
            dp1.Color = accessory.Data.DefaultDangerColor;
            dp1.Delay = delay;
            dp1.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);

            // 垂直部分
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = $"{baseName}_2";
            dp2.Position = position;
            dp2.Rotation = rotation + MathF.PI / 2; // 旋转90度
            dp2.Scale = new Vector2(15, 100);
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.Delay = delay;
            dp2.DestoryAt = duration;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
        }

        #endregion
        #region 尼姆瓣齿鲨
        [ScriptMethod(
            name: "Hydrocleave (尼姆瓣齿鲨)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:43149"]
        )]
        public void HydrocleaveDraw (Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "SharkAttack_Hydrocleave_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(50);
            dp.Radian = 60 * MathF.PI / 180.0f; 
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        [ScriptMethod(
            name: "潮汐断头台(尼姆瓣齿鲨)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41723"]
        )]
        public void OnTidalGuillotineFirstCast(Event @event, ScriptAccessory accessory)
        {
            // 清空旧的列表，开始新序列
            _tidalGuillotineAoes.Clear();

            var pos = JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);
            var castTime = 8000;
            var name = $"TidalGuillotine_AOE_0";

            // 记录第一个AOE
            _tidalGuillotineAoes.Add((name, castTime));

            // 绘制第一个AOE
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = pos;
            dp.Scale = new Vector2(20);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = castTime + 1000; // 咏唱结束后1秒销毁
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            accessory.Log.Debug($"记录了第一个潮汐断头台AOE，位置: {pos}");
        }
        [ScriptMethod(
            name: "潮汐断头台 - 记录后续AOE(尼姆瓣齿鲨)",
            eventType: EventTypeEnum.ActionEffect, 
            eventCondition: ["ActionId:41682"]
        )]
        public void OnTidalGuillotineTeleport(Event @event, ScriptAccessory accessory)
        {
            if (_tidalGuillotineAoes.Count >= 3) return; // 最多3个

            var pos = JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
            var index = _tidalGuillotineAoes.Count;
            var name = $"TidalGuillotine_AOE_{index}";

            // 根据是第几个AOE来决定延迟时间
            var delay = index == 1 ? 8700 : 9900;

            // 记录后续的AOE
            _tidalGuillotineAoes.Add((name, delay));

            // 绘制后续的AOE
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = name;
            dp.Position = pos;
            dp.Scale = new Vector2(20);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = delay - 5000;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            accessory.Log.Debug($"记录了第 {index + 1} 个潮汐断头台AOE，位置: {pos}");
        }
        [ScriptMethod(
            name: "旋转月环-触发",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:/*OpenWaterVisualFirst_Shared_AID*/"] // TODO: 替换为内外圈共用的触发技能ID
        )]
        public void OnOpenWater(Event @event, ScriptAccessory accessory)
        {
            _openWaterCastStartTime = DateTime.Now; // 记录咏唱开始时间
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            var dir = caster.Position - SharkArenaCenter;
            var isCasterInside = dir.LengthSquared() < 15 * 15; // 半径15为内外圈分界线

            if (isCasterInside)
            {
                // 内圈模式
                // 参数: 总共8次AOE, 每次间隔1.2秒, 每次旋转22.5度, 内圈模式
                HandleOpenWater(accessory, @event, 35, 1200, 22.5f * MathF.PI / 180.0f, true);
            }
            else
            {
                // 外圈模式
                // 参数: 总共12次AOE, 每次间隔0.8秒, 每次旋转12.5度, 外圈模式
                HandleOpenWater(accessory, @event, 59, 800, 12.5f * MathF.PI / 180.0f, false);
            }
        }
        private void HandleOpenWater(ScriptAccessory accessory, Event @event, int maxCasts, int timeToMoveMs, float increment, bool isInner)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            // 清理旧状态
            _openWaterAoes.Clear();
            _openWaterCastsDone = 0;

            var dir = caster.Position - SharkArenaCenter;

            // 从caster的旋转值（弧度）计算出其正前方的向量
            var forwardVector = new Vector3(MathF.Sin(caster.Rotation), 0, MathF.Cos(caster.Rotation));

            // 判断旋转方向（顺时针/逆时针）
            var rotationDirection = (Vector3.Cross(dir, forwardVector).Y < 0) ? 1.0f : -1.0f;
            var rotationIncrement = rotationDirection * increment;

            var initialCastTime = (int)(JsonConvert.DeserializeObject<float>(@event["CastTime"]) * 1000);

            // 预先计算所有AOE的位置和时间
            for (int i = 0; i < maxCasts; i++)
            {
                var rotationAngle = rotationIncrement * i;
                var rotatedDir = new Vector3(
                    dir.X * MathF.Cos(rotationAngle) - dir.Z * MathF.Sin(rotationAngle),
                    dir.Y,
                    dir.X * MathF.Sin(rotationAngle) + dir.Z * MathF.Cos(rotationAngle)
                );
                var aoePosition = SharkArenaCenter + rotatedDir;
                var aoeRadius = isInner ? 4f : 5f;
                var aoeDelay = initialCastTime + (timeToMoveMs * i);
                _openWaterAoes.Add((aoePosition, aoeRadius, aoeDelay));
            }

            accessory.Log.Debug($"旋转月环已启动。模式: {(isInner ? "内圈" : "外圈")}, 数量: {maxCasts}, 旋转方向: {(rotationDirection > 0 ? "顺时针" : "逆时针")}");
            DrawOpenWaterAoes(accessory);
        }
        private void DrawOpenWaterAoes(ScriptAccessory accessory)
        {
            // 先移除所有旧的“旋转月环”绘图，以便刷新
            accessory.Method.RemoveDraw("OpenWater_AOE_.*");

            var aoesToDraw = _openWaterAoes.Skip(_openWaterCastsDone).Take(8); // 最多显示未来的5个圈
            int index = _openWaterCastsDone;

            foreach (var aoe in aoesToDraw)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"OpenWater_AOE_{index}";
                dp.Position = aoe.Position;
                dp.Scale = new Vector2(aoe.Radius);

                // 高亮下一个要爆炸的圈
                if (index == _openWaterCastsDone)
                {
                    dp.Color = new Vector4(1, 0, 0, 0.8f); // 危险颜色
                }
                else
                {
                    dp.Color = accessory.Data.DefaultDangerColor; // 普通警告颜色
                }

                // 计算从现在到爆炸的剩余时间作为延迟
                var remainingTime = aoe.Delay - (int)(DateTime.Now - _openWaterCastStartTime).TotalMilliseconds;
                if (remainingTime < 0) remainingTime = 0;

                dp.Delay = remainingTime;
                dp.DestoryAt = 2000; // 显示2秒
                dp.ScaleMode |= ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                index++;
            }
        }
        [ScriptMethod(
            name: "旋转月环-AOE爆炸",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:43151"],
            userControl: false
        )]
        public void OnOpenWaterCast(Event @event, ScriptAccessory accessory)
        {
            // 检查触发伤害的AOE是否与当前机制匹配
            var casterPos = @event.SourcePosition;
            var isCasterInside = (casterPos - SharkArenaCenter).LengthSquared() < 15 * 15;
            if (_openWaterAoes.Count > 0 && (isCasterInside == (_openWaterAoes[0].Radius < 5)))
            {
                _openWaterCastsDone++;
                if (_openWaterCastsDone >= _openWaterAoes.Count)
                {
                    // 机制结束，清理
                    _openWaterAoes.Clear();
                    _openWaterCastsDone = 0;
                    accessory.Method.RemoveDraw("OpenWater_AOE_.*");
                    accessory.Log.Debug("旋转月环机制结束。");
                }
                else
                {
                    // 刷新显示的AOE
                    DrawOpenWaterAoes(accessory);
                }
            }
        }


        #endregion
        #region 城塞守卫
        private const uint AID_AncientAeroIII_5s = 41287;  // 古代疾风III (5s咏唱)
        private const uint AID_AncientAeroIII_12s = 41292; // 古代疾风III (12s咏唱)
        private const uint AID_AncientStoneIII_5s = 41289; // 古代垒石III (5s咏唱)
        private const uint AID_AncientStoneIII_12s = 41293; // 古代垒石III (12s咏唱)
        private const uint AID_WindSurge = 41295; // 风之涌流 (AOE爆炸技能)
        private const uint AID_SandSurge = 41296; // 沙之涌流 (AOE爆炸技能)
        private const uint AID_AncientHoly1 = 41284;       // 神圣
        private const uint AID_LightSurge = 41294; // 光之涌流

        [ScriptMethod(
            name: "圣焰（城塞守卫）",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41297"]
        )]
        public void 圣焰Draw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "城塞守卫_圣焰_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(5, 60);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 4000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(
            name: "风石光 - 记录能量球",
            eventType: EventTypeEnum.AddCombatant,
            eventCondition: ["DataId:18125"],
            userControl: false
        )]
        public void OnSphereCreated(Event @event, ScriptAccessory accessory)
        {
            var sphere = accessory.Data.Objects.SearchById(@event.SourceId);
            if (sphere != null)
            {
                lock (_surgeLock)
                {
                    _spheres.Add(sphere);
                }
                accessory.Log.Debug($"记录一个新的能量球: {sphere.Name}, ID: {sphere.EntityId}");
            }
        }
        [ScriptMethod(
            name: "风石光 - 能量球属性分类",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:2536"],
            userControl: false
        )]
        public void OnSphereStatusGain(Event @event, ScriptAccessory accessory)
        {
            var sphere = accessory.Data.Objects.SearchById(@event.TargetId);
            if (sphere == null) return;

            // status.Extra (Param) 用于区分风(0x224)或石(0x225)
            // 请将 "876" 和 "877" 替换为真实的 Param 值
            var statusParam = @event["Param"];

            lock (_surgeLock)
            {
                if (!_spheres.Contains(sphere)) return; // 在锁内检查

                if (statusParam == "548") // 假设 "876" 对应风属性 (0x224)
                {
                    _spheresWind.Add(sphere);
                    _spheres.Remove(sphere);
                    accessory.Log.Debug($"能量球 {sphere.EntityId} 被分类为 [风]");
                }
                else if (statusParam == "549") // 假设 "877" 对应石属性 (0x225)
                {
                    _spheresStone.Add(sphere);
                    _spheres.Remove(sphere);
                    accessory.Log.Debug($"能量球 {sphere.EntityId} 被分类为 [石]");
                }
            }
        }

        [ScriptMethod(
            name: "风石光 - 预测并绘制AOE",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41287|41292|41289|41293)$"]
        )]
        public void OnAncientSpellCast(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            // 根据ActionId判断技能类型（风/石）和咏唱时间
            List<IGameObject> originalSphereList;
            int castTimeMs;

            switch (@event.ActionId)
            {
                case AID_AncientAeroIII_5s:
                    originalSphereList = _spheresWind;
                    castTimeMs = 5000;
                    break;
                case AID_AncientAeroIII_12s:
                    originalSphereList = _spheresWind;
                    castTimeMs = 12000;
                    break;
                case AID_AncientStoneIII_5s:
                    originalSphereList = _spheresStone;
                    castTimeMs = 5000;
                    break;
                case AID_AncientStoneIII_12s:
                    originalSphereList = _spheresStone;
                    castTimeMs = 12000;
                    break;
                default:
                    return; // 不应该发生
            }

            accessory.Log.Debug($"检测到BOSS咏唱 {@event.ActionId} (咏唱时间: {castTimeMs}ms), 开始检测范围内的能量球。");

            // 在锁内创建列表副本以进行安全的迭代
            List<IGameObject> spheresToCheck;
            lock (_surgeLock)
            {
                if (originalSphereList.Count == 0) return;
                spheresToCheck = new List<IGameObject>(originalSphereList);
            }

            // 定义一个前方的锥形判定区 (40码长, 60度角)
            var coneAngle = 60 * MathF.PI / 180.0f;
            var coneLength = 40f;

            // 遍历副本，避免在迭代时修改集合
            foreach (var sphere in spheresToCheck)
            {
                // --- 锥形范围检测逻辑 ---
                var vectorToSphere = sphere.Position - caster.Position;
                var distance = vectorToSphere.Length();

                if (distance > 0 && distance < coneLength)
                {
                    var casterForward = new Vector3(MathF.Sin(caster.Rotation), 0, MathF.Cos(caster.Rotation));
                    var dotProduct = Vector3.Dot(Vector3.Normalize(vectorToSphere), Vector3.Normalize(casterForward));
                    var angleToSphere = MathF.Acos(Math.Clamp(dotProduct, -1.0f, 1.0f));

                    if (Math.Abs(angleToSphere) < coneAngle / 2)
                    {
                        // 球在锥形范围内，绘制AOE
                        accessory.Log.Debug($"能量球 {sphere.EntityId} 在攻击范围内，准备绘制AOE。");

                        var dp = accessory.Data.GetDefaultDrawProperties();
                        var drawName = $"Surge_AOE_{sphere.EntityId}";

                        dp.Name = drawName;
                        dp.Position = sphere.Position;
                        dp.Scale = new Vector2(15); // AOE半径15
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.Delay = castTimeMs - 2400;
                        dp.DestoryAt = 7800;
                        dp.ScaleMode |= ScaleMode.ByTime;

                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                        // 在锁内修改原始列表
                        lock (_surgeLock)
                        {
                            _surgeAoes.Add((sphere.EntityId, drawName));
                            originalSphereList.Remove(sphere);
                        }
                    }
                }
            }
        }

        [ScriptMethod(
            name: "风石光 - 神圣引爆光球",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41284"]
        )]
        public void OnAncientHolyCast(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"检测到BOSS咏唱神圣 ({@event.ActionId}), 引爆所有剩余光球。");

            List<IGameObject> lightSpheres;
            lock (_surgeLock)
            {
                if (_spheres.Count == 0) return;
                // 剩余在 _spheres 列表中的就是光球
                lightSpheres = new List<IGameObject>(_spheres);
            }

            // 爆炸时间 = 咏唱时间 (event.CastTime是秒) + 2.4秒额外延迟
            var castTimeMs = 11000;
            var explosionDelay = castTimeMs - 2400;

            foreach (var sphere in lightSpheres)
            {
                accessory.Log.Debug($"光球 {sphere.EntityId} 将在 {explosionDelay}ms 后爆炸。");

                var dp = accessory.Data.GetDefaultDrawProperties();
                var drawName = $"Surge_AOE_Light_{sphere.EntityId}";

                dp.Name = drawName;
                dp.Position = sphere.Position;
                dp.Scale = new Vector2(15);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = explosionDelay;
                dp.DestoryAt = 7800;
                dp.ScaleMode |= ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                lock (_surgeLock)
                {
                    _surgeAoes.Add((sphere.EntityId, drawName));
                }
            }

            // 所有光球都被消耗
            lock (_surgeLock)
            {
                _spheres.Clear();
            }
        }

        [ScriptMethod(
            name: "风石光 - 清理已爆炸的AOE",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(41296|41295|41294)$"],
            userControl: false
        )]
        public void OnSurgeExplosion(Event @event, ScriptAccessory accessory)
        {
            var explodingSphereId = @event.SourceId;

            // 查找并移除与爆炸能量球匹配的绘图
            var aoeToRemove = _surgeAoes.FirstOrDefault(aoe => aoe.ActorID == explodingSphereId);
            if (aoeToRemove != default)
            {
                accessory.Method.RemoveDraw(aoeToRemove.DrawName);
                _surgeAoes.Remove(aoeToRemove);
                accessory.Log.Debug($"清理AOE绘制: {aoeToRemove.DrawName}");
            }
        }
        public void ResetWindStoneLightSurgeState()
        {
            lock (_surgeLock)
            {
                _surgeAoes.Clear();
                _spheres.Clear();
                _spheresStone.Clear();
                _spheresWind.Clear();
            }
        }
        #endregion






    }
}