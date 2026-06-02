using UnityEngine;
using UnityEditor;
using MinecraftEngine;
using System.IO;
using System.Collections.Generic;

public class ItemDatabaseGenerator : EditorWindow
{
    [MenuItem("MinecraftEngine/Generate Item SOs (Auto)")]
    public static void GenerateItems()
    {
        string basePath = "Assets/Resources/Items";
        EnsureDir(basePath);
        EnsureDir(basePath + "/Tools");
        EnsureDir(basePath + "/Food");
        EnsureDir(basePath + "/Armor");
        EnsureDir(basePath + "/Materials");

        int created = 0;
        ushort nextID = 1000; // Non-block items start at 1000

        // ══════════════════════════════════════════
        // 1. TOOLS
        // ══════════════════════════════════════════
        var tiers = new (string name, ToolTier tier, int dur, float speed, int harvest, int ench, float dmgSword, float dmgAxe, float dmgPick, float spdSword, float spdAxe)[]
        {
            ("Wooden",    ToolTier.Wood,      59,  2f, 0, 15, 4f,  7f,  2f, 1.6f, 0.8f),
            ("Stone",     ToolTier.Stone,    131,  4f, 1,  5, 5f,  9f,  3f, 1.6f, 0.8f),
            ("Iron",      ToolTier.Iron,     250,  6f, 2, 14, 6f,  9f,  4f, 1.6f, 0.9f),
            ("Golden",    ToolTier.Gold,      32, 12f, 0, 22, 4f,  7f,  2f, 1.6f, 1.0f),
            ("Diamond",   ToolTier.Diamond, 1561,  8f, 3, 10, 7f,  9f,  5f, 1.6f, 1.0f),
            ("Netherite",ToolTier.Netherite,2031,  9f, 3, 15, 8f, 10f,  6f, 1.6f, 1.0f),
        };

        var toolTypes = new (string suffix, ToolType type, float baseDmg, float baseSpd)[]
        {
            ("Pickaxe", ToolType.Pickaxe, 2f, 1.2f),
            ("Axe",     ToolType.Axe,     7f, 0.8f),
            ("Shovel",  ToolType.Shovel,  1f, 1.0f),
            ("Hoe",     ToolType.Hoe,     1f, 1.0f),
            ("Sword",   ToolType.Sword,   4f, 1.6f),
        };

        foreach (var t in tiers)
        {
            foreach (var tt in toolTypes)
            {
                string itemName = $"{t.name}_{tt.suffix}";
                string path = $"{basePath}/Tools/{itemName}.asset";
                if (File.Exists(path)) { nextID++; continue; }

                ToolData tool = ScriptableObject.CreateInstance<ToolData>();
                tool.itemID = nextID++;
                tool.itemName = itemName.Replace("_", " ");
                tool.type = tt.type == ToolType.Sword ? ItemType.Weapon : ItemType.Tool;
                tool.maxStackSize = 1;
                tool.toolType = tt.type;
                tool.tier = t.tier;
                tool.maxDurability = t.dur;
                tool.miningSpeedMultiplier = t.speed;
                tool.harvestLevel = t.harvest;
                tool.enchantability = t.ench;

                // Damage per tool type
                switch (tt.type)
                {
                    case ToolType.Sword:   tool.attackDamage = t.dmgSword; tool.attackSpeed = t.spdSword; break;
                    case ToolType.Axe:     tool.attackDamage = t.dmgAxe;   tool.attackSpeed = t.spdAxe;   break;
                    case ToolType.Pickaxe: tool.attackDamage = t.dmgPick;  tool.attackSpeed = 1.2f;       break;
                    case ToolType.Shovel:  tool.attackDamage = t.dmgPick;  tool.attackSpeed = 1.0f;       break;
                    case ToolType.Hoe:     tool.attackDamage = 1f;         tool.attackSpeed = 1.0f;       break;
                }

                AssetDatabase.CreateAsset(tool, path);
                created++;
            }
        }

        // Misc tools
        CreateToolIfMissing(basePath + "/Tools/Shears.asset", ref nextID, "Shears", ToolType.Shears, ToolTier.Iron, 238, 1f, 0, 15, 1f, 4f, ref created);
        CreateToolIfMissing(basePath + "/Tools/Flint_And_Steel.asset", ref nextID, "Flint And Steel", ToolType.None, ToolTier.Iron, 64, 1f, 0, 0, 1f, 4f, ref created);
        CreateToolIfMissing(basePath + "/Tools/Fishing_Rod.asset", ref nextID, "Fishing Rod", ToolType.None, ToolTier.Wood, 64, 1f, 0, 10, 1f, 4f, ref created);
        CreateToolIfMissing(basePath + "/Tools/Bow.asset", ref nextID, "Bow", ToolType.None, ToolTier.Wood, 384, 1f, 0, 1, 1f, 4f, ref created);
        CreateToolIfMissing(basePath + "/Tools/Crossbow.asset", ref nextID, "Crossbow", ToolType.None, ToolTier.Wood, 465, 1f, 0, 1, 1f, 4f, ref created);
        CreateToolIfMissing(basePath + "/Tools/Trident.asset", ref nextID, "Trident", ToolType.None, ToolTier.Iron, 250, 1f, 0, 1, 9f, 1.1f, ref created);
        CreateToolIfMissing(basePath + "/Tools/Shield.asset", ref nextID, "Shield", ToolType.None, ToolTier.Wood, 336, 1f, 0, 0, 1f, 4f, ref created);

        // ══════════════════════════════════════════
        // 2. FOOD
        // ══════════════════════════════════════════
        var foods = new (string name, int nutr, float sat, float eatDur, bool always)[]
        {
            ("Apple",              4, 0.3f,  1.61f, false),
            ("Baked_Potato",       5, 0.6f,  1.61f, false),
            ("Bread",              5, 0.6f,  1.61f, false),
            ("Carrot",             3, 0.6f,  1.61f, false),
            ("Cooked_Beef",        8, 0.8f,  1.61f, false),
            ("Cooked_Chicken",     6, 0.6f,  1.61f, false),
            ("Cooked_Cod",         5, 0.6f,  1.61f, false),
            ("Cooked_Mutton",      6, 0.8f,  1.61f, false),
            ("Cooked_Porkchop",    8, 0.8f,  1.61f, false),
            ("Cooked_Rabbit",      5, 0.6f,  1.61f, false),
            ("Cooked_Salmon",      6, 0.8f,  1.61f, false),
            ("Cookie",             2, 0.1f,  1.61f, false),
            ("Dried_Kelp",         1, 0.6f,  0.865f,false),
            ("Golden_Apple",       4, 1.2f,  1.61f, true),
            ("Golden_Carrot",      6, 1.2f,  1.61f, false),
            ("Melon_Slice",        2, 0.3f,  1.61f, false),
            ("Mushroom_Stew",      6, 0.6f,  1.61f, false),
            ("Poisonous_Potato",   2, 0.3f,  1.61f, false),
            ("Pumpkin_Pie",        8, 0.3f,  1.61f, false),
            ("Raw_Beef",           3, 0.3f,  1.61f, false),
            ("Raw_Chicken",        2, 0.3f,  1.61f, false),
            ("Raw_Cod",            2, 0.1f,  1.61f, false),
            ("Raw_Mutton",         2, 0.3f,  1.61f, false),
            ("Raw_Porkchop",       3, 0.3f,  1.61f, false),
            ("Raw_Rabbit",         3, 0.3f,  1.61f, false),
            ("Raw_Salmon",         2, 0.1f,  1.61f, false),
            ("Rotten_Flesh",       4, 0.1f,  1.61f, false),
            ("Spider_Eye",         2, 0.8f,  1.61f, false),
            ("Sweet_Berries",      2, 0.1f,  1.61f, false),
            ("Glow_Berries",       2, 0.1f,  1.61f, false),
            ("Beetroot",           1, 0.6f,  1.61f, false),
            ("Beetroot_Soup",      6, 0.6f,  1.61f, false),
            ("Honey_Bottle",       6, 0.1f,  1.61f, false),
            ("Chorus_Fruit",       4, 0.3f,  1.61f, true),
        };

        foreach (var f in foods)
        {
            string path = $"{basePath}/Food/{f.name}.asset";
            if (File.Exists(path)) { nextID++; continue; }

            FoodData food = ScriptableObject.CreateInstance<FoodData>();
            food.itemID = nextID++;
            food.itemName = f.name.Replace("_", " ");
            food.type = ItemType.Food;
            food.maxStackSize = f.name.Contains("Stew") || f.name.Contains("Soup") || f.name.Contains("Bottle") ? 1 : 64;
            food.nutrition = f.nutr;
            food.saturationModifier = f.sat;
            food.eatDuration = f.eatDur;
            food.canAlwaysEat = f.always;

            AssetDatabase.CreateAsset(food, path);
            created++;
        }

        // ══════════════════════════════════════════
        // 3. ARMOR
        // ══════════════════════════════════════════
        var armorMats = new (string name, ArmorMaterial mat, int baseDur, int[] defense, float tough, float kb)[]
        {
            //                                    base  H  C  L  B  tough  kb
            ("Leather",   ArmorMaterial.Leather,    5, new[]{1,3,2,1}, 0f, 0f),
            ("Chainmail", ArmorMaterial.Chainmail,  15, new[]{2,5,4,1}, 0f, 0f),
            ("Iron",      ArmorMaterial.Iron,       15, new[]{2,6,5,2}, 0f, 0f),
            ("Golden",    ArmorMaterial.Gold,        7, new[]{2,5,3,1}, 0f, 0f),
            ("Diamond",   ArmorMaterial.Diamond,    33, new[]{3,8,6,3}, 2f, 0f),
            ("Netherite",ArmorMaterial.Netherite,   37, new[]{3,8,6,3}, 3f, 0.1f),
        };

        int[] durMult = { 11, 16, 15, 13 }; // helmet, chest, legs, boots
        string[] slotNames = { "Helmet", "Chestplate", "Leggings", "Boots" };

        foreach (var am in armorMats)
        {
            for (int s = 0; s < 4; s++)
            {
                string itemName = $"{am.name}_{slotNames[s]}";
                string path = $"{basePath}/Armor/{itemName}.asset";
                if (File.Exists(path)) { nextID++; continue; }

                ArmorData armor = ScriptableObject.CreateInstance<ArmorData>();
                armor.itemID = nextID++;
                armor.itemName = itemName.Replace("_", " ");
                armor.type = ItemType.Armor;
                armor.maxStackSize = 1;
                armor.slot = (ArmorSlot)s;
                armor.material = am.mat;
                armor.defensePoints = am.defense[s];
                armor.toughness = am.tough;
                armor.knockbackResistance = am.kb;
                armor.maxDurability = am.baseDur * durMult[s];

                AssetDatabase.CreateAsset(armor, path);
                created++;
            }
        }

        // Turtle Shell (helmet only)
        {
            string path = $"{basePath}/Armor/Turtle_Shell.asset";
            if (!File.Exists(path))
            {
                ArmorData turtle = ScriptableObject.CreateInstance<ArmorData>();
                turtle.itemID = nextID++;
                turtle.itemName = "Turtle Shell";
                turtle.type = ItemType.Armor;
                turtle.maxStackSize = 1;
                turtle.slot = ArmorSlot.Helmet;
                turtle.material = ArmorMaterial.Turtle;
                turtle.defensePoints = 2;
                turtle.toughness = 0f;
                turtle.knockbackResistance = 0f;
                turtle.maxDurability = 275;
                AssetDatabase.CreateAsset(turtle, path);
                created++;
            }
        }

        // ══════════════════════════════════════════
        // 4. MATERIALS (crafting ingredients)
        // ══════════════════════════════════════════
        var materials = new (string name, int stack)[]
        {
            ("Stick", 64), ("Coal", 64), ("Charcoal", 64), ("Diamond", 64),
            ("Emerald", 64), ("Iron_Ingot", 64), ("Gold_Ingot", 64), ("Copper_Ingot", 64),
            ("Netherite_Ingot", 64), ("Netherite_Scrap", 64), ("Raw_Iron", 64), ("Raw_Gold", 64),
            ("Raw_Copper", 64), ("Redstone", 64), ("Lapis_Lazuli", 64), ("Quartz", 64),
            ("Amethyst_Shard", 64), ("Flint", 64), ("Feather", 64), ("Leather", 64),
            ("String", 64), ("Gunpowder", 64), ("Blaze_Rod", 64), ("Blaze_Powder", 64),
            ("Ender_Pearl", 16), ("Eye_Of_Ender", 64), ("Ghast_Tear", 64),
            ("Slime_Ball", 64), ("Magma_Cream", 64), ("Glowstone_Dust", 64),
            ("Bone", 64), ("Bone_Meal", 64), ("Sugar", 64), ("Wheat", 64),
            ("Paper", 64), ("Book", 64), ("Ink_Sac", 64), ("Glow_Ink_Sac", 64),
            ("Iron_Nugget", 64), ("Gold_Nugget", 64), ("Nether_Star", 64),
            ("Phantom_Membrane", 64), ("Rabbit_Hide", 64), ("Rabbit_Foot", 64),
            ("Prismarine_Shard", 64), ("Prismarine_Crystals", 64),
            ("Nautilus_Shell", 64), ("Heart_Of_The_Sea", 64),
            ("Brick", 64), ("Nether_Brick", 64), ("Clay_Ball", 64),
            ("Snowball", 16), ("Egg", 16), ("Bucket", 16),
            ("Water_Bucket", 1), ("Lava_Bucket", 1), ("Milk_Bucket", 1),
            ("Name_Tag", 64), ("Saddle", 1), ("Compass", 64), ("Clock", 64),
            ("Map", 64), ("Lead", 64),
        };

        foreach (var m in materials)
        {
            string path = $"{basePath}/Materials/{m.name}.asset";
            if (File.Exists(path)) { nextID++; continue; }

            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.itemID = nextID++;
            item.itemName = m.name.Replace("_", " ");
            item.type = ItemType.Material;
            item.maxStackSize = m.stack;

            AssetDatabase.CreateAsset(item, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[ItemGen] ✅ Создано {created} предметов (ID {1000}—{nextID - 1}). Пропущены уже существующие.");
    }

    static void CreateToolIfMissing(string path, ref ushort id, string name, ToolType toolType, ToolTier tier,
        int dur, float speed, int harvest, int ench, float dmg, float atkSpd, ref int count)
    {
        if (File.Exists(path)) { id++; return; }

        ToolData tool = ScriptableObject.CreateInstance<ToolData>();
        tool.itemID = id++;
        tool.itemName = name;
        tool.type = ItemType.Tool;
        tool.maxStackSize = 1;
        tool.toolType = toolType;
        tool.tier = tier;
        tool.maxDurability = dur;
        tool.miningSpeedMultiplier = speed;
        tool.harvestLevel = harvest;
        tool.enchantability = ench;
        tool.attackDamage = dmg;
        tool.attackSpeed = atkSpd;

        AssetDatabase.CreateAsset(tool, path);
        count++;
    }

    static void EnsureDir(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }
}
