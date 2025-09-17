using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Threading;
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
using KodakkuAssist.Module.Draw.Manager;

namespace KodakkuDebugScript
{
    [ScriptType(
        name: "调试脚本 (By Chat Command)",
        guid: "B866F3EA-35A3-4BAA-8259-17A0D7608928",
        territorys: [],
        version: "0.0.2",
        author: "XSZYYS",
        note: "在默语频道输入命令来动态绘图。\n用法:\n[目标] [形状] [参数...]\n\n目标 (可选, 默认为自己):\n- test ... (在自己身上绘制)\n- test eid=[实体ID] ... (在指定实体上绘制)\n\n形状和参数:\n- circle [半径]\n- fan [半径] [角度]\n- rect [宽度] [长度]\n- donut [外径] [内径]\n- straight [宽度] [长度]\n\n示例:\n- test circle 5\n- test eid=4000123A fan 10 90"
    )]
    public class GeneralDebugScript
    {

        private enum DrawOriginType { Object, Position }
        private class DrawOrigin
        {
            public DrawOriginType Type { get; set; }
            public ulong OwnerId { get; set; }
            public Vector3 Position { get; set; }
            public string SourceName { get; set; }
        }

        public void Init(ScriptAccessory accessory)
        {
            accessory.Log.Debug("通用调试脚本已加载。");
        }
        [ScriptMethod(
            name: "测试自身", 
            eventType: EventTypeEnum.Chat, 
            eventCondition: new string[] { "Type:Echo" }
        )]
        public async void TestSelf(Event @event, ScriptAccessory accessory)
        {
            string message = @event["Message"];
            if (message.Trim().Equals("测试自身", StringComparison.OrdinalIgnoreCase))
            {
                accessory.Method.SendChat("/e --- 周围玩家列表 ---");
                foreach (var obj in accessory.Data.Objects)
                {
                    if (obj is IPlayerCharacter player)
                    {
                        string playerName = player.Name.ToString();
                        var playerJob = player.ClassJob.Value.Name;
                        accessory.Method.SendChat($"/e 玩家: {playerName}, 职业: {playerJob}");
                        await Task.Delay(10);
                    }
                }
                accessory.Method.SendChat("/e --- 列表结束 ---");
            }
        }
        [ScriptMethod(
            name: "检测职业属性",
            eventType: EventTypeEnum.Chat,
            eventCondition: ["Type:Echo"]
        )]
        public void DebugClassJobProperties(Event @event, ScriptAccessory accessory)
        {
            if (@event["Message"] != "检测职业") return;

            var player = accessory.Data.MyObject;
            if (player?.ClassJob.Value == null)
            {
                accessory.Method.SendChat("/e 无法获取当前玩家的职业信息。");
                return;
            }

            var classJob = player.ClassJob.Value;
            string report = $"--- 职业信息 ---\n" +
                            $"Name: {classJob.Name}\n" +
                            $"Abbreviation: {classJob.Abbreviation}\n" +
                            $"ID: {classJob.JobIndex}\n" +
                            $"Role: {classJob.Role}\n" +
                            $"IsTank: {IsTank(player)}\n" +
                            $"IsHealer: {IsHealer(player)}\n" +
                            $"IsDps: {IsDps(player)}";
            
            accessory.Method.SendChat($"/e {report}");
        }
        [ScriptMethod(
            name: "聊天指令调试绘图",
            eventType: EventTypeEnum.Chat,
            eventCondition: new string[] { "Type:Echo" }
        )]
        public void OnEchoChat(Event @event, ScriptAccessory accessory)
        {
            string message = @event["Message"].ToLower();
            string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2 || parts[0] != "test") return;

            DrawOrigin origin = null;
            int commandStartIndex = 1;


            if (parts[1].StartsWith("eid="))
            {
                string eidString = parts[1].Substring(4); // 移除 "eid="
                if (!ulong.TryParse(eidString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong eid))
                {
                    accessory.Method.SendChat($"/e [调试脚本] 错误: 无效的实体ID格式: {eidString}");
                    return;
                }

                var target = accessory.Data.Objects.SearchById(eid);
                if (target == null)
                {
                    accessory.Method.SendChat($"/e [调试脚本] 错误: 未找到实体ID为 {eidString} 的对象。");
                    return;
                }
                origin = new DrawOrigin { Type = DrawOriginType.Object, OwnerId = target.EntityId, SourceName = $"实体({eidString.ToUpper()})" };
                commandStartIndex = 2;
            }
            else
            {
                origin = new DrawOrigin { Type = DrawOriginType.Object, OwnerId = accessory.Data.Me, SourceName = "您" };
            }

            if (parts.Length <= commandStartIndex) return;

            string shapeCommand = parts[commandStartIndex];
            string[] shapeParts = parts.Skip(commandStartIndex).ToArray();

            try
            {
                switch (shapeCommand)
                {
                    case "circle": HandleCircleCommand(shapeParts, origin, accessory); break;
                    case "fan": HandleFanCommand(shapeParts, origin, accessory); break;
                    case "rect": HandleRectCommand(shapeParts, origin, accessory); break;
                    case "donut": HandleDonutCommand(shapeParts, origin, accessory); break;
                    case "straight": HandleStraightCommand(shapeParts, origin, accessory); break;
                }
            }
            catch (Exception ex)
            {
                accessory.Log.Error($"处理调试指令时出错: {ex.Message}");
                accessory.Method.SendChat($"/e [调试脚本] 指令错误: {ex.Message}");
            }
        }




        private void ApplyOrigin(DrawPropertiesEdit dp, DrawOrigin origin)
        {
            dp.Owner = origin.OwnerId;
        }

        private void HandleCircleCommand(string[] parts, DrawOrigin origin, ScriptAccessory accessory)
        {
            if (parts.Length != 2) throw new ArgumentException("Circle指令格式错误。正确格式: circle [半径]");
            if (!float.TryParse(parts[1], out float radius)) throw new ArgumentException($"无效的半径值: {parts[1]}");

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"debug_circle_{Guid.NewGuid()}";
            ApplyOrigin(dp, origin);
            dp.Scale = new Vector2(radius);
            dp.Color = new Vector4(0.1f, 0.8f, 0.8f, 1.0f); // 青色
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime; // 添加时间填充效果
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
            accessory.Method.SendChat($"/e [调试脚本] 已在 {origin.SourceName} 身上绘制半径为 {radius} 的圆形。");
        }

        private void HandleFanCommand(string[] parts, DrawOrigin origin, ScriptAccessory accessory)
        {
            if (parts.Length != 3) throw new ArgumentException("Fan指令格式错误。正确格式: fan [半径] [角度]");
            if (!float.TryParse(parts[1], out float radius)) throw new ArgumentException($"无效的半径值: {parts[1]}");
            if (!float.TryParse(parts[2], out float angleDegrees)) throw new ArgumentException($"无效的角度值: {parts[2]}");

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"debug_fan_{Guid.NewGuid()}";
            ApplyOrigin(dp, origin);
            dp.Scale = new Vector2(radius);
            dp.Radian = angleDegrees * MathF.PI / 180.0f;
            dp.Color = new Vector4(0.8f, 0.1f, 0.8f, 1.0f); // 紫色
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime; // 添加时间填充效果
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
            accessory.Method.SendChat($"/e [调试脚本] 已在 {origin.SourceName} 身上绘制半径为 {radius}，角度为 {angleDegrees}° 的扇形。");
        }

        private void HandleRectCommand(string[] parts, DrawOrigin origin, ScriptAccessory accessory)
        {
            if (parts.Length != 3) throw new ArgumentException("Rect指令格式错误。正确格式: rect [宽度] [长度]");
            if (!float.TryParse(parts[1], out float width) || !float.TryParse(parts[2], out float length)) throw new ArgumentException("无效的宽度或长度值。");

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"debug_rect_{Guid.NewGuid()}";
            ApplyOrigin(dp, origin);
            dp.Scale = new Vector2(width, length);
            dp.Color = new Vector4(0.8f, 0.4f, 0.1f, 1.0f); // 橙色
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime; // 添加时间填充效果
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp);
            accessory.Method.SendChat($"/e [调试脚本] 已在 {origin.SourceName} 身上绘制宽度为 {width}，长度为 {length} 的矩形。");
        }

        private void HandleDonutCommand(string[] parts, DrawOrigin origin, ScriptAccessory accessory)
        {
            if (parts.Length != 3) throw new ArgumentException("Donut指令格式错误。正确格式: donut [外径] [内径]");
            if (!float.TryParse(parts[1], out float outerRadius) || !float.TryParse(parts[2], out float innerRadius)) throw new ArgumentException("无效的外径或内径值。");

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"debug_donut_{Guid.NewGuid()}";
            ApplyOrigin(dp, origin);
            dp.Scale = new Vector2(outerRadius);
            dp.InnerScale = new Vector2(innerRadius);
            dp.Radian = MathF.PI * 2;
            dp.Color = new Vector4(0.1f, 0.8f, 0.4f, 1.0f); // 绿色
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime; // 添加时间填充效果
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp);
            accessory.Method.SendChat($"/e [调试脚本] 已在 {origin.SourceName} 身上绘制外径为 {outerRadius}，内径为 {innerRadius} 的圆环。");
        }

        private void HandleStraightCommand(string[] parts, DrawOrigin origin, ScriptAccessory accessory)
        {
            if (parts.Length != 3) throw new ArgumentException("Straight指令格式错误。正确格式: straight [宽度] [长度]");
            if (!float.TryParse(parts[1], out float width) || !float.TryParse(parts[2], out float length)) throw new ArgumentException("无效的宽度或长度值。");

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"debug_straight_{Guid.NewGuid()}";
            ApplyOrigin(dp, origin);
            dp.Scale = new Vector2(width, length);
            dp.Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f); // 白色
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime; // 添加时间填充效果
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Straight, dp);
            accessory.Method.SendChat($"/e [调试脚本] 已在 {origin.SourceName} 身上绘制宽度为 {width}，长度为 {length} 的直线。");
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


}
