using System;
using System.Globalization;
using System.Numerics;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Script;
using Newtonsoft.Json;

namespace XSZYYS.Script.新月岛
{
    [ScriptType(
        name: "新月岛北伐",
        territorys: [1346],
        guid: "7E2B9A14-5C8F-4D6A-B3E1-9F0A2C4D5E6F",
        version: "0.0.2",
        author: "XSZYYS",
        note: "新月岛北伐CE：雪石膏之剑读条预警"
    )]
    public class 新月岛北伐
    {
        // ==================== 雪石膏之剑 ====================
        [ScriptMethod(name: "雪石膏之剑 (扇形预警)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47158"])]
        public void 雪石膏之剑_扇形预警(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "雪石膏之剑_扇形";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(40);
            dp.Radian = MathF.PI / 2;          // 90°
            dp.Rotation = @event.SourceRotation; // 以该实体读条时面向为基准
            dp.FixRotation = true;             // 读条期间锁定朝向
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 3000;
            dp.ScaleMode |= ScaleMode.ByTime;  // 随时间填充的预警效果

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(name: "雪石膏之剑_左右刀 (正反扇形预警)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(47166|47167)$"])]
        public void 雪石膏之剑_左右刀(Event @event, ScriptAccessory accessory)
        {
            // 第一刀：读条面向方向，180° 扇形，持续 5000ms
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "雪石膏之剑_左右刀_正向";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(40);
            dp.Radian = MathF.PI;              // 180°
            dp.Rotation = @event.SourceRotation; // 以读条时面向为基准
            dp.FixRotation = true;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            // 第二刀：5000ms 后在反方向绘制，180° 扇形，持续 2000ms
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "雪石膏之剑_左右刀_反向";
            dp2.Owner = @event.SourceId;
            dp2.Scale = new Vector2(40);
            dp2.Radian = MathF.PI;             // 180°
            dp2.Rotation = @event.SourceRotation + MathF.PI; // 反方向
            dp2.FixRotation = true;
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.Delay = 5000;                  // 延迟 5000ms 出现
            dp2.DestoryAt = 2000;              // 出现后持续 2000ms
            dp2.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp2);
        }

        // ==================== 新月女王 ====================
        [ScriptMethod(name: "新月女王_王权闪光 (扇形预警)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(49494|49495)$"])]
        public void 新月女王_王权闪光(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "新月女王_王权闪光_扇形";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(40);
            dp.Radian = MathF.PI;              // 180°
            dp.Rotation = @event.SourceRotation; // 以读条时面向为基准
            dp.FixRotation = true;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        // ==================== 妖艳魔花珊迪 ====================
        [ScriptMethod(name: "妖艳魔花珊迪_十字 (十字AOE预警)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(48945|48946|48947)$"])]
        public void 妖艳魔花珊迪_十字(Event @event, ScriptAccessory accessory)
        {
            // 十字 = 两条交叉的直线：水平 + 垂直（旋转90°），整体相对实体面向偏移45°
            var baseRotation = @event.SourceRotation + MathF.PI / 4; // 45°偏移

            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = "妖艳魔花珊迪_十字_1";
            dp1.Owner = @event.SourceId;
            dp1.Scale = new Vector2(6, 30);     // 宽度6, 长度30
            dp1.Rotation = baseRotation;
            dp1.FixRotation = true;
            dp1.Color = accessory.Data.DefaultDangerColor;
            dp1.DestoryAt = 5000;
            dp1.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);

            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "妖艳魔花珊迪_十字_2";
            dp2.Owner = @event.SourceId;
            dp2.Scale = new Vector2(6, 30);
            dp2.Rotation = baseRotation + MathF.PI / 2; // 垂直方向
            dp2.FixRotation = true;
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.DestoryAt = 5000;
            dp2.ScaleMode |= ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
        }

        // ==================== 毒气团 ====================
        [ScriptMethod(name: "毒气团 (圆形预警)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:48950"])]
        public void 毒气团_圆形预警(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "毒气团_圆形";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(10);        // 半径 10
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 5000;
            dp.ScaleMode |= ScaleMode.ByTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }
}
