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
    }
}
