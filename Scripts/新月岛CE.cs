using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;
using ECommons;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using Newtonsoft.Json;

namespace EurekaOrthosCeScripts
{
    [ScriptType(
        name: "新月岛CE",
        guid: "15725518-8F8E-413A-BEA8-E19CC861CF93",
        territorys: [9999], //等国服更新
        version: "0.0.1",
        author: "XSZYYS",
        note: "用于新月岛紧急遭遇战。"
    )]
    public class NewMoonIslandCeScript
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
            dp.Scale = new Vector2(30);
            dp.InnerScale = new Vector2(8);
            dp.Radian = MathF.PI * 2;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 7000;
            dp.ScaleMode |= ScaleMode.ByTime; 

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
            name: "指令 (With Extreme Prejudice)",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:regex:^(303|304|306)$"]
        )]
        public void RockSlideStoneSwell(Event @event, ScriptAccessory accessory)
        {
            uint tetherId = uint.Parse(@event["Id"]);

            switch (tetherId)
            {
                case 303: 
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"ExtremePrejudice_Circle_{@event.TargetId}";
                        dp.Owner = @event.TargetId;
                        dp.Scale = new Vector2(16);
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.DestoryAt = 6100;
                        dp.ScaleMode |= ScaleMode.ByTime;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                        break;
                    }
                case 304: 
                    {
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
                        break;
                    }
                case 306: 
                    {
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"ExtremePrejudice_Circle_{@event.SourceId}";
                        dp.Owner = @event.SourceId;
                        dp.Scale = new Vector2(16);
                        dp.Color = accessory.Data.DefaultDangerColor;
                        dp.DestoryAt = 6100;
                        dp.ScaleMode |= ScaleMode.ByTime;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                        break;
                    }
            }
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
        }
        [ScriptMethod(
            name: "冲锋-小鸟记录 (Noise Complaint)",
            eventType: EventTypeEnum.TargetIcon,
            eventCondition: ["Id:578"],
            userControl: false
        )]
        public void RushingRumbleRampage_Icon(Event @event, ScriptAccessory accessory)
        {
            _activeBirds.Enqueue(@event.SourceId);
            accessory.Log.Debug($"小鸟ID:{@event.SourceId} 已记录。队列中的小鸟数量：{_activeBirds.Count}");


        }
        
        [ScriptMethod(
            name: "RushingRumble (Noise Complaint)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41175"]
        )]
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

        [ScriptMethod(
            name: "狂鸟冲锋 (Noise Complaint)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41176"]
        )]
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

        [ScriptMethod(
            name: "Rushing Rumble Rampage (Noise Complaint)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41177"]
        )]

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
    }
}
