using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.Draw;
using System.Numerics;
using Newtonsoft.Json;
using KodakkuAssist.Module.Draw.Manager;

namespace A8S_Scripts
{
    [ScriptType(name: "亚历山大零式机神城 律动之章4",
                territorys: [532],
                guid: "2313EAA7-0C90-4802-AA48-C9CFF30BFBEA",
                version: "1.0.9",
                author: "XSZYYS")]
    public class A8S
    {

        public void Init(ScriptAccessory accessory)
        {
            accessory.Method.RemoveDraw(".*");
        }


        [ScriptMethod(name: "巨型激光炮",
                      eventType: EventTypeEnum.StartCasting,
                      eventCondition: ["ActionId:regex:^(5678|5732)$"])]
        public void MegaBeam(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["DurationMilliseconds"], out var castTime))
            {
                castTime = 5000;
            }

            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "A8S_MegaBeam_Danger_Zone";         // Unique name for the drawing
            dp.Owner = @event.SourceId;                    // Anchor the drawing to the caster
            dp.Scale = new Vector2(10, 50);                // Set the rectangle's size: 10m width, 50m length
            dp.Color = accessory.Data.DefaultDangerColor;  // Use the default danger color
            dp.DestoryAt = castTime;                       // The drawing will disappear when the cast finishes

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }


        [ScriptMethod(name: "双重火箭飞拳",
                      eventType: EventTypeEnum.StartCasting,
                      eventCondition: ["ActionId:5731"])]
        public void DoubleRocketPunch(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["DurationMilliseconds"], out var castTime))
            {
                castTime = 5000;
            }

            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "A8S_DoubleRocketPunch_Danger_Zone"; // Unique name for the drawing
            dp.Owner = @event.TargetId;                     // Anchor the drawing to the target of the ability
            dp.Scale = new Vector2(5, 5);                   // Set the circle's radius to 5m
            dp.Color = accessory.Data.DefaultDangerColor;   // Use the default danger color
            dp.DestoryAt = castTime;                        // The drawing will disappear when the cast finishes

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }


        [ScriptMethod(name: "超级跳跃",
                      eventType: EventTypeEnum.StartCasting,
                      eventCondition: ["ActionId:5733"])]
        public void SuperJump(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["DurationMilliseconds"], out var castTime))
            {
                castTime = 5000;
            }

            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "A8S_SuperJump_Danger_Zone";          // Unique name for the drawing
            dp.Owner = @event.SourceId;                      // The drawing's position is relative to the caster
            dp.Scale = new Vector2(5, 5);                    // Set the circle's radius to 5m
            dp.Color = accessory.Data.DefaultDangerColor;    // Use the default danger color
            dp.DestoryAt = castTime;                         // The drawing will disappear when the cast finishes

            dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }


        [ScriptMethod(name: "末世宣言",
                      eventType: EventTypeEnum.ActionEffect,
                      eventCondition: ["ActionId:5734"])]
        public void ApocalypticRay(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "A8S_ApocalypticRay_Danger_Zone";    // Unique name for the drawing
            dp.Owner = @event.SourceId;                     // Anchor the drawing to the caster
            dp.Scale = new Vector2(25, 25);                 // Set the fan's radius to 25m
            dp.Radian = MathF.PI / 2;                       // Set the angle to 90 degrees (PI/2 radians)
            dp.Color = accessory.Data.DefaultDangerColor;   // Use the default danger color
            dp.DestoryAt = 5000;                            // The drawing will last for 5000ms (5 seconds)

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }


        [ScriptMethod(name: "激光战轮",
                      eventType: EventTypeEnum.StartCasting,
                      eventCondition: ["ActionId:5716"])]
        public void LaserChakram(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["DurationMilliseconds"], out var castTime))
            {
                castTime = 5000;
            }

            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "A8S_LaserChakram_Danger_Zone";    // Unique name for the drawing
            dp.Owner = @event.SourceId;                   // Anchor the drawing to the caster
            dp.Scale = new Vector2(6, 70);                // Set the rectangle's size: 6m width, 70m length
            dp.Color = accessory.Data.DefaultDangerColor; // Use the default danger color
            dp.DestoryAt = castTime;                      // The drawing will disappear when the cast finishes

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }


        [ScriptMethod(name: "幻影系统",
                      eventType: EventTypeEnum.TargetIcon,
                      eventCondition: ["Id:0008"])]
        public void PhantomSystem(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "A8S_PhantomSystem_Danger_Zone";    // Unique name for the drawing
            dp.Owner = @event.TargetId;                   // Anchor the drawing to the targeted player
            dp.Scale = new Vector2(5, 5);                 // Set the circle's radius to 5m
            dp.Color = accessory.Data.DefaultDangerColor; // Use the default danger color
            dp.DestoryAt = 5000;                          // The drawing will last for 5000ms (5 seconds)

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }
}
