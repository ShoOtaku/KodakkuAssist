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
        version: "0.0.10",
        author: "XSZYYS",
        note: "用于新月岛紧急遭遇战，进化加鲁拉的运动会已完成。"
    )]
    public class 新月岛CE
    {
        /// <summary>
        /// 脚本初始化
        /// </summary>
        // --- Noise Complaint 状态变量 ---
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
        private bool _isRampageSequenceRunning;
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
            // 初始化Noise Complaint机制的状态
            //_noiseComplaintArenaCenter = null;
            //_noiseComplaintCenterRecorded = false;
            _lightningIsCardinal = null;
            _activeBirds.Clear();
        }

        // --- Black Regiment ---


        [ScriptMethod(
            name: "陆行鸟冲 (Black Regiment)",
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
            name: "陆行鸟乱羽 (Black Regiment)",
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
            name: "陆行鸟风暴 (Black Regiment)",
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
            name: "陆行鸟气旋 (Black Regiment)",
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
            name: "陆行鸟屠杀 (Black Regiment)",
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

                int explosionTime = firstExplosionTime + (i * subsequentInterval);

                dp.Delay = explosionTime - warningDuration;
                dp.DestoryAt = warningDuration + lingerDuration;
                dp.ScaleMode |= ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }

        [ScriptMethod(
            name: "神秘热量 (From Times Bygone)",
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
            name: "大爆炸 (From Times Bygone)",
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
            name: "死亡射线 (From Times Bygone)",
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
            name: "钢铁之击 (From Times Bygone)",
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
            name: "奥术之球 (From Times Bygone)",
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

        // --- With Extreme Prejudice ---


        [ScriptMethod(
            name: "指令 - 圆形 (目标) (With Extreme Prejudice)",
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
            name: "指令 - 十字 (目标) (With Extreme Prejudice)",
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
            name: "指令 - 圆形 (目标) (With Extreme Prejudice)",
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
        #region Noise Complaint
        // --- Noise Complaint ---


        [ScriptMethod(
            name: "场地中心记录 (Noise Complaint)",
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
                accessory.Log.Debug($"Noise Complaint Arena Center recorded at: {_noiseComplaintArenaCenter}");
            }
        }

        [ScriptMethod(
            name: "雷圈 (Noise Complaint)",
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
            name: "核爆 (Noise Complaint)",
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
            name: "雷光十字 (Noise Complaint)",
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
            name: "猛挥 (Noise Complaint)",
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
            name: "冲锋-方向记录 (Noise Complaint)",
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
            name: "冲锋-小鸟记录 (Noise Complaint)",
            eventType: EventTypeEnum.TargetIcon,
            eventCondition: ["Id:0242"],
            userControl: false
        )]
        public void RushingRumbleRampage_Icon(Event @event, ScriptAccessory accessory)
        {
            _activeBirds.Enqueue(@event.TargetId);
            accessory.Log.Debug($"小鸟ID:{@event.TargetId} 已记录。队列中的小鸟数量：{_activeBirds.Count}");
            TryDrawMechanics(accessory);


        }

        [ScriptMethod(
            name: "RushingRumble (Noise Complaint)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41175"]
        )]
        public void OnRushingRumbleStart(Event @event, ScriptAccessory accessory)
        {
            // 只记录技能ID和BossID，不立即绘制
            _activeMechanicId = 41175;
            _bossId = @event.SourceId;
            // 尝试触发绘制，以防小鸟和方向信息已经先到了
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
            name: "狂鸟冲锋 (Noise Complaint)",
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
            name: "Rushing Rumble Rampage (Noise Complaint)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41177"]
        )]
        public void OnRampageStart(Event @event, ScriptAccessory accessory)
        {
            _activeMechanicId = 41177;
            _bossId = @event.SourceId;
            TryDrawMechanics(accessory);
        }
        private void TryDrawMechanics(ScriptAccessory accessory)
        {
            // 如果没有激活的技能，直接返回
            if (_activeMechanicId == 0) return;

            // 根据激活的技能ID，检查其特定条件
            switch (_activeMechanicId)
            {
                // 单次冲锋：需要方向和小鸟
                case 41175 when _lightningIsCardinal != null && _activeBirds.Count > 0:
                    DrawRushingRumble(accessory);
                    ResetState(); // 执行后重置状态
                    break;

                // 狂鸟冲锋：只需要小鸟
                case 41176 when _activeBirds.Count > 0:
                    DrawBirdserkRush(accessory);
                    ResetState();
                    break;

                // 连续冲锋：需要方向和小鸟，且序列尚未开始
                case 41177 when _lightningIsCardinal != null && _activeBirds.Count > 0 && !_isRampageSequenceRunning:
                    _isRampageSequenceRunning = true; // 标记序列已开始，防止重复启动
                    _ = StartRampageSequenceAsync(accessory); // 异步启动序列
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
        }
        private void DrawRushingRumble(ScriptAccessory accessory)
        {
            var bird = accessory.Data.Objects.SearchById(_activeBirds.Dequeue());
            var boss = accessory.Data.Objects.SearchById(_bossId);
            if (bird == null || boss == null) return;

            var destination = bird.Position - boss.Position;

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
        }

        private void DrawBirdserkRush(ScriptAccessory accessory)
        {
            var bird = accessory.Data.Objects.SearchById(_activeBirds.Dequeue());
            if (bird == null) return;

            // 绘制冲锋路径
            var dpCharge = accessory.Data.GetDefaultDrawProperties();
            dpCharge.Name = $"NoiseComplaint_BirdserkRush_Charge_{_bossId}";
            dpCharge.Owner = _bossId;
            dpCharge.TargetObject = bird.EntityId;
            dpCharge.Scale = new Vector2(8, 100);
            dpCharge.ScaleMode |= ScaleMode.YByDistance | ScaleMode.ByTime;
            dpCharge.Color = accessory.Data.DefaultDangerColor;
            dpCharge.DestoryAt = 6300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpCharge);

            // 绘制最终的扇形AOE
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
            name: "绘图移除 (Noise Complaint)",
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

        #region CrawlingDeath
        [ScriptMethod(
            name: "爪痕 (CrawlingDeath)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41315|41316|41317)$"]
        )]

        public void LethalNails(Event @event, ScriptAccessory accessory)
        {
            var nailId = @event.ActionId;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"CrawlingDeath_LethalNails_{nailId}";
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
            name: "垂直交错 (CrawlingDeath)",
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
                dp.Name = $"CrawlingDeath_Crosshatch_{names[i]}_{@event.SourceId}";
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
            name: "水平交错 (CrawlingDeath)",
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
                dp.Name = $"CrawlingDeath_Crosshatch_{names[i]}_{@event.SourceId}";
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
            name: "SkulkingOrders (CrawlingDeath)(未完成）",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId: regex: ^(41326|41329)$"]
        )]
        public void SkulkingOrders(Event @event, ScriptAccessory accessory)
        {
            
        }
        */

        #endregion

        #region TrialByClaw
        [ScriptMethod(
            name: "PrismaticWing（钢铁月环）(TrialByClaw)",
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
                        dp.Name = $"TrialByClaw_PrismaticWing_{AID}";
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
                        dp.Name = $"TrialByClaw_PrismaticWing_{AID}";
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
                        dp.Name = $"TrialByClaw_PrismaticWing_{AID}";
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
                        dp.Name = $"TrialByClaw_PrismaticWing_{AID}";
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
            name: "结晶能量/混沌 (TrialByClaw)",
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

        #region Unbridled
        [ScriptMethod(
           name: "严厉扫荡 (Unbridled)",
           eventType: EventTypeEnum.StartCasting,
           eventCondition: ["ActionId:42691"]
       )]

        public void ScathingSweep(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Unbridled_Sweep_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(60, 60);
            dp.Color = new Vector4(0.957f, 0.140f, 0.140f, 0.8f);
            dp.DestoryAt = 6000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        [ScriptMethod(
            name: "狂怒1(Unbridled)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:37323"]
        )]
        public void UnbridledRage1(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Unbridled_Rage1_Danger_Zone";
            dp.Position = @event.TargetPosition;
            dp.Scale = new Vector2(8, 8);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 8000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "Unbridled_BedrockUplift1_Danger_Zone";
            dp2.Position = @event.TargetPosition;
            dp2.Scale = new Vector2(60, 60);
            dp2.InnerScale = new Vector2(8, 8);
            dp2.Color = new Vector4(0.957f, 0.140f, 0.140f, 0.8f);
            dp2.Radian = 360 * MathF.PI / 180.0f;
            dp2.Delay = 6500;
            dp2.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
        }
        [ScriptMethod(
            name: "狂怒2(Unbridled)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:30872"]
        )]
        public void UnbridledRage2(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Unbridled_Rage2_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(24, 24);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 9000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "Unbridled_BedrockUplift2_Danger_Zone";
            dp2.Owner = @event.SourceId;
            dp2.Scale = new Vector2(60, 60);
            dp2.InnerScale = new Vector2(24, 24);
            dp2.Radian = 360 * MathF.PI / 180.0f;
            dp2.Color = new Vector4(0.957f, 0.140f, 0.140f, 0.8f);
            dp2.Delay = 7500;
            dp2.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
        }
        [ScriptMethod(
            name: "狂怒3(Unbridled)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:30873"]
        )]
        public void UnbridledRage3(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Unbridled_Rage3_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(16, 16);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 9000;
            dp.DestoryAt = 6500;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "Unbridled_BedrockUplift3_Danger_Zone";
            dp2.Owner = @event.SourceId;
            dp2.Scale = new Vector2(60, 60);
            dp2.InnerScale = new Vector2(16, 16);
            dp2.Radian = 360 * MathF.PI / 180.0f;
            dp2.Color = new Vector4(0.957f, 0.140f, 0.140f, 0.8f);
            dp2.Delay = 14000;
            dp2.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
        }
        [ScriptMethod(
            name: "狂怒4(Unbridled)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:30874"]
        )]
        public void UnbridledRage4(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Unbridled_Rage4_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(8, 8);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 12000;
            dp.DestoryAt = 6500;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "Unbridled_BedrockUplift4_Danger_Zone";
            dp2.Owner = @event.SourceId;
            dp2.Scale = new Vector2(60, 60);
            dp2.InnerScale = new Vector2(16, 16);
            dp2.Color = new Vector4(0.957f, 0.140f, 0.140f, 0.8f);
            dp2.Radian = 360 * MathF.PI / 180.0f;
            dp2.Delay = 21000;
            dp2.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
        }
        [ScriptMethod(
            name: "激烈爆发(Unbridled)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:37804"]
        )]
        public void UnbridledFury(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "Unbridled_Fury_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(13, 13);
            dp.Color = new Vector4(0.957f, 0.140f, 0.140f, 0.8f);
            dp.DestoryAt = 6000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        #endregion
        #region CalamityBound
        [ScriptMethod(
            name: "爆炸 (CalamityBound)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41357"]
        )]
        public void Explosion(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "CalamityBound_Explosion_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(22, 22);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(
            name: "潮汐吐息(CalamityBound)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41360"]
        )]
        public void TidalBreath(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "CalamityBound_TidalBreath_Danger_Zone";
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
            eventCondition: ["ActionId:41828"] // 技能ID Dualfist Flurry
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
            const int totalExplosions = 9;      // 总共3次爆炸
            const float radius = 6f;            // 爆炸半径
            const float stepDistance = 7f;      // 每次前进距离
            const int firstExplosionTime = 6000;// 第一次爆炸时间 (基于6秒咏唱)
            const int subsequentInterval = 1100;// 后续爆炸间隔
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
    }
}