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
using KodakkuAssist.Extensions;
using Newtonsoft.Json;
using System.Runtime.Intrinsics.Arm;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.GameOperate;
using System.Collections.Concurrent;
using KodakkuAssist.Module.Draw.Manager;




namespace KodakkuAssistXSZYYS
{
    internal static class EOMineDatabase
    {
        public struct Mine
        {
            public Vector3 Position;
            public bool IsLarge;
        }

        public class MineGroup
        {
            public List<Mine> Mines = new List<Mine>();
        }

        // 所有坐标数据源自Triggernometry脚本，并进行了 Y/Z 轴的对调
        // Triggernometry (X, Y, Z) -> Kodakku (X, Z, Y)
        public static readonly List<MineGroup> AllMines = new List<MineGroup>
        {
            // --- Map ID: 969 Data ---
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(655.610f, -500f, -85.595f), IsLarge = false }, new Mine { Position = new Vector3(673.758f, -500f, -56.485f), IsLarge = false },
                new Mine { Position = new Vector3(668.247f, -500f, -56.545f), IsLarge = false }, new Mine { Position = new Vector3(655.730f, -500f, -78.541f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(681.673f, -500f, -76.610f), IsLarge = true }, new Mine { Position = new Vector3(678.532f, -500f, -80.284f), IsLarge = true },
                new Mine { Position = new Vector3(672.796f, -500f, -72.422f), IsLarge = true },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(624.481f, -492f, -5.538f), IsLarge = false }, new Mine { Position = new Vector3(619.426f, -492f, -5.512f), IsLarge = false },
                new Mine { Position = new Vector3(597.776f, -489f, 4.601f), IsLarge = false }, new Mine { Position = new Vector3(597.776f, -489f, 7.265f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(583.575f, -489f, -45.560f), IsLarge = false }, new Mine { Position = new Vector3(581.296f, -489f, -39.905f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(625.880f, -489f, -101.845f), IsLarge = false }, new Mine { Position = new Vector3(629.658f, -489f, -103.990f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(595.772f, -489f, 48.175f), IsLarge = false }, new Mine { Position = new Vector3(590.393f, -489f, 49.879f), IsLarge = false },
                new Mine { Position = new Vector3(585.271f, -489f, 51.714f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(588.825f, -489f, 75.448f), IsLarge = true }, new Mine { Position = new Vector3(591.016f, -489f, 78.811f), IsLarge = true },
                new Mine { Position = new Vector3(631.057f, -489f, 115.441f), IsLarge = true }, new Mine { Position = new Vector3(634.203f, -489f, 117.537f), IsLarge = true },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(551.283f, -489f, 43.721f), IsLarge = false }, new Mine { Position = new Vector3(553.133f, -489f, 49.612f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(590.707f, -489f, 92.890f), IsLarge = false }, new Mine { Position = new Vector3(586.762f, -489f, 97.586f), IsLarge = false },
                new Mine { Position = new Vector3(583.005f, -489f, 102.095f), IsLarge = false }, new Mine { Position = new Vector3(615.843f, -489f, 130.046f), IsLarge = false },
                new Mine { Position = new Vector3(619.233f, -489f, 125.969f), IsLarge = false }, new Mine { Position = new Vector3(623.224f, -489f, 121.120f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(656.048f, -489f, 119.488f), IsLarge = false }, new Mine { Position = new Vector3(657.851f, -489f, 114.205f), IsLarge = false },
                new Mine { Position = new Vector3(659.676f, -489f, 108.972f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(726.456f, -500f, -56.775f), IsLarge = false }, new Mine { Position = new Vector3(731.754f, -500f, -56.775f), IsLarge = false },
                new Mine { Position = new Vector3(744.431f, -500f, -79.189f), IsLarge = false }, new Mine { Position = new Vector3(744.491f, -500f, -84.931f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(732.095f, -500f, -76.629f), IsLarge = true }, new Mine { Position = new Vector3(728.670f, -500f, -79.736f), IsLarge = true },
                new Mine { Position = new Vector3(723.247f, -500f, -72.448f), IsLarge = true },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(775.480f, -492f, -5.563f), IsLarge = false }, new Mine { Position = new Vector3(780.481f, -492f, -5.511f), IsLarge = false },
                new Mine { Position = new Vector3(802.218f, -489f, 4.565f), IsLarge = false }, new Mine { Position = new Vector3(802.158f, -489f, 8.566f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(818.813f, -489f, -39.423f), IsLarge = false }, new Mine { Position = new Vector3(816.455f, -489f, -45.560f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(773.639f, -489f, -101.845f), IsLarge = false }, new Mine { Position = new Vector3(770.055f, -489f, -103.990f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(814.789f, -489f, 51.775f), IsLarge = false }, new Mine { Position = new Vector3(809.498f, -489f, 49.875f), IsLarge = false },
                new Mine { Position = new Vector3(804.292f, -489f, 48.117f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(811.631f, -489f, 75.448f), IsLarge = true }, new Mine { Position = new Vector3(809.176f, -489f, 78.811f), IsLarge = true },
                new Mine { Position = new Vector3(768.902f, -489f, 115.715f), IsLarge = true }, new Mine { Position = new Vector3(765.759f, -489f, 117.537f), IsLarge = true },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(847.013f, -489f, 49.612f), IsLarge = false }, new Mine { Position = new Vector3(848.844f, -489f, 43.721f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(809.605f, -489f, 93.299f), IsLarge = false }, new Mine { Position = new Vector3(813.746f, -489f, 98.215f), IsLarge = false },
                new Mine { Position = new Vector3(817.092f, -489f, 102.252f), IsLarge = false }, new Mine { Position = new Vector3(784.267f, -489f, 130.046f), IsLarge = false },
                new Mine { Position = new Vector3(780.596f, -489f, 125.781f), IsLarge = false }, new Mine { Position = new Vector3(776.574f, -489f, 120.878f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(744.107f, -489f, 119.396f), IsLarge = false }, new Mine { Position = new Vector3(742.111f, -489f, 114.238f), IsLarge = false },
                new Mine { Position = new Vector3(740.298f, -489f, 108.810f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(703.450f, -504f, 99.934f), IsLarge = false }, new Mine { Position = new Vector3(700.088f, -504f, 99.942f), IsLarge = false },
                new Mine { Position = new Vector3(697.010f, -504f, 99.962f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(703.007f, -504f, 79.006f), IsLarge = false }, new Mine { Position = new Vector3(700.039f, -504f, 79.008f), IsLarge = false },
                new Mine { Position = new Vector3(696.987f, -504f, 78.969f), IsLarge = false },
            }},
            // --- Map ID: 970 Data ---
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(-566f, -852f, 292f), IsLarge = false }, new Mine { Position = new Vector3(-566f, -852f, 299f), IsLarge = false },
                new Mine { Position = new Vector3(-566f, -852f, 306f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(-538f, -852f, 292f), IsLarge = false }, new Mine { Position = new Vector3(-538f, -852f, 299f), IsLarge = false },
                new Mine { Position = new Vector3(-538f, -852f, 306f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(-503f, -852f, 292f), IsLarge = false }, new Mine { Position = new Vector3(-503f, -852f, 299f), IsLarge = false },
                new Mine { Position = new Vector3(-503f, -852f, 306f), IsLarge = false }, new Mine { Position = new Vector3(-503f, -852f, 299f), IsLarge = true },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(-469.965f, -852f, 292f), IsLarge = false }, new Mine { Position = new Vector3(-469.965f, -852f, 306f), IsLarge = false },
                new Mine { Position = new Vector3(-469.965f, -852f, 306f), IsLarge = true }, new Mine { Position = new Vector3(-469.965f, -852f, 292f), IsLarge = true },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(-434f, -852f, 292f), IsLarge = true }, new Mine { Position = new Vector3(-434f, -852f, 299f), IsLarge = true },
                new Mine { Position = new Vector3(-434f, -852f, 306f), IsLarge = true },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(-566f, -852f, 322f), IsLarge = false }, new Mine { Position = new Vector3(-566f, -852f, 329f), IsLarge = false },
                new Mine { Position = new Vector3(-566f, -852f, 336f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(-538f, -852f, 322f), IsLarge = false }, new Mine { Position = new Vector3(-538f, -852f, 329f), IsLarge = false },
                new Mine { Position = new Vector3(-538f, -852f, 336f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(-503f, -852f, 322f), IsLarge = false }, new Mine { Position = new Vector3(-503f, -852f, 336f), IsLarge = false },
                new Mine { Position = new Vector3(-503f, -852f, 329f), IsLarge = true },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(-470f, -852f, 322f), IsLarge = false }, new Mine { Position = new Vector3(-470f, -852f, 336f), IsLarge = false },
                new Mine { Position = new Vector3(-470f, -852f, 336f), IsLarge = true }, new Mine { Position = new Vector3(-470f, -852f, 322f), IsLarge = true },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(-433.965f, -852f, 322f), IsLarge = true }, new Mine { Position = new Vector3(-434f, -852f, 329f), IsLarge = true },
                new Mine { Position = new Vector3(-433.965f, -852f, 336f), IsLarge = true },
            }},
            // --- Map ID: 971 Data ---
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(698.518f, -500f, -295.730f), IsLarge = false }, new Mine { Position = new Vector3(701.741f, -500f, -295.730f), IsLarge = false },
                new Mine { Position = new Vector3(701.822f, -500f, -315.723f), IsLarge = false }, new Mine { Position = new Vector3(698.399f, -500f, -315.723f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(700f, -500f, -340f), IsLarge = true }, new Mine { Position = new Vector3(700f, -500f, -348f), IsLarge = true },
                new Mine { Position = new Vector3(692f, -500f, -348f), IsLarge = true }, new Mine { Position = new Vector3(684f, -500f, -348f), IsLarge = true },
                new Mine { Position = new Vector3(708f, -500f, -348f), IsLarge = true }, new Mine { Position = new Vector3(716f, -500f, -348f), IsLarge = true },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(652.021f, -500f, -340.308f), IsLarge = false }, new Mine { Position = new Vector3(652.149f, -500f, -347.825f), IsLarge = false },
                new Mine { Position = new Vector3(652.149f, -500f, -355.692f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(618.559f, -499.850f, -344.559f), IsLarge = false }, new Mine { Position = new Vector3(618.559f, -499.850f, -351.348f), IsLarge = false },
                new Mine { Position = new Vector3(625.348f, -499.850f, -351.348f), IsLarge = false }, new Mine { Position = new Vector3(625.348f, -499.850f, -344.559f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(609.542f, -499.842f, -341.369f), IsLarge = true }, new Mine { Position = new Vector3(615.590f, -499.894f, -360.189f), IsLarge = true },
                new Mine { Position = new Vector3(633.782f, -499.895f, -354.508f), IsLarge = true }, new Mine { Position = new Vector3(629.012f, -499.840f, -335.971f), IsLarge = true },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(747.914f, -500f, -355.825f), IsLarge = false }, new Mine { Position = new Vector3(747.914f, -500f, -348f), IsLarge = false },
                new Mine { Position = new Vector3(747.914f, -500f, -340.262f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(781.526f, -499.850f, -351.034f), IsLarge = false }, new Mine { Position = new Vector3(781.526f, -499.850f, -344.897f), IsLarge = false },
                new Mine { Position = new Vector3(774.631f, -499.850f, -344.897f), IsLarge = false }, new Mine { Position = new Vector3(774.631f, -499.850f, -351.034f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(784.567f, -499.869f, -360.189f), IsLarge = true }, new Mine { Position = new Vector3(790.533f, -499.840f, -341.369f), IsLarge = true },
                new Mine { Position = new Vector3(771.499f, -499.890f, -335.971f), IsLarge = true }, new Mine { Position = new Vector3(765.746f, -499.874f, -354.508f), IsLarge = true },
            }},
            // --- Map ID: 986 Data ---
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(696.5f, -500f, -474.5f), IsLarge = false }, new Mine { Position = new Vector3(703.5f, -500f, -474.5f), IsLarge = false },
                new Mine { Position = new Vector3(703.5f, -500f, -467.5f), IsLarge = false }, new Mine { Position = new Vector3(696.5f, -500f, -467.5f), IsLarge = false },
                new Mine { Position = new Vector3(700f, -500f, -471f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(691.5f, -500f, -476f), IsLarge = true }, new Mine { Position = new Vector3(695f, -500f, -479.5f), IsLarge = true },
                new Mine { Position = new Vector3(705f, -500f, -479.5f), IsLarge = true }, new Mine { Position = new Vector3(708.5f, -500f, -476f), IsLarge = true },
                new Mine { Position = new Vector3(708.5f, -500f, -466f), IsLarge = true }, new Mine { Position = new Vector3(704.5f, -500f, -462.5f), IsLarge = true },
                new Mine { Position = new Vector3(695f, -500f, -462.5f), IsLarge = true }, new Mine { Position = new Vector3(691.5f, -500f, -466f), IsLarge = true },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(673f, -500f, -468.5f), IsLarge = false }, new Mine { Position = new Vector3(673f, -500f, -473.5f), IsLarge = false },
                new Mine { Position = new Vector3(727f, -500f, -473.5f), IsLarge = false }, new Mine { Position = new Vector3(727f, -500f, -468.5f), IsLarge = false },
                new Mine { Position = new Vector3(739f, -500f, -468.5f), IsLarge = false }, new Mine { Position = new Vector3(739f, -500f, -473.5f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(630f, -500f, -467f), IsLarge = false }, new Mine { Position = new Vector3(630f, -500f, -475f), IsLarge = false },
                new Mine { Position = new Vector3(638f, -500f, -475f), IsLarge = false }, new Mine { Position = new Vector3(638f, -500f, -467f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(802.5f, -500f, -477f), IsLarge = false }, new Mine { Position = new Vector3(806f, -500f, -471f), IsLarge = false },
                new Mine { Position = new Vector3(818f, -500f, -483.5f), IsLarge = false }, new Mine { Position = new Vector3(811.5f, -500f, -486.5f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(817f, -500f, -520.5f), IsLarge = false }, new Mine { Position = new Vector3(811f, -500f, -522f), IsLarge = false },
                new Mine { Position = new Vector3(809.5f, -500f, -528f), IsLarge = false }, new Mine { Position = new Vector3(811f, -500f, -534f), IsLarge = false },
                new Mine { Position = new Vector3(817f, -500f, -535.5f), IsLarge = false }, new Mine { Position = new Vector3(823f, -500f, -534f), IsLarge = false },
                new Mine { Position = new Vector3(824.5f, -500f, -528f), IsLarge = false }, new Mine { Position = new Vector3(823f, -500f, -522f), IsLarge = false },
            }},
            new MineGroup { Mines = {
                new Mine { Position = new Vector3(694.5f, -498.476f, -503.5f), IsLarge = false }, new Mine { Position = new Vector3(700f, -498.476f, -503.5f), IsLarge = false },
                new Mine { Position = new Vector3(705.5f, -498.476f, -503.5f), IsLarge = false }, new Mine { Position = new Vector3(705.5f, -493.556f, -524f), IsLarge = false },
                new Mine { Position = new Vector3(700f, -493.556f, -524f), IsLarge = false }, new Mine { Position = new Vector3(694.5f, -493.556f, -524f), IsLarge = false },
                new Mine { Position = new Vector3(694.5f, -488.756f, -544f), IsLarge = false }, new Mine { Position = new Vector3(700f, -488.756f, -544f), IsLarge = false },
                new Mine { Position = new Vector3(705.5f, -488.756f, -544f), IsLarge = false }, new Mine { Position = new Vector3(705.5f, -483.956f, -564f), IsLarge = false },
                new Mine { Position = new Vector3(700f, -483.956f, -564f), IsLarge = false }, new Mine { Position = new Vector3(694.5f, -483.956f, -564f), IsLarge = false },
                new Mine { Position = new Vector3(694.5f, -479.156f, -584f), IsLarge = false }, new Mine { Position = new Vector3(700f, -479.156f, -584f), IsLarge = false },
                new Mine { Position = new Vector3(705.5f, -479.156f, -584f), IsLarge = false }, new Mine { Position = new Vector3(705.5f, -476f, -603.5f), IsLarge = false },
                new Mine { Position = new Vector3(700f, -476f, -603.5f), IsLarge = false }, new Mine { Position = new Vector3(694.5f, -476f, -603.5f), IsLarge = false },
            }},
        };
    }
    
    
    [ScriptType(
    name: "力之塔排雷（塔内）",
    guid: "874D3ECF-BD6B-448F-BB42-AE7F082E4999",
    territorys: [1252],
    version: "0.0.2",
    author: "XSZYYS",
    note: "塔内在聊天栏输入[/e 新月排雷]即可开始排雷。再次输入可关闭显示。显示持续1000s，如果显示消失，则请重新输入"
    )]
    public class 力之塔排雷
    {
        private bool _areMinesShown = false;
        private readonly object _lock = new object();
        // 脚本初始化时调用，用于重置状态
        public void Init(ScriptAccessory accessory)
        {
            lock (_lock)
            {
                _areMinesShown = false;
            }
            // 移除所有可能残留的绘图
            accessory.Method.RemoveDraw(".*");
            accessory.Log.Debug("新月岛排雷脚本已初始化。");
        }
        [ScriptMethod(
            name: "切换地雷位置显示",
            eventType: EventTypeEnum.Chat,
            eventCondition: ["Type:Echo", "Message:新月排雷"] // 使用 /echo 新月排雷 触发
        )]
        public void ToggleMineDisplay(Event @event, ScriptAccessory accessory)
        {
            lock (_lock)
            {
                _areMinesShown = !_areMinesShown;

                if (_areMinesShown)
                {
                    DrawAllMines(accessory);
                    accessory.Method.TextInfo("显示地雷位置", 2000);
                }
                else
                {
                    // 使用正则表达式移除所有以此开头的绘图
                    accessory.Method.RemoveDraw("EO_Mine_.*");
                    accessory.Method.TextInfo("隐藏地雷位置", 2000);
                }
            }
        }
        [ScriptMethod(
            name: "大雷生成处理",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["Operate:Add", "DataId:2014585"]
        )]
        public void OnLargeMineSpawn(Event @event, ScriptAccessory accessory)
        {
            HandleMineSpawn(@event.SourcePosition, true, accessory);
        }

        [ScriptMethod(
            name: "小雷生成处理",
            eventType: EventTypeEnum.ObjectChanged,
            eventCondition: ["Operate:Add", "DataId:2014584"]
        )]
        public void OnSmallMineSpawn(Event @event, ScriptAccessory accessory)
        {
            HandleMineSpawn(@event.SourcePosition, false, accessory);
        }

        private void HandleMineSpawn(Vector3 spawnedPosition, bool isLargeSpawned, ScriptAccessory accessory)
        {
            int groupIndex = 0;
            // 查找该地雷所属的雷区组
            foreach (var group in EOMineDatabase.AllMines)
            {
                int mineIndex = 0;
                foreach (var mine in group.Mines)
                {
                    if (Vector3.Distance(mine.Position, spawnedPosition) < 1.5f) // 使用1.5m的容差进行位置匹配
                    {
                        // 1. 清理该组所有的初始位置标记
                        int innerMineIndex = 0;
                        foreach (var mineToClear in group.Mines)
                        {
                            accessory.Method.RemoveDraw($"EO_Mine_G{groupIndex}_M{innerMineIndex}");
                            innerMineIndex++;
                        }

                        // 2. 绘制实际地雷的爆炸范围
                        DrawPropertiesEdit dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"EO_Explosion_G{groupIndex}_M{mineIndex}";
                        dp.Position = spawnedPosition;
                        dp.Color = new Vector4(1.0f, 0.0f, 0.0f, 0.0f); // 鲜红色, 0% 透明度
                        dp.DestoryAt = 1000000; // 显示1000秒

                        // 根据原始Triggernometry脚本数据设置爆炸半径
                        dp.Scale = isLargeSpawned ? new Vector2(30f, 30f) : new Vector2(7f, 7f); 

                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
                        
                        // 找到并处理后即可退出外层循环
                        goto Found;
                    }
                    mineIndex++;
                }
                groupIndex++;
            }
            Found:;
        }
        private void DrawAllMines(ScriptAccessory accessory)
        {
            // --- 绘图配置 ---
            const long displayDuration = 10000000; // 显示时长 (ms)
            var smallMineColor = new Vector4(1.0f, 0.65f, 0.0f, 0.5f); // 橙色 (Orange) 带透明度
            var largeMineColor = new Vector4(0.86f, 0.08f, 0.23f, 0.5f); // 猩红色 (Crimson Red) 带透明度
            var smallMineRadius = new Vector2(4f, 4f); // 小雷显示半径
            var largeMineRadius = new Vector2(4f, 4f); // 大雷显示半径
            // ------------------
            int groupIndex = 0;
            foreach (var group in EOMineDatabase.AllMines)
            {
                int mineIndex = 0;
                foreach (var mine in group.Mines)
                {
                    DrawPropertiesEdit dp = accessory.Data.GetDefaultDrawProperties();

                    // 使用坐标创建唯一名称，方便未来进行单独移除
                    dp.Name = $"EO_Mine_G{groupIndex}_M{mineIndex}";
                    dp.Position = mine.Position; // 直接使用地雷的世界坐标
                    dp.DestoryAt = displayDuration;

                    if (mine.IsLarge)
                    {
                        dp.Color = largeMineColor;
                        dp.Scale = largeMineRadius;
                    }
                    else
                    {
                        dp.Color = smallMineColor;
                        dp.Scale = smallMineRadius;
                    }
                
                    // 发送绘图请求
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
                    mineIndex++;
                }
                groupIndex++;
            }
        }
        [ScriptMethod(
            name: "盗贼扫雷",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:41648"]
        )]
        public void OnThiefScan(Event @event, ScriptAccessory accessory)
        {
            var scanPosition = @event.SourcePosition;
            const float scanRadius = 15f;

            int groupIndex = 0;
            foreach (var group in EOMineDatabase.AllMines)
            {
                int mineIndex = 0;
                foreach (var mine in group.Mines)
                {
                    if (Vector3.Distance(mine.Position, scanPosition) <= scanRadius)
                    {
                        // 此地雷在扫描范围内，移除其绘制
                        accessory.Method.RemoveDraw($"EO_Mine_G{groupIndex}_M{mineIndex}");
                    }
                    mineIndex++;
                }
                groupIndex++;
            }
        }
        [ScriptMethod(
            name: "猎人排雷",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:41601"]
        )]
        public void OnHunterScan(Event @event, ScriptAccessory accessory)
        {
            // 猎人扫雷是以技能为中心
            var scanPosition = @event.EffectPosition;
            const float scanRadius = 9f;
            int groupIndex = 0;
            foreach (var group in EOMineDatabase.AllMines)
            {
                int mineIndex = 0;
                foreach (var mine in group.Mines)
                {
                    if (Vector3.Distance(mine.Position, scanPosition) <= scanRadius)
                    {
                        accessory.Method.RemoveDraw($"EO_Mine_G{groupIndex}_M{mineIndex}");
                    }
                    mineIndex++;
                }
                groupIndex++;
            }
        }
        [ScriptMethod(
            name: "雷爆炸",
            eventType: EventTypeEnum.ActionEffect,
            eventCondition: ["ActionId:regex:^(42050|42051)$"] 
        )]
        public void OnMineExplosion(Event @event, ScriptAccessory accessory)
        {
            var explosionPosition = @event.SourcePosition;

            int groupIndex = 0;
            foreach (var group in EOMineDatabase.AllMines)
            {
                int mineIndex = 0;
                foreach (var mine in group.Mines)
                {
                    if (Vector3.Distance(mine.Position, explosionPosition) < 1.5f)
                    {
                        // 找到了爆炸的地雷，清除其所有相关绘制
                        accessory.Method.RemoveDraw($"EO_Mine_G{groupIndex}_M{mineIndex}");
                        accessory.Method.RemoveDraw($"EO_Explosion_G{groupIndex}_M{mineIndex}");
                        // 假设一个位置只有一个雷，找到后即可返回
                        return;
                    }
                    mineIndex++;
                }
                groupIndex++;
            }
        }
    }
}