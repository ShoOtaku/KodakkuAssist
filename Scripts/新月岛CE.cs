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
        version: "0.1.6",
        author: "XSZYYS",
        note: "新月岛CE绘制已完成"
    )]
    public class 新月岛CE
    {
        // =================================================================================
        // ============================= 字段和状态变量定义 =============================
        // =================================================================================

        // -------------------- 机制通用锁对象 --------------------
        private readonly object _mechanicLock = new(); // 通用机制处理锁，防止多线程冲突
        private readonly object _surgeLock = new();    // 城塞守卫能量球机制专用锁
        private readonly object _trapLock = new();     // 夺心魔陷阱机制专用锁

        // ==================== 进化加鲁拉 ====================
        private ulong _bossId; // Boss的实体ID
        private Vector3? _noiseComplaintArenaCenter; // 场地中心点
        private bool _noiseComplaintCenterRecorded; // 是否已记录场地中心
        private bool? _lightningIsCardinal; // 记录雷电方向：true为基本方向(东西南北)，false为斜角方向(东南/东北/西南/西北)
        private readonly Queue<ulong> _activeBirds = new(); // 存储被点名小鸟ID的队列
        private int _activeMechanicId; // 当前正在处理的冲锋机制ID

        // --- “连续突进跺地”  专用状态变量 ---
        private bool _isRampageSequenceRunning; // 连续冲锋序列是否正在进行
        private int _rampageChargeIndex; // 连续冲锋的当前次数索引
        private Vector3 _rampageNextChargeStartPos; // 下一次连续冲锋的起始位置
        // ==================== 死亡爪 ====================
        private static readonly Vector3 DeathclawArenaCenter = new(681f, 74f, 534f);
        // ==================== 城塞守卫  ====================
        private readonly List<IGameObject> _spheres = new(12);       // 存储所有未分类能量球的列表
        private readonly List<IGameObject> _spheresStone = new(6);   // 专门存储“石”属性能量球的列表
        private readonly List<IGameObject> _spheresWind = new(6);    // 专门存储“风”属性能量球的列表
        private readonly List<(ulong ActorID, string DrawName)> _surgeAoes = new(); // 存储已绘制的能量球AOE，用于后续移除
        private bool _isHolyCasting; // Boss是否正在咏唱神圣(Holy)

        // ==================== 夺心魔  ====================
        private readonly List<FireIceTrapInfo> _fireIceTraps = new(); // 存储当前场上所有火冰陷阱的信息
        private readonly Dictionary<ulong, bool> _playerElements = new(); // 存储玩家当前携带的元素debuff (Key: 玩家ID, Value: true为火, false为冰)

        /// <summary>
        /// 夺心魔火冰陷阱的信息载体
        /// </summary>
        private class FireIceTrapInfo
        {
            public ulong NpcId { get; init; }
            public Vector3 Position { get; set; }
            public bool IsFire { get; init; }
        }

        // ==================== 尼姆瓣齿鲨 ====================
        private static readonly Vector3 SharkArenaCenter = new(-117f, 1, -850f); // 鲨鱼战斗场地中心
        private readonly List<(string Name, int Delay)> _tidalGuillotineAoes = new(3); // 存储“怒潮断头台”系列AOE信息

        // ==================== 跃立狮 ====================
        private static readonly Vector3 OnTheHuntAreaCenter = new Vector3(636f, 108f, -54f); // 跃立狮战斗场地中心

        // =================================================================================
        // ================================= 脚本主体 =================================
        // =================================================================================


        public void Init(ScriptAccessory accessory)
        {
            accessory.Log.Debug("新月岛CE脚本已加载。");
            accessory.Method.RemoveDraw(".*"); // 清理所有旧的绘图

            // 初始化所有机制的状态变量
            _lightningIsCardinal = null;
            _activeBirds.Clear();
            _tidalGuillotineAoes.Clear();

            // 重置进化加鲁拉机制状态
            _noiseComplaintCenterRecorded = false;
            _noiseComplaintArenaCenter = null;
            ResetState();

            // 重置城塞守卫机制状态
            ResetWindStoneLightSurgeState();

            // 重置夺心魔机制状态
            lock (_trapLock)
            {
                _fireIceTraps.Clear();
                _playerElements.Clear();
            }
        }

        // --- 黑色连队 ---


        [ScriptMethod(
            name: "陆行鸟攻击(黑色连队)",
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
            name: "尾羽 (黑色连队)",
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
            name: "陆行鸟旋风 (黑色连队)",
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
            name: "陆行鸟杀戮 (黑色连队)",
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
            name: "魔射线 (神秘土偶)",
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
            name: "飞剑强袭 (神秘土偶)",
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
            dp.Delay = 4200;
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
            eventCondition: ["Id:012E"]
        )]
        public void RockSlideStoneSwell_CircleSource(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"指令 (Tether 012E) 触发: 绘制圆形AOE, 目标: {@event.SourceId}");
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"ExtremePrejudice_Circle_{@event.SourceId}";
            dp.Owner = @event.SourceId;
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
            name: "圆状放雷 (进化加鲁拉)",
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
            name: "大落雷 (进化加鲁拉)",
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
            name: "扇状放雷 (进化加鲁拉)",
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
            name: "掀地 (进化加鲁拉)",
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
            var boss = accessory.Data.Objects.SearchById(@event.TargetId);
            if (boss == null || boss.DataId != 18078) return;
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
                    ResetState(); // 安全重置
                    return;
                }

                // 绘制这一段的连续冲锋，起点是上一次的落点
                DrawRampageCharge(accessory, _rampageNextChargeStartPos, bird, _rampageChargeIndex);

                // 如果所有冲锋都已完成(通常是3次)，则重置状态。
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
            name: "突进跺地 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41175"]
        )]
        public void OnRushingRumbleStart(Event @event, ScriptAccessory accessory)
        {
            _activeMechanicId = 41175;
            _bossId = @event.SourceId;
            TryDrawMechanics(accessory);
        }



        [ScriptMethod(
            name: "突进掀地 (进化加鲁拉)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41176"]
        )]
        public void OnBirdserkRushStart(Event @event, ScriptAccessory accessory)
        {
            _activeMechanicId = 41176;
            _bossId = @event.SourceId;
            TryDrawMechanics(accessory);
        }

        [ScriptMethod(
            name: "连续突进跺地 (进化加鲁拉)",
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
            // 如果一个连续冲锋已经开始(即不是第一次冲锋)，则由图标事件处理，这里直接返回
            if (_isRampageSequenceRunning) return;
            // 如果没有激活的机制ID，也直接返回
            if (_activeMechanicId == 0) return;

            switch (_activeMechanicId)
            {
                // RushingRumble (单次冲锋): 需要方向和小鸟
                case 41175 when _lightningIsCardinal != null && _activeBirds.Count > 0:
                    accessory.Log.Debug("条件满足，绘制 RushingRumble。");
                    DrawRushingRumble(accessory);
                    ResetState();
                    break;

                // BirdserkRush (狂鸟冲锋): 只需要小鸟
                case 41176 when _activeBirds.Count > 0:
                    accessory.Log.Debug("条件满足，绘制 BirdserkRush。");
                    DrawBirdserkRush(accessory);
                    ResetState();
                    break;

                // Rushing Rumble Rampage (连续冲锋) 的启动: 需要方向和小鸟
                case 41177 when _lightningIsCardinal != null && _activeBirds.Count > 0:
                    accessory.Log.Debug("条件满足，启动 Rushing Rumble Rampage 序列。");
                    var boss = accessory.Data.Objects.SearchById(_bossId);
                    if (boss == null) { ResetState(); return; }

                    // 设置连续冲锋状态
                    _isRampageSequenceRunning = true;
                    _rampageChargeIndex = 0;
                    _rampageNextChargeStartPos = boss.Position;

                    // 绘制第一次冲锋
                    if (_activeBirds.TryDequeue(out var birdId))
                    {
                        var bird = accessory.Data.Objects.SearchById(birdId);
                        if (bird != null)
                        {
                            DrawRampageCharge(accessory, _rampageNextChargeStartPos, bird, _rampageChargeIndex);
                        }
                    }
                    // 注意：此处不调用ResetState()，因为序列才刚刚开始
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
        }
        private Vector3 GetArenaEdgePosition(Vector3 destination)
        {
            const float arenaRadius = 23f;
            if (_noiseComplaintArenaCenter == null) return destination;

            var vectorFromCenter = destination - _noiseComplaintArenaCenter.Value;
            if (vectorFromCenter.LengthSquared() <= arenaRadius * arenaRadius)
            {
                return destination;
            }
            else
            {
                var direction = Vector3.Normalize(vectorFromCenter);
                return _noiseComplaintArenaCenter.Value + direction * arenaRadius;
            }
        }

        private Vector3 GetLineArenaIntersection(Vector3 start, Vector3 end, ScriptAccessory accessory)
        {
            const float arenaRadius = 23f;
            if (_noiseComplaintArenaCenter == null) return GetArenaEdgePosition(end);

            Vector2 center = new Vector2(_noiseComplaintArenaCenter.Value.X, _noiseComplaintArenaCenter.Value.Z);
            Vector2 p1 = new Vector2(start.X, start.Z);
            Vector2 p2 = new Vector2(end.X, end.Z);

            if (Vector2.DistanceSquared(p2, center) <= arenaRadius * arenaRadius)
            {
                return end;
            }

            Vector2 d = p2 - p1;
            Vector2 f = p1 - center;

            float a = Vector2.Dot(d, d);
            float b = 2 * Vector2.Dot(f, d);
            float c = Vector2.Dot(f, f) - arenaRadius * arenaRadius;

            float discriminant = b * b - 4 * a * c;

            if (discriminant < 0)
            {
                return GetArenaEdgePosition(end);
            }

            float sqrtDiscriminant = MathF.Sqrt(discriminant);
            float t1 = (-b - sqrtDiscriminant) / (2 * a);
            float t2 = (-b + sqrtDiscriminant) / (2 * a);

            accessory.Log.Debug($"[交点计算] t1: {t1:F3}, t2: {t2:F3}");

            var validIntersections = new List<Vector3>();

            if (t1 >= 0 && t1 <= 1)
            {
                Vector2 intersection2D = p1 + t1 * d;
                var point = new Vector3(intersection2D.X, start.Y, intersection2D.Y);
                validIntersections.Add(point);
                accessory.Log.Debug($"[交点计算] 候选点 1 (来自 t1): {point}");
            }
            if (t2 >= 0 && t2 <= 1)
            {
                Vector2 intersection2D = p1 + t2 * d;
                var point = new Vector3(intersection2D.X, start.Y, intersection2D.Y);
                validIntersections.Add(point);
                accessory.Log.Debug($"[交点计算] 候选点 2 (来自 t2): {point}");
            }

            if (validIntersections.Count == 0)
            {
                accessory.Log.Debug("[交点计算] 路径上无有效交点，启用备用方案。");
                return GetArenaEdgePosition(end);
            }
            if (validIntersections.Count == 1)
            {
                accessory.Log.Debug($"[交点计算] 找到一个有效交点，使用: {validIntersections[0]}");
                return validIntersections[0];
            }

            var dist1 = Vector3.DistanceSquared(validIntersections[0], end);
            var dist2 = Vector3.DistanceSquared(validIntersections[1], end);

            accessory.Log.Debug($"[交点计算] 找到两个有效交点。候选点1到终点距离: {dist1:F2}, 候选点2到终点距离: {dist2:F2}");

            if (dist1 <= dist2)
            {
                accessory.Log.Debug($"[交点计算] 选择候选点 1，因为它更接近终点。");
                return validIntersections[0];
            }
            else
            {
                accessory.Log.Debug($"[交点计算] 选择候选点 2，因为它更接近终点。");
                return validIntersections[1];
            }
        }


        private void DrawRushingRumble(ScriptAccessory accessory)
        {
            if (!_activeBirds.TryDequeue(out var birdId)) return;
            var bird = accessory.Data.Objects.SearchById(birdId);
            var boss = accessory.Data.Objects.SearchById(_bossId);
            if (bird == null || boss == null) return;

            var birdPos = bird.Position;
            var destination = GetArenaEdgePosition(birdPos);
            // 1. 从Boss到小鸟的直线冲锋
            var dpRumble = accessory.Data.GetDefaultDrawProperties();
            dpRumble.Name = $"NoiseComplaint_Rumble_{bird.EntityId}";
            dpRumble.Owner = _bossId;
            dpRumble.TargetPosition = destination;
            dpRumble.Scale = new Vector2(8, 100); // 使用一个足够大的基础长度
            dpRumble.ScaleMode |= ScaleMode.YByDistance; // 让矩形长度自动匹配距离
            dpRumble.Color = accessory.Data.DefaultDangerColor;
            dpRumble.DestoryAt = 6300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpRumble);

            // 2. 落地大圈
            var dpCircle = accessory.Data.GetDefaultDrawProperties();
            dpCircle.Name = $"NoiseComplaint_Rush_Circle_{bird.EntityId}";
            dpCircle.Position = destination;
            dpCircle.Scale = new Vector2(30);
            dpCircle.Color = accessory.Data.DefaultDangerColor;
            dpCircle.DestoryAt = 10400;
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
                dpCone.DestoryAt = 10400;
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
            dpCharge.Scale = new Vector2(8, 100); // 使用一个足够大的基础长度
            dpCharge.ScaleMode |= ScaleMode.YByDistance; // 让矩形长度自动匹配距离
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

            var birdPos = bird.Position;
            Vector3 destination;

            if (chargeIndex == 0)
            {
                destination = GetArenaEdgePosition(birdPos);
            }
            else
            {
                destination = GetLineArenaIntersection(chargeStartPos, birdPos, accessory);
            }


            // 1. 直线冲锋
            var dpCharge = accessory.Data.GetDefaultDrawProperties();
            dpCharge.Name = $"NoiseComplaint_Rampage_Charge_{chargeIndex}";
            dpCharge.Position = chargeStartPos;
            dpCharge.TargetPosition = destination;
            dpCharge.Scale = new Vector2(8, 100); // 使用一个足够大的基础长度
            dpCharge.ScaleMode |= ScaleMode.YByDistance; // 让矩形长度自动匹配距离
            dpCharge.Color = accessory.Data.DefaultDangerColor;
            dpCharge.DestoryAt = 6300;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpCharge);

            // 2. 目标点的大圆形AOE
            var dpCircle = accessory.Data.GetDefaultDrawProperties();
            dpCircle.Name = $"NoiseComplaint_Rampage_Circle_{chargeIndex}";
            dpCircle.Position = destination;
            dpCircle.Scale = new Vector2(30);
            dpCircle.Color = accessory.Data.DefaultDangerColor;
            dpCircle.DestoryAt = 10400;
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
            _rampageNextChargeStartPos = destination;
            _rampageChargeIndex++;
        }

        /*
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
        */
        #endregion

        #region 死亡爪
        [ScriptMethod(
            name: "死亡甲 (死亡爪)",
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
            name: "纵向双重抓 (死亡爪)",
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
            name: "纵向双重抓长 (死亡爪)",
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
            name: "横向双重抓 (死亡爪)",
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
            name: "横向双重抓长 (死亡爪)",
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
        [ScriptMethod(
            name: "利爪(死亡爪)",
            eventType: EventTypeEnum.ObjectEffect,
            eventCondition: ["Id1:16", "Id2:32"]
        )]
        public void LethalClaw(Event @event, ScriptAccessory accessory)
        {
            // 获取释放机制的单位
            var source = accessory.Data.Objects.SearchById(@event.SourceId);

            // 修改：检查释放单位是否在场地21m范围内
            if (source == null || Vector3.Distance(source.Position, DeathclawArenaCenter) > 21f)
            {
                accessory.Log.Debug("释放单位不在死亡爪场地范围内，忽略利爪机制。");
                return; // 如果单位不存在或不在范围内，则不执行后续绘图逻辑
            }

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"死亡爪_LethalClaw_{@event.SourceId}";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(50);
            dp.Radian = 90 * MathF.PI / 180.0f;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 10000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            accessory.Log.Debug($"绘制死亡爪 利爪 AOE: {@event.SourceId}");
        }
        /*
        [ScriptMethod(
            name: "SkulkingOrders (死亡爪)(未完成)",
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
            name: "水晶之翼(钢铁月环)(水晶龙)",
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
            name: "水晶波动/乱流 (水晶龙)",
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
           name: "横砍 (新月狂战士)",
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
            name: "震击怒涛1(新月狂战士)",
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
            name: "震击怒涛2(新月狂战士)",
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
            name: "震击怒涛3(新月狂战士)",
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
            name: "震击怒涛4(新月狂战士)",
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
            name: "气焰(新月狂战士)",
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
            name: "怒潮吐息(回廊恶魔)",
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
        #region 骑士团
        [ScriptMethod(
            name: "重拳崩 (骑士团)",
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
            const float stepDistance = 7f;      // 每次前进距离
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
        [ScriptMethod(
             name: "回旋炮 (骑士团)",
             eventType: EventTypeEnum.StartCasting,
             eventCondition: ["ActionId:regex:^(41823|41822)$"]
         )]
        public void SpinningSiege(Event @event, ScriptAccessory accessory)
        {

            int rotationDirection = (@event.ActionId == 41822) ? -1 : 1;

            const float crossLength = 120f;
            const float crossWidth = 6f;
            const int Duration = 8000;
            const int rotationInterval = 1700;  
            const float rotationAngleDegrees = 9f;
            const float rotationAngleRad = rotationAngleDegrees * MathF.PI / 180.0f;
            const int numberOfSteps = 6;


            for (int i = 0; i < numberOfSteps; i++)
            {
                int delay = 3000 + i * rotationInterval;
                int lifeSpan = Duration - 3000;
                if (lifeSpan <= 0) continue;

                float currentRotation = i * rotationDirection * rotationAngleRad;

                // 绘制十字的第一条直线
                var dp1 = accessory.Data.GetDefaultDrawProperties();
                dp1.Name = $"SpinningSiege_{@event.ActionId}_Cross1_{i}";
                dp1.Position = @event.SourcePosition;
                dp1.Scale = new Vector2(crossWidth, crossLength);
                dp1.Rotation = currentRotation;
                dp1.Color = accessory.Data.DefaultDangerColor;
                dp1.Delay = delay;
                dp1.DestoryAt = lifeSpan;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);

                // 绘制十字的第二条(垂直)直线
                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Name = $"SpinningSiege_{@event.ActionId}_Cross2_{i}";
                dp1.Position = @event.SourcePosition;
                dp2.Scale = new Vector2(crossWidth, crossLength);
                dp2.Rotation = currentRotation + (MathF.PI / 2);
                dp2.Color = accessory.Data.DefaultDangerColor;
                dp2.Delay = delay;
                dp2.DestoryAt = lifeSpan;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
            }
        }


        #endregion
        #region 鬼火苗
        [ScriptMethod(
            name: "分身机制(鬼火苗)",
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
        [ScriptMethod(
            name: "Boss直接AOE(鬼火苗)",
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
            name: "深水切割者 (尼姆瓣齿鲨)",
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
            name: "怒潮断头台(尼姆瓣齿鲨)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41723"]
        )]
        public void OnTidalGuillotineFirstCast(Event @event, ScriptAccessory accessory)
        {
            // 清空旧的列表，开始新序列
            _tidalGuillotineAoes.Clear();

            var pos = JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
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
            name: "怒潮断头台 - 记录后续AOE(尼姆瓣齿鲨)",
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
            name: "开放水域-地火(尼姆瓣齿鲨)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41687"]
        )]
        public void OnOpenWaterRefactored(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            // --- 1. 判断模式并设定参数 ---
            var dirToCaster = caster.Position - SharkArenaCenter;
            var isInnerMode = dirToCaster.LengthSquared() < 15f * 15f;

            int maxCasts;
            int intervalMs;
            float rotationIncrementRad;
            float aoeRadius;

            if (isInnerMode)
            {
                maxCasts = 35;
                intervalMs = 1200;
                rotationIncrementRad = 22.5f * MathF.PI / 180.0f;
                aoeRadius = 4f;
            }
            else
            {
                maxCasts = 59;
                intervalMs = 690;
                rotationIncrementRad = 12f * MathF.PI / 180.0f;
                aoeRadius = 4f;
            }

            // --- 2. 计算旋转方向 ---
            var forwardVector = new Vector3(MathF.Sin(caster.Rotation), 0, MathF.Cos(caster.Rotation));
            var rotationDirection = (Vector3.Cross(dirToCaster, forwardVector).Y < 0) ? 1.0f : -1.0f;
            var signedRotationIncrement = rotationDirection * rotationIncrementRad;

            // --- 3. 预先计算所有AOE的位置 ---
            var aoePositions = new List<Vector3>();
            for (int i = 0; i < maxCasts; i++)
            {
                var rotationAngle = signedRotationIncrement * i;
                var rotatedDir = new Vector3(
                    dirToCaster.X * MathF.Cos(rotationAngle) - dirToCaster.Z * MathF.Sin(rotationAngle),
                    dirToCaster.Y,
                    dirToCaster.X * MathF.Sin(rotationAngle) + dirToCaster.Z * MathF.Cos(rotationAngle)
                );
                aoePositions.Add(SharkArenaCenter + rotatedDir);
            }

            // --- 4. 一次性发送所有绘图指令 ---
            for (int i = 0; i < aoePositions.Count; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"OpenWater_Refactored_AOE_{i}";
                dp.Position = aoePositions[i];
                dp.Scale = new Vector2(aoeRadius);

                // 第一个圈使用更醒目的颜色
                dp.Color = (i == 0)
                    ? new Vector4(1, 0, 0, 0.8f) // 红色
                    : accessory.Data.DefaultDangerColor; // 普通警告色

                // 计算每个AOE的出现和消失时间
                int explosionTime = 5000 + (i * intervalMs);
                int appearanceTime = explosionTime - 5000;

                dp.Delay = (appearanceTime > 0) ? appearanceTime : 0; // 预警圈出现时间
                dp.DestoryAt = 5000; // 预警圈总持续时间
                dp.ScaleMode |= ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }

            accessory.Log.Debug($"旋转月环(重构)已启动。模式: {(isInnerMode ? "内圈" : "外圈")}, 数量: {maxCasts}, 方向: {(rotationDirection > 0 ? "顺时针" : "逆时针")}");
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
            name: "圣焰(城塞守卫)",
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
            name: "风石光 - 记录能量球(城塞守卫)",
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
            name: "风石光 - 能量球属性分类(城塞守卫)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:2536"],
            userControl: false
        )]
        public void OnSphereStatusGain(Event @event, ScriptAccessory accessory)
        {
            var sphere = accessory.Data.Objects.SearchById(@event.TargetId);
            if (sphere == null) return;

            // status.Extra (Param) 用于区分风(0x224)或石(0x225)
            var statusParam = @event["Param"];

            lock (_surgeLock)
            {
                if (!_spheres.Contains(sphere)) return; // 在锁内检查

                if (statusParam == "548") 
                {
                    _spheresWind.Add(sphere);
                    _spheres.Remove(sphere);
                    accessory.Log.Debug($"能量球 {sphere.EntityId} 被分类为 [风]");
                }
                else if (statusParam == "549") 
                {
                    _spheresStone.Add(sphere);
                    _spheres.Remove(sphere);
                    accessory.Log.Debug($"能量球 {sphere.EntityId} 被分类为 [石]");
                }
            }
        }

        [ScriptMethod(
            name: "风石光 - 预测并绘制AOE(城塞守卫)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41287|41292|41289|41293)$"]
        )]
        public void OnAncientSpellCast(Event @event, ScriptAccessory accessory)
        {
            var caster = accessory.Data.Objects.SearchById(@event.SourceId);
            if (caster == null) return;

            // 根据ActionId判断技能类型(风/石)和咏唱时间
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
                        dp.Delay = castTimeMs - 5000;
                        dp.DestoryAt = 7500;
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
            name: "风石光 - 标记神圣咏唱开始(城塞守卫)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41284"],
            userControl: false
        )]
        public void OnAncientHolyCast(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug($"检测到BOSS开始咏唱神圣 ({@event.ActionId})，延迟光球判定。");
            _isHolyCasting = true;
        }
        [ScriptMethod(
            name: "风石光 - 神圣咏唱完成并引爆光球(城塞守卫)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:41284"]
        )]
        public void OnAncientHolyFinish(Event @event, ScriptAccessory accessory)
        {
            if (!_isHolyCasting) return;

            accessory.Log.Debug("神圣咏唱完成，开始处理剩余的光球。");

            List<IGameObject> lightSpheres;
            lock (_surgeLock)
            {
                if (_spheres.Count == 0)
                {
                    _isHolyCasting = false; // 重置标记
                    return;
                }
                lightSpheres = new List<IGameObject>(_spheres);
            }

            // 爆炸发生在咏唱完成后约2.4秒
            const int explosionDelay = 2400;

            foreach (var sphere in lightSpheres)
            {
                accessory.Log.Debug($"光球 {sphere.EntityId} 将在 {explosionDelay}ms 后爆炸。");

                var dp = accessory.Data.GetDefaultDrawProperties();
                var drawName = $"Surge_AOE_Light_{sphere.EntityId}";

                dp.Name = drawName;
                dp.Position = sphere.Position;
                dp.Scale = new Vector2(15);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.DestoryAt = 2400;
                dp.ScaleMode |= ScaleMode.ByTime;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                lock (_surgeLock)
                {
                    _surgeAoes.Add((sphere.EntityId, drawName));
                }
            }

            lock (_surgeLock)
            {
                _spheres.Clear();
            }

            _isHolyCasting = false; // 重置标记
        }


        [ScriptMethod(
            name: "风石光 - 清理已爆炸的AOE(城塞守卫)",
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
                _isHolyCasting = false;
                _surgeAoes.Clear();
                _spheres.Clear();
                _spheresStone.Clear();
                _spheresWind.Clear();
            }
        }
        #endregion
        #region 夺心魔
        [ScriptMethod(
            name: "昏暗(夺心魔)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41170"]
        )]
        public void OnDarkIIDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "夺心魔_昏暗_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(65);
            dp.Radian = 90 * MathF.PI / 180.0f;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 6000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        [ScriptMethod(
            name: "触手(夺心魔)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:regex:^(41257|41314|41256)$"]
        )]
        public void 触手Draw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "夺心魔_触手_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.ScaleMode |= ScaleMode.ByTime;
            switch (@event.ActionId)
            {
                case 41257:
                    dp.Scale = new Vector2(20, 60);
                    dp.DestoryAt = 11000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
                case 41314:
                    dp.Scale = new Vector2(10, 60);
                    dp.DestoryAt = 7000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
                case 41256:
                    dp.Scale = new Vector2(10, 60);
                    dp.DestoryAt = 11000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                    break;
            }
        }


        private const uint SID_PlayingWithFire = 4211;
        private const uint SID_PlayingWithIce = 4212;
        private const uint SID_ImpElement = 2193;
        private const uint OID_JestingJackanapes = 18102;
        private const uint AID_SurpriseAttack = 41254;


        [ScriptMethod(
            name: "火冰陷阱 - 记录玩家元素(夺心魔)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:regex:^(4211|4212)$"],
            userControl: false
        )]
        public void OnPlayerElementGain(Event @event, ScriptAccessory accessory)
        {
            var isFire = @event.StatusId == SID_PlayingWithFire;
            lock (_trapLock)
            {
                _playerElements[@event.TargetId] = isFire;
                // 【新增日志】确认玩家状态被成功记录
                if (@event.TargetId == accessory.Data.Me)
                {
                    accessory.Log.Debug($"[状态记录] 成功记录你的元素状态为: {(isFire ? "火" : "冰")}");
                }
            }

            if (@event.TargetId == accessory.Data.Me)
            {
                DrawFireIceTraps(accessory);
            }
        }

        [ScriptMethod(
            name: "火冰陷阱 - 移除玩家元素(夺心魔)",
            eventType: EventTypeEnum.StatusRemove,
            eventCondition: ["StatusID:regex:^(4211|4212)$"],
            userControl: false
        )]
        public void OnPlayerElementLose(Event @event, ScriptAccessory accessory)
        {
            bool needsRedraw = false;
            lock (_trapLock)
            {
                if (_playerElements.Remove(@event.TargetId))
                {
                    needsRedraw = @event.TargetId == accessory.Data.Me;
                }
            }

            if (needsRedraw)
            {
                accessory.Log.Debug("你的元素已消失，更新陷阱提示。");
                DrawFireIceTraps(accessory);
            }
        }
        [ScriptMethod(
            name: "火冰陷阱 - 记录陷阱(夺心魔)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:2193"]
        )]
        public void OnTrapCreated(Event @event, ScriptAccessory accessory)
        {
            var npc = accessory.Data.Objects.SearchById(@event.TargetId);
            if (npc == null || npc.DataId != OID_JestingJackanapes) return;
            accessory.Log.Debug($"陷阱NPC {@event.TargetId} 获得元素状态, 收到的Param值为: '{@event["Param"]}'");
            // 根据status.Extra(Param)判断元素，0x344是火，否则是冰
            var isFire = @event["Param"] == "836";

            var trap = new FireIceTrapInfo
            {
                NpcId = npc.EntityId,
                Position = npc.Position,
                IsFire = isFire
            };

            lock (_trapLock)
            {
                _fireIceTraps.Add(trap);
            }
            accessory.Log.Debug($"发现一个新的{(isFire ? "火" : "冰")}陷阱，位于 {trap.Position}");
            DrawFireIceTraps(accessory);
        }
        [ScriptMethod(
            name: "火冰陷阱 - 更新陷阱位置(夺心魔)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41254"]
        )]
        public void OnTrapMove(Event @event, ScriptAccessory accessory)
        {
            // 在访问共享列表前加锁，保证线程安全
            lock (_trapLock)
            {
                // 获取“移动”技能咏唱者的位置
                var casterPosition = @event.SourcePosition;

                // 通过位置来找到要移动的陷阱。我们检查哪个陷阱的位置和咏唱者位置非常接近。
                int trapIndex = _fireIceTraps.FindIndex(t => Vector3.Distance(t.Position, casterPosition) < 1.0f);

                if (trapIndex != -1)
                {
                    // 获取旧陷阱的数据
                    var oldTrap = _fireIceTraps[trapIndex];
                    var newPosition = @event.EffectPosition; // 陷阱的新位置是技能的目标点
                    accessory.Log.Debug($"陷阱从 {oldTrap.Position} 移动到 {newPosition} (通过位置匹配)");

                    // 创建一个包含新位置的全新陷阱对象
                    var updatedTrap = new FireIceTrapInfo
                    {
                        NpcId = oldTrap.NpcId,
                        Position = newPosition, // 使用新的位置
                        IsFire = oldTrap.IsFire
                    };

                    // 用新对象替换列表中的旧对象，这是最安全的更新方式
                    _fireIceTraps[trapIndex] = updatedTrap;
                }
                else
                {
                    accessory.Log.Debug($"在位置 {casterPosition} 附近未能匹配到任何已知陷阱。");
                }
            }
            // 修改完成后再重绘
            DrawFireIceTraps(accessory);
        }
        [ScriptMethod(
            name: "火冰陷阱 - 机制结束清理(夺心魔)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(41250|41251)$"],
            userControl: false
        )]
        public void OnTrapExplosion(Event @event, ScriptAccessory accessory)
        {
            accessory.Log.Debug("陷阱已爆炸，清理所有绘图和状态。");
            lock (_trapLock)
            {
                _fireIceTraps.Clear();
                _playerElements.Clear();
            }
            accessory.Method.RemoveDraw("FireIceTrap_.*");
        }

        private async void DrawFireIceTraps(ScriptAccessory accessory)
        {
            // 【关键修正】在重绘前，先移除所有旧的同名绘图
            accessory.Method.RemoveDraw("FireIceTrap_.*");

            // 【关键修正】加入一个短暂的延迟，确保移除指令被处理完毕
            await Task.Delay(50); // 延迟50毫秒
            List<FireIceTrapInfo> trapsCopy;
            Dictionary<ulong, bool> playerElementsCopy;
            lock (_trapLock)
            {
                trapsCopy = new List<FireIceTrapInfo>(_fireIceTraps);
                playerElementsCopy = new Dictionary<ulong, bool>(_playerElements);
            }
            // 2. 获取当前玩家的元素状态
            _playerElements.TryGetValue(accessory.Data.Me, out var playerIsFire);
            bool playerHasElement = _playerElements.ContainsKey(accessory.Data.Me);
            // 【新增日志】在绘图前，先确认脚本认为你是什么状态
            if (playerHasElement)
            {
                accessory.Log.Debug($"[绘图前检查] 脚本认为你的状态是: {(playerIsFire ? "火" : "冰")}");
            }
            else
            {
                accessory.Log.Debug("[绘图前检查] 脚本认为你没有元素状态。");
            }
            // 3. 根据场上陷阱的数量，动态决定警告的持续时间
            int trapWarningDurationMs = _fireIceTraps.Count > 2 ? 20000 : 10000; // 大于2只小丑为20秒，否则为10秒
            if (trapsCopy.Any())
            {
                accessory.Log.Debug($"当前陷阱数量: {trapsCopy.Count}, 警告持续时间设置为: {trapWarningDurationMs}ms");
            }



            // 4. 遍历所有已知的陷阱并进行绘制
            foreach (var trap in trapsCopy)
            {
                // 如果玩家没有元素debuff，所有陷阱都显示为小圈(基础提示)
                if (!playerHasElement)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"FireIceTrap_Small_{trap.NpcId}";
                    dp.Position = trap.Position;
                    dp.Scale = new Vector2(8); // 小圈半径8
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.DestoryAt = trapWarningDurationMs; // 使用动态计算的时长
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    continue; // 继续处理下一个陷阱
                }

                // 如果玩家有元素debuff，则进行特殊绘制
                // 判断当前陷阱的元素是否与玩家相同
                bool isSameElement = (playerIsFire == trap.IsFire);
                accessory.Log.Debug($"[循环判断] 陷阱 {trap.NpcId}: 玩家是火({playerIsFire}), 陷阱是火({trap.IsFire}). --> 元素是否相同: {isSameElement}");
                if (isSameElement)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"FireIceTrap_Big_Danger_{trap.NpcId}";
                    dp.Position = trap.Position;
                    dp.Scale = new Vector2(38); // 大圈外半径38
                    dp.Color = new Vector4(1.0f, 0.2f, 0.2f, 0.6f);
                    dp.DestoryAt = trapWarningDurationMs;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
                else
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"FireIceTrap_Small_Safe_{trap.NpcId}";
                    dp.Position = trap.Position;
                    dp.Scale = new Vector2(8);
                    dp.Color = new Vector4(1.0f, 0.2f, 0.2f, 0.6f); 
                    dp.DestoryAt = trapWarningDurationMs;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
            }
        }

/*
        /// <summary>
        /// 【调试模式】核心绘制函数：无论如何都在陷阱位置绘制一个黄色圆圈
        /// </summary>
        private void DrawFireIceTraps(ScriptAccessory accessory)
        {
            // 1. 清除所有旧的绘图
            accessory.Method.RemoveDraw("FireIceTrap_.*");

            // 2. 在锁的保护下，创建陷阱列表的本地副本
            List<FireIceTrapInfo> trapsCopy;
            lock (_trapLock)
            {
                trapsCopy = new List<FireIceTrapInfo>(_fireIceTraps);
            }

            accessory.Log.Debug($"[调试模式] 准备为 {trapsCopy.Count} 个陷阱绘制测试圆圈。");

            // 3. 遍历副本，为每个陷阱绘制一个醒目的黄色圆圈
            foreach (var trap in trapsCopy)
            {
                accessory.Log.Debug($"[调试模式] 正在为陷阱 {trap.NpcId} 在位置 {trap.Position} 绘制测试圆圈。");
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"FireIceTrap_Test_Circle_{trap.NpcId}";
                dp.Position = trap.Position;
                dp.Scale = new Vector2(10); // 一个容易看到的尺寸
                dp.Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f); // 亮黄色
                dp.DestoryAt = 15000; // 显示15秒
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }
        */
        #endregion
        #region 跃立狮OnTheHuntAreaCenter

        [ScriptMethod(
            name: "裂魄惊芒爪(跃立狮)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41411"]
        )]
        public void 恐怖闪光Draw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "跃立狮_恐怖闪光_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(60);
            dp.Radian = 90 * MathF.PI / 180.0f;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 6000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
            accessory.Log.Debug("绘制跃立狮的恐怖闪光AOE");
        }
        [ScriptMethod(
            name: "压缩爆炸(跃立狮)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41407"]
        )]
        public void OnDecompressDraw(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "跃立狮_Decompress_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(12);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            accessory.Log.Debug("绘制跃立狮的Decompress AOE");
        }
        [ScriptMethod(
            name: "以太射线 (跃立狮)",
            eventType: EventTypeEnum.Tether,
            eventCondition: ["Id:0138"]
        )]
        public void OnAetherialRayTether(Event @event, ScriptAccessory accessory)
        {

            var target = accessory.Data.Objects.SearchById(@event.TargetId);
            if (target == null)
            {
                accessory.Log.Error($"找不到连线目标: {@event.TargetId}");
                return;
            }

            // 计算从中心指向目标的向量
            var directionVector = target.Position - OnTheHuntAreaCenter;

            // 将向量转换为角度，并加上固定的200度旋转
            var angle = MathF.Atan2(directionVector.X, directionVector.Z);
            var finalRotation = angle + (200 * MathF.PI / 180.0f);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"CrystalDragon_AetherialRay_{target.EntityId}";
            dp.Position = OnTheHuntAreaCenter; // AOE在场地中心
            dp.Scale = new Vector2(10, 28); // 宽度10
            dp.Rotation = finalRotation;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5200;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            accessory.Log.Debug($"绘制以太射线, 目标: {target.Name}, 旋转角度: {finalRotation}");
        }
        [ScriptMethod(
            name: "明亮脉冲 (跃立狮)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:2193"]
        )]
        public void OnBrightPulseStatus(Event @event, ScriptAccessory accessory)
        {
            var roundel = accessory.Data.Objects.SearchById(@event.TargetId);
            if (roundel == null || roundel.DataId != 18142) return;


            // 1. 计算从中心到光球的方向向量
            var dir = roundel.Position - OnTheHuntAreaCenter;

            // 2. 判断光球在内圈还是外圈，决定基础旋转角度
            // 半径15的平方是225
            var angleDegrees = dir.LengthSquared() < 225f ? 280f : 150f;

            // 3. 判断光球的朝向，决定旋转是顺时针还是逆时针
            // 获取光球的正前方向量
            var forwardVector = new Vector3(MathF.Sin(roundel.Rotation), 0, MathF.Cos(roundel.Rotation));
            // 计算方向向量的垂直向量 (OrthoR)
            var orthoR = new Vector3(dir.Z, 0, -dir.X);
            // 点积判断方向
            if (Vector3.Dot(orthoR, forwardVector) > 0f)
            {
                angleDegrees = -angleDegrees; // 反向旋转
            }

            // 4. 计算最终AOE位置
            var angleRadians = angleDegrees * MathF.PI / 180.0f;
            var rotatedDir = new Vector3(
                dir.X * MathF.Cos(angleRadians) - dir.Z * MathF.Sin(angleRadians),
                dir.Y,
                dir.X * MathF.Sin(angleRadians) + dir.Z * MathF.Cos(angleRadians)
            );
            var aoePosition = OnTheHuntAreaCenter + rotatedDir;

            // 5. 绘制AOE
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"CrystalDragon_BrightPulse_{roundel.EntityId}";
            dp.Position = aoePosition;
            dp.Scale = new Vector2(13); // 半径13
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5200; // 5.2秒后消失
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            accessory.Log.Debug($"绘制明亮脉冲, 光球ID: {roundel.EntityId}, 最终位置: {aoePosition}");
        }

        [ScriptMethod(
            name: "绘图移除 (跃立狮)",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(41402|41403)$"],
            userControl: false
        )]
        public void RemoveCrystalDragonAoes(Event @event, ScriptAccessory accessory)
        {
            if (@event.ActionId == 41402) // AetherialRay
            {
                accessory.Method.RemoveDraw("CrystalDragon_AetherialRay_.*");
                accessory.Log.Debug("清理以太射线绘图。");
            }
            else if (@event.ActionId == 41403) // BrightPulse
            {
                accessory.Method.RemoveDraw($"CrystalDragon_BrightPulse_{@event.SourceId}");
                accessory.Log.Debug($"清理明亮脉冲绘图: {@event.SourceId}");
            }
        }
        #endregion

        #region 金钱龟
        [ScriptMethod(
            name: "古币爆风(金钱龟)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:41522"]
        )]
        public void OnCostOfLivingKB(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "CostOfLivingKBDraw";
            dp.Owner = accessory.Data.Me;
            dp.Scale = new Vector2(1.5f, 30f);
            dp.Rotation = 180f * MathF.PI / 180f;
            dp.Color = new(0.3f, 1.0f, 0f, 1.5f);
            dp.TargetPosition = @event.SourcePosition;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
            accessory.Log.Debug($"绘制击退: Name={dp.Name}s");
        }
        [ScriptMethod(
            name: "强制移动(金钱龟)",
            eventType: EventTypeEnum.StatusAdd,
            eventCondition: ["StatusID:4344"]
        )]
        public void OnForcedMovement(Event @event, ScriptAccessory accessory)
        {
            if (@event.TargetId != accessory.Data.Me)
            {
                accessory.Log.Debug("强制移动事件不是针对玩家，忽略。");
                return;
            }
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "ForcedMovementDraw";
            dp.Owner = accessory.Data.Me;
            dp.Scale = new Vector2(1.5f, 35f);
            dp.Color = new(0.3f, 1.0f, 0f, 1.5f);
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
            accessory.Log.Debug($"绘制强制移动: Name={dp.Name}");
        }
        #endregion
    }
}