using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.Draw;
using System.Numerics;
using Newtonsoft.Json;
using KodakkuAssist.Module.Draw.Manager;

namespace A12S_Scripts
{
    [ScriptType(name: "亚历山-大零式机神城 律动之章4",
                territorys: [532],
                guid: "f5e4d3c2-b1a0-9876-5432-10fedcba9876",
                version: "1.0.8",
                author: "XSZYYS")]
    public class A12S
    {
        /// <summary>
        /// This method is called when the script is initialized.
        /// It's used to clean up any drawings from the previous load.
        /// </summary>
        public void Init(ScriptAccessory accessory)
        {
            // Remove all existing drawings managed by this script
            accessory.Method.RemoveDraw(".*");
        }

        /// <summary>
        /// Draws the danger zone for the "Mega Beam" ability.
        /// This is triggered when any entity starts casting the ability with ID 5678 or 5732.
        /// </summary>
        /// <param name="event">The event data for the cast start.</param>
        /// <param name="accessory">The script accessory for interacting with the game.</param>
        [ScriptMethod(name: "巨型激光炮",
                      eventType: EventTypeEnum.StartCasting,
                      eventCondition: ["ActionId:regex:^(5678|5732)$"])]
        public void MegaBeam(Event @event, ScriptAccessory accessory)
        {
            // Try to parse the cast time from the event data.
            if (!uint.TryParse(@event["DurationMilliseconds"], out var castTime))
            {
                // If parsing fails, use a default duration of 5 seconds.
                castTime = 5000;
            }

            // Get the default properties for drawing.
            var dp = accessory.Data.GetDefaultDrawProperties();

            // Set the properties for the drawing.
            dp.Name = "A12S_MegaBeam_Danger_Zone";         // Unique name for the drawing
            dp.Owner = @event.SourceId;                    // Anchor the drawing to the caster
            dp.Scale = new Vector2(10, 50);                // Set the rectangle's size: 10m width, 50m length
            dp.Color = accessory.Data.DefaultDangerColor;  // Use the default danger color
            dp.DestoryAt = castTime;                       // The drawing will disappear when the cast finishes

            // Send the command to draw the rectangle in the game world.
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        /// <summary>
        /// Draws the danger zone for the "Double Rocket Punch" ability.
        /// This is triggered when any entity starts casting the ability with ID 5731.
        /// </summary>
        /// <param name="event">The event data for the cast start.</param>
        /// <param name="accessory">The script accessory for interacting with the game.</param>
        [ScriptMethod(name: "双重火箭飞拳",
                      eventType: EventTypeEnum.StartCasting,
                      eventCondition: ["ActionId:5731"])]
        public void DoubleRocketPunch(Event @event, ScriptAccessory accessory)
        {
            // Try to parse the cast time from the event data.
            if (!uint.TryParse(@event["DurationMilliseconds"], out var castTime))
            {
                // If parsing fails, use a default duration.
                castTime = 5000;
            }

            // Get the default properties for drawing.
            var dp = accessory.Data.GetDefaultDrawProperties();

            // Set the properties for the drawing.
            dp.Name = "A12S_DoubleRocketPunch_Danger_Zone"; // Unique name for the drawing
            dp.Owner = @event.TargetId;                     // Anchor the drawing to the target of the ability
            dp.Scale = new Vector2(5, 5);                   // Set the circle's radius to 5m
            dp.Color = accessory.Data.DefaultDangerColor;   // Use the default danger color
            dp.DestoryAt = castTime;                        // The drawing will disappear when the cast finishes

            // Send the command to draw the circle in the game world.
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        /// <summary>
        /// Draws the danger zone for the "Super Jump" ability.
        /// This is triggered when any entity starts casting the ability with ID 5733.
        /// The circle will be placed on the player furthest from the caster.
        /// </summary>
        /// <param name="event">The event data for the cast start.</param>
        /// <param name="accessory">The script accessory for interacting with the game.</param>
        [ScriptMethod(name: "超级跳跃",
                      eventType: EventTypeEnum.StartCasting,
                      eventCondition: ["ActionId:5733"])]
        public void SuperJump(Event @event, ScriptAccessory accessory)
        {
            // Try to parse the cast time from the event data.
            if (!uint.TryParse(@event["DurationMilliseconds"], out var castTime))
            {
                // If parsing fails, use a default duration.
                castTime = 5000;
            }

            // Get the default properties for drawing.
            var dp = accessory.Data.GetDefaultDrawProperties();

            // Set the properties for the drawing.
            dp.Name = "A12S_SuperJump_Danger_Zone";          // Unique name for the drawing
            dp.Owner = @event.SourceId;                      // The drawing's position is relative to the caster
            dp.Scale = new Vector2(5, 5);                    // Set the circle's radius to 5m
            dp.Color = accessory.Data.DefaultDangerColor;    // Use the default danger color
            dp.DestoryAt = castTime;                         // The drawing will disappear when the cast finishes

            // Set the drawing's center to resolve to the player furthest from the Owner (the caster).
            // This ensures the circle tracks the correct player throughout the cast.
            dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;

            // Send the command to draw the circle in the game world.
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        /// <summary>
        /// Draws the danger zone for the "Apocalyptic Ray" ability.
        /// This is triggered after the ability effect (ID 5734) has occurred.
        /// </summary>
        /// <param name="event">The event data for the action effect.</param>
        /// <param name="accessory">The script accessory for interacting with the game.</param>
        [ScriptMethod(name: "末世宣言",
                      eventType: EventTypeEnum.ActionEffect,
                      eventCondition: ["ActionId:5734"])]
        public void ApocalypticRay(Event @event, ScriptAccessory accessory)
        {
            // Get the default properties for drawing.
            var dp = accessory.Data.GetDefaultDrawProperties();

            // Set the properties for the drawing.
            dp.Name = "A12S_ApocalypticRay_Danger_Zone";    // Unique name for the drawing
            dp.Owner = @event.SourceId;                     // Anchor the drawing to the caster
            dp.Scale = new Vector2(25, 25);                 // Set the fan's radius to 25m
            dp.Radian = MathF.PI / 2;                       // Set the angle to 90 degrees (PI/2 radians)
            dp.Color = accessory.Data.DefaultDangerColor;   // Use the default danger color
            dp.DestoryAt = 5000;                            // The drawing will last for 5000ms (5 seconds)

            // Send the command to draw the fan in the game world.
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        /// <summary>
        /// Draws the danger zone for the "Laser Chakram" ability.
        /// This is triggered when any entity starts casting the ability with ID 5716.
        /// </summary>
        /// <param name="event">The event data for the cast start.</param>
        /// <param name="accessory">The script accessory for interacting with the game.</param>
        [ScriptMethod(name: "激光战轮",
                      eventType: EventTypeEnum.StartCasting,
                      eventCondition: ["ActionId:5716"])]
        public void LaserChakram(Event @event, ScriptAccessory accessory)
        {
            // Try to parse the cast time from the event data.
            if (!uint.TryParse(@event["DurationMilliseconds"], out var castTime))
            {
                // If parsing fails, use a default duration.
                castTime = 5000;
            }

            // Get the default properties for drawing.
            var dp = accessory.Data.GetDefaultDrawProperties();

            // Set the properties for the drawing.
            dp.Name = "A12S_LaserChakram_Danger_Zone";    // Unique name for the drawing
            dp.Owner = @event.SourceId;                   // Anchor the drawing to the caster
            dp.Scale = new Vector2(6, 70);                // Set the rectangle's size: 6m width, 70m length
            dp.Color = accessory.Data.DefaultDangerColor; // Use the default danger color
            dp.DestoryAt = castTime;                      // The drawing will disappear when the cast finishes

            // Send the command to draw the rectangle in the game world.
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        /// <summary>
        /// Draws the danger zone for the "Phantom System" ability.
        /// This is triggered when a player receives the TargetIcon with ID 8.
        /// </summary>
        /// <param name="event">The event data for the TargetIcon.</param>
        /// <param name="accessory">The script accessory for interacting with the game.</param>
        [ScriptMethod(name: "幻影系统",
                      eventType: EventTypeEnum.TargetIcon,
                      eventCondition: ["Id:0008"])]
        public void PhantomSystem(Event @event, ScriptAccessory accessory)
        {
            // Get the default properties for drawing.
            var dp = accessory.Data.GetDefaultDrawProperties();

            // Set the properties for the drawing.
            dp.Name = "A12S_PhantomSystem_Danger_Zone";    // Unique name for the drawing
            dp.Owner = @event.TargetId;                   // Anchor the drawing to the targeted player
            dp.Scale = new Vector2(5, 5);                 // Set the circle's radius to 5m
            dp.Color = accessory.Data.DefaultDangerColor; // Use the default danger color
            dp.DestoryAt = 5000;                          // The drawing will last for 5000ms (5 seconds)

            // Send the command to draw the circle in the game world.
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }
}
