using System;
using System.Numerics;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using Newtonsoft.Json;

namespace KodakkuAssistXSZYYS
{
    [ScriptType(
        name: "新月岛北伐",
        guid: "7E2B9A14-5C8F-4D6A-B3E1-9F0A2C4D5E6F",
        territorys: [1346],
        version: "0.0.1",
        author: "XSZYYS",
        note: "新月岛北伐CE：雪石膏之剑读条预警"
    )]
    public class 新月岛北伐
    {
        // =================================================================================
        // ============================= 雪石膏之剑 =============================
        // =================================================================================
        private const uint SnowgypsumSwordActionId = 47158;   // 雪石膏之剑读条技能ID
        private const float SnowgypsumSwordRadius = 40f;      // 扇形半径
        private const float SnowgypsumSwordAngleDeg = 90f;    // 扇形角度
        private const long SnowgypsumSwordDurationMs = 3000;  // 预警持续时长

        [ScriptMethod(
            name: "雪石膏之剑 (扇形预警)",
            eventType: EventTypeEnum.StartCasting,
            eventCondition: ["ActionId:47158"]
        )]
        public void SnowgypsumSword(Event @event, ScriptAccessory accessory)
        {
            if (!TryDeserializeFloat(@event["SourceRotation"], out var srot)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "SnowgypsumSword_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(SnowgypsumSwordRadius);
            dp.Radian = DegToRad(SnowgypsumSwordAngleDeg);
            dp.Rotation = srot;                  // 以该实体读条时的面向为基准
            dp.FixRotation = true;               // 读条期间保持初始朝向
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = SnowgypsumSwordDurationMs;
            dp.ScaleMode |= ScaleMode.ByTime;    // 随时间填充的预警效果

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        #region Helper_Functions

        private static float DegToRad(float degrees) => degrees * MathF.PI / 180.0f;

        /// <summary>
        /// 安全地从JSON字符串反序列化float
        /// </summary>
        private static bool TryDeserializeFloat(string json, out float result)
        {
            try
            {
                if (string.IsNullOrEmpty(json))
                {
                    result = default;
                    return false;
                }
                result = JsonConvert.DeserializeObject<float>(json);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        #endregion
    }
}
