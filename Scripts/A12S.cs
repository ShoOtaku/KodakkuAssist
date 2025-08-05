using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.Draw;
using System.Numerics;
using Newtonsoft.Json;
using KodakkuAssist.Module.Draw.Manager;

namespace A12S_Scripts
{
    [ScriptType(name: "亚历山大零式机神城 天动之章4",
                territorys: [587],
                guid: "b3a4c5d6-e7f8-9a0b-1c2d-3e4f5a6b7c8d",
                version: "0.0.1",
                author: "XSZYYS")]
    public class A12S
    {
        public void Init(ScriptAccessory accessory)
        {
            accessory.Method.RemoveDraw(".*");
        }

        /// 惩戒射线读条时，在目标身上绘制圆形危险区。
        [ScriptMethod(name: "惩戒射线",
                      eventType: EventTypeEnum.StartCasting,
                      eventCondition: ["ActionId:6633"])]
        public void PunishingRay(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["DurationMilliseconds"], out var castTime))
            {
                castTime = 4000;
            }

            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "A12S_PunishingRay_Danger_Zone";
            dp.Owner = @event.TargetId;
            dp.Scale = new Vector2(4, 4);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = castTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        /// 白光之鞭点名时，在目标身上绘制圆形危险区。
        [ScriptMethod(name: "白光之鞭",
                      eventType: EventTypeEnum.TargetIcon,
                      eventCondition: ["Id:001E"])]
        public void WhiteArchonFlame(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "A12S_WhiteArchonFlame_Danger_Zone";
            dp.Owner = @event.TargetId;
            dp.Scale = new Vector2(4, 4);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 10000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        /// 重力异常读条时，在释放者位置绘制圆形危险区。
        [ScriptMethod(name: "重力异常",
                      eventType: EventTypeEnum.StartCasting,
                      eventCondition: ["ActionId:6642"])]
        public void GravityAnomaly(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["DurationMilliseconds"], out var castTime))
            {
                castTime = 5000;
            }

            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "A12S_GravityAnomaly_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(8, 8);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = castTime;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        /// 拜火圣礼读条时，在释放者位置绘制月环危险区。
        [ScriptMethod(name: "拜火圣礼",
                      eventType: EventTypeEnum.StartCasting,
                      eventCondition: ["ActionId:6637"])]
        public void HolyRite(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "A12S_HolyRite_Danger_Zone";
            dp.Owner = @event.SourceId;
            dp.Scale = new Vector2(60, 60);
            dp.InnerScale = new Vector2(8, 8);
            dp.Radian = MathF.PI * 2;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 6000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }

        /// 十字圣礼读条时，在释放者位置绘制十字危险区。
        [ScriptMethod(name: "十字圣礼",
                      eventType: EventTypeEnum.StartCasting,
                      eventCondition: ["ActionId:6635"])]
        public void CrossHoly(Event @event, ScriptAccessory accessory)
        {
            if (!uint.TryParse(@event["DurationMilliseconds"], out var castTime))
            {
                castTime = 6000;
            }

            // 绘制第一条直线 (前后)
            var dp1 = accessory.Data.GetDefaultDrawProperties();
            dp1.Name = "A12S_CrossHoly_Danger_Zone_1";
            dp1.Owner = @event.SourceId;
            dp1.Scale = new Vector2(16, 120);
            dp1.Color = accessory.Data.DefaultDangerColor;
            dp1.DestoryAt = castTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);

            // 绘制第二条直线 (左右)
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Name = "A12S_CrossHoly_Danger_Zone_2";
            dp2.Owner = @event.SourceId;
            dp2.Scale = new Vector2(16, 120);
            dp2.Rotation = MathF.PI / 2; // 旋转90度
            dp2.Color = accessory.Data.DefaultDangerColor;
            dp2.DestoryAt = castTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
        }

        /// 当玩家被附加名誉罪状态时，绘制大圈危险区。
        [ScriptMethod(name: "名誉罪",
                      eventType: EventTypeEnum.StatusAdd,
                      eventCondition: ["StatusID:1120"])]
        public void Dishonor(Event @event, ScriptAccessory accessory)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "A12S_Dishonor_Danger_Zone";
            dp.Owner = @event.TargetId;
            dp.Scale = new Vector2(30, 30);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 15000;

            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }
}
