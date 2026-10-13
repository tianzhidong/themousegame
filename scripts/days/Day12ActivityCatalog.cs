#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace NewGameProject.Days;

public enum Day12ActivityKind { Balloons, Instruments, Eggs, Gifts, Food, Luggage, Tower, Aquarium }

public sealed record Day12ActivityDefinition(
    string Id, string Title, string Instruction, Day12ActivityKind Kind,
    string[] Textures, string[] Names, string[]? OpenedTextures = null, string[]? Recipients = null);

public static class Day12ActivityCatalog
{
    private static string[] P(int day, params string[] names) =>
        names.Select(name => $"res://assets/images/objects/day{day}/{name}.png").ToArray();
    private static string[] T(int day, params string[] names) =>
        names.Select(name => $"res://assets/images/objects/day{day}/{name}.tres").ToArray();
    private static readonly string[] Friends = P(7, "doll_rabbit", "doll_bear", "doll_cat", "doll_fox", "doll_puppy");
    private static readonly string[] FriendNames = ["小兔", "小熊", "小猫", "小狐狸", "小狗"];

    private static readonly Dictionary<string, Day12ActivityDefinition> Activities = new()
    {
        ["click_balloons"] = new("click_balloons", "彩色气球摊", "点击5个气球，看看它们怎样绽放彩纸！",
            Day12ActivityKind.Balloons, P(6, "balloon_blue", "balloon_green", "balloon_blue", "balloon_green", "balloon_blue"),
            ["气球", "气球", "气球", "气球", "气球"]),
        ["click_instruments"] = new("click_instruments", "音乐小舞台", "点击5种乐器，让小舞台响起音乐！",
            Day12ActivityKind.Instruments, T(12, "instrument_drum", "instrument_xylophone", "instrument_piano", "instrument_trumpet", "instrument_guitar"),
            ["小鼓", "木琴", "钢琴", "小号", "吉他"]),
        ["open_eggs"] = new("open_eggs", "惊喜彩蛋屋", "连续点击两次，打开5颗彩蛋！",
            Day12ActivityKind.Eggs, T(8, "egg_red_closed", "egg_blue_closed", "egg_yellow_closed", "egg_green_closed", "egg_purple_closed"),
            ["红色彩蛋", "蓝色彩蛋", "黄色彩蛋", "绿色彩蛋", "紫色彩蛋"],
            T(8, "egg_red_opened", "egg_blue_opened", "egg_yellow_opened", "egg_green_opened", "egg_purple_opened")),
        ["open_gifts"] = new("open_gifts", "神秘礼物摊", "双击打开5个礼盒，发现里面的惊喜！",
            Day12ActivityKind.Gifts, T(8, "box_red_closed", "box_teal_closed", "box_yellow_closed", "box_purple_closed", "box_green_closed"),
            ["星星礼盒", "条纹礼盒", "花朵礼盒", "月亮礼盒", "叶子礼盒"],
            T(8, "box_red_opened", "box_teal_opened", "box_yellow_opened", "box_purple_opened", "box_green_opened")),
        ["deliver_food"] = new("deliver_food", "动物美食街", "按餐盘上的小图案，把5份食物拖给朋友！",
            Day12ActivityKind.Food, P(4, "vegetable_carrot", "fruit_apple", "fruit_pear", "vegetable_broccoli", "fruit_orange"),
            ["胡萝卜", "苹果", "梨", "西兰花", "橙子"], null, Friends),
        ["load_luggage"] = new("load_luggage", "欢乐小火车", "把5件行李拖进图案相同的车厢！",
            Day12ActivityKind.Luggage, P(4, "clothing_sun_hat", "clothing_jacket", "toy_beach_ball", "toy_car").Concat(P(7, "book_green")).ToArray(),
            ["帽子", "外套", "皮球", "玩具车", "故事书"]),
        ["find_tower_friends"] = new("find_tower_friends", "树屋观景塔", "双击5扇窗户，看看是谁藏在树屋里！",
            Day12ActivityKind.Tower, Friends, FriendNames),
        ["find_aquatic_animals"] = new("find_aquatic_animals", "河底水族馆", "点击5种水生动物，让它们吐出泡泡！",
            Day12ActivityKind.Aquarium, P(3, "fish", "turtle", "frog", "snall").Concat(P(12, "river_crayfish")).ToArray(),
            ["小金鱼", "小乌龟", "青蛙", "蜗牛", "小龙虾"])
    };

    public static Day12ActivityDefinition Get(string id) =>
        Activities.TryGetValue(id, out Day12ActivityDefinition? definition)
            ? definition : throw new ArgumentException($"Unknown Day12 activity: {id}");

}

