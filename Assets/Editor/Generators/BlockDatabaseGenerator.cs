using UnityEngine;
using UnityEditor;
using MinecraftEngine;
using System.IO;
using System.Collections.Generic;

public class BlockDatabaseGenerator : EditorWindow
{
    [MenuItem("MinecraftEngine/Generate Block SOs (Auto-Textures)")]
    public static void GenerateBlocks()
    {
        string blocksPath = "Assets/Resources/Blocks";
        if (!Directory.Exists(blocksPath)) Directory.CreateDirectory(blocksPath);

        string texturesFolder = "Assets/Textures/MinecraftTextures/block";
        if (!Directory.Exists(texturesFolder))
        {
            Debug.LogError($"Не найдена папка текстур: {texturesFolder}");
            return;
        }

        // 1. Строим индекс текстур
        string[] filePaths = Directory.GetFiles(texturesFolder, "*.png", SearchOption.TopDirectoryOnly);
        List<string> validPaths = new List<string>(filePaths);
        validPaths.Sort();

        Dictionary<string, int> texIndexMap = new Dictionary<string, int>();
        HashSet<string> texExists = new HashSet<string>();
        for (int i = 0; i < validPaths.Count; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(validPaths[i]);
            texIndexMap[fileName] = i;
            texExists.Add(fileName);
        }

        int GetTexIdx(string texName)
        {
            if (texIndexMap.TryGetValue(texName, out int idx)) return idx;
            return 0;
        }

        bool HasTex(string texName) => texExists.Contains(texName);

        // 2. Конвертер enum → snake_case
        string ToSnake(string name)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && i > 0) sb.Append('_');
                sb.Append(char.ToLower(name[i]));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Автоматически находит текстуры для блока по snake_case имени.
        /// Возвращает (top, bottom, front, back, left, right).
        /// Пробует паттерны: _top, _bottom, _side, _front, _back, _end, _left, _right.
        /// </summary>
        void AutoResolveTextures(string snake, out string top, out string bottom,
            out string front, out string back, out string left, out string right)
        {
            // Дефолт: все стороны = base name
            top = bottom = front = back = left = right = snake;

            // Если базовая текстура существует — используем как fallback для всех сторон
            bool baseExists = HasTex(snake);

            // === TOP ===
            if (HasTex(snake + "_top")) top = snake + "_top";
            else if (!baseExists && HasTex(snake + "_end")) top = snake + "_end"; // beehive_end

            // === BOTTOM ===
            if (HasTex(snake + "_bottom")) bottom = snake + "_bottom";
            else if (HasTex(snake + "_end")) bottom = snake + "_end";
            else bottom = top; // fallback: bottom = top

            // === SIDES ===
            string sidesTex = snake;
            if (HasTex(snake + "_side")) sidesTex = snake + "_side";

            front = back = left = right = sidesTex;

            // === FRONT (специальный) ===
            if (HasTex(snake + "_front")) front = snake + "_front";

            // === BACK (специальный) ===
            if (HasTex(snake + "_back")) back = snake + "_back";
            else back = sidesTex; // back = side если нет _back

            // Некоторые блоки имеют front≠back но side для left/right
            // cartography_table: side1, side2, side3 — берём side1
            if (HasTex(snake + "_side1")) left = snake + "_side1";
            if (HasTex(snake + "_side2")) right = snake + "_side2";
            if (HasTex(snake + "_side3"))
            {
                // 3 разных стороны: front + side3 + side1 + side2
                if (!HasTex(snake + "_front")) front = snake + "_side3";
                else back = snake + "_side3";
            }

            // Если нет base, нет _side, но есть _top — log-style block
            if (!baseExists && !HasTex(snake + "_side") && HasTex(snake + "_top"))
            {
                // Это колонна типа bone_block, hay_block
                // side уже установлен как snake (не существует), нужен fallback
                if (HasTex(snake + "_side")) sidesTex = snake + "_side";
                front = back = left = right = sidesTex;
            }
        }

        // 3. Генерируем SO
        int generated = 0;
        int warnings = 0;

        foreach (BlockType type in System.Enum.GetValues(typeof(BlockType)))
        {
            ushort id = (ushort)type;
            string name = type.ToString();
            string snake = ToSnake(name);

            BlockData block = ScriptableObject.CreateInstance<BlockData>();
            block.blockName = name;
            block.blockID = id;

            // Defaults
            block.isSolid = true;
            block.isTransparent = false;
            block.hardness = 1.5f;
            block.blastResistance = 6.0f;
            block.bestTool = ToolType.Pickaxe;
            block.soundCategory = SoundCategory.Stone;

            // === СВОЙСТВА ПО ТИПАМ ===
            ApplyBlockProperties(type, block);

            // === ТЕКСТУРЫ ===
            string texTop, texBottom, texFront, texBack, texLeft, texRight;

            // Сначала проверяем hardcoded маппинги (для нестандартных блоков)
            if (!TryGetHardcodedTextures(type, snake, HasTex, out texTop, out texBottom,
                    out texFront, out texBack, out texLeft, out texRight))
            {
                // Автоматический поиск по паттернам
                AutoResolveTextures(snake, out texTop, out texBottom,
                    out texFront, out texBack, out texLeft, out texRight);
            }

            if (type != BlockType.Air)
            {
                block.textureIndices[0] = GetTexIdx(texBack);
                block.textureIndices[1] = GetTexIdx(texFront);
                block.textureIndices[2] = GetTexIdx(texTop);
                block.textureIndices[3] = GetTexIdx(texBottom);
                block.textureIndices[4] = GetTexIdx(texLeft);
                block.textureIndices[5] = GetTexIdx(texRight);

                // Проверяем нашлись ли текстуры
                if (!HasTex(texTop) && !HasTex(texFront) && !HasTex(snake))
                {
                    Debug.LogWarning($"[BlockGen] Текстуры не найдены для {name} (snake: {snake})");
                    warnings++;
                }
            }

            AssetDatabase.CreateAsset(block, $"{blocksPath}/{id}_{name}.asset");
            generated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[BlockGen] ✅ Сгенерировано {generated} блоков. Предупреждений: {warnings}.");
    }

    /// <summary>
    /// Hardcoded текстурные маппинги для блоков с нестандартными именами.
    /// Возвращает true если маппинг найден.
    /// </summary>
    static bool TryGetHardcodedTextures(BlockType type, string snake,
        System.Func<string, bool> HasTex,
        out string top, out string bottom, out string front, out string back,
        out string left, out string right)
    {
        top = bottom = front = back = left = right = snake;

        switch (type)
        {
            // === Alias blocks: enum name ≠ texture name ===
            case BlockType.SmoothQuartz:
                top = bottom = "quartz_block_bottom"; front = back = left = right = "quartz_block_bottom"; return true;
            case BlockType.FurnaceLit:
                top = "furnace_top"; bottom = "furnace_top"; front = "furnace_front_on"; back = left = right = "furnace_side"; return true;
            case BlockType.Chest:
            case BlockType.TrappedChest:
            case BlockType.EnderChest:
                // Chest — entity-rendered в MC, используем oak_planks как placeholder
                top = bottom = front = back = left = right = "oak_planks"; return true;
            case BlockType.GrindstoneBlock:
                top = bottom = front = back = left = right = "grindstone_side"; return true;
            case BlockType.RedstoneDust:
                top = bottom = front = back = left = right = "redstone_dust_dot"; return true;
            case BlockType.StickyPiston:
                top = "piston_top_sticky"; bottom = "piston_bottom"; front = back = left = right = "piston_side"; return true;
            case BlockType.Piston:
                top = "piston_top"; bottom = "piston_bottom"; front = back = left = right = "piston_side"; return true;
            case BlockType.StoneButton:
            case BlockType.StonePressurePlate:
                top = bottom = front = back = left = right = "stone"; return true;
            case BlockType.OakButton:
            case BlockType.OakPressurePlate:
                top = bottom = front = back = left = right = "oak_planks"; return true;
            case BlockType.HeavyWeightedPressurePlate:
                top = bottom = front = back = left = right = "iron_block"; return true;
            case BlockType.LightWeightedPressurePlate:
                top = bottom = front = back = left = right = "gold_block"; return true;
            case BlockType.TargetBlock:
                top = bottom = "target_top"; front = back = left = right = "target_side"; return true;
            case BlockType.ShroomLight:
                top = bottom = front = back = left = right = "shroomlight"; return true;
            case BlockType.CampfireBlock:
                top = bottom = front = back = left = right = "campfire_log"; return true;
            case BlockType.SoulCampfire:
                top = bottom = front = back = left = right = "soul_campfire_log_lit"; return true;
            case BlockType.MagmaBlock:
                top = bottom = front = back = left = right = "magma"; return true;
            case BlockType.MudBlock:
                top = bottom = front = back = left = right = "mud"; return true;
            case BlockType.SculkBlock:
                top = bottom = front = back = left = right = "sculk"; return true;
            case BlockType.SculkVeins:
                top = bottom = front = back = left = right = "sculk_vein"; return true;
            case BlockType.DriedKelpBlock:
                top = "dried_kelp_top"; bottom = "dried_kelp_bottom"; front = back = left = right = "dried_kelp_side"; return true;
            case BlockType.HoneyBlock:
                top = "honey_block_top"; bottom = "honey_block_bottom"; front = back = left = right = "honey_block_side"; return true;
            case BlockType.SlimeBlock:
                top = bottom = front = back = left = right = "slime_block"; return true;

            // Waxed copper = same texture as non-waxed
            case BlockType.WaxedCopperBlock:
                top = bottom = front = back = left = right = "copper_block"; return true;
            case BlockType.WaxedExposedCopper:
                top = bottom = front = back = left = right = "exposed_copper"; return true;
            case BlockType.WaxedWeatheredCopper:
                top = bottom = front = back = left = right = "weathered_copper"; return true;
            case BlockType.WaxedOxidizedCopper:
                top = bottom = front = back = left = right = "oxidized_copper"; return true;

            // Plants with stages — use final stage
            case BlockType.Bamboo:
                top = bottom = front = back = left = right = "bamboo_stalk"; return true;
            case BlockType.SweetBerryBush:
                top = bottom = front = back = left = right = "sweet_berry_bush_stage3"; return true;
            case BlockType.GlowBerries:
                top = bottom = front = back = left = right = "cave_vines_lit"; return true;
            case BlockType.SeaGrass:
                top = bottom = front = back = left = right = "seagrass"; return true;
            case BlockType.GlowLichen:
                top = bottom = front = back = left = right = "glow_lichen"; return true;

            // Crops — use final stage
            case BlockType.Wheat:
                top = bottom = front = back = left = right = "wheat_stage7"; return true;
            case BlockType.Carrots:
                top = bottom = front = back = left = right = "carrots_stage3"; return true;
            case BlockType.Potatoes:
                top = bottom = front = back = left = right = "potatoes_stage3"; return true;
            case BlockType.Beetroots:
                top = bottom = front = back = left = right = "beetroots_stage3"; return true;
            case BlockType.NetherWart:
                top = bottom = front = back = left = right = "nether_wart_stage2"; return true;
            case BlockType.CocoaBeans:
                top = bottom = front = back = left = right = "cocoa_stage2"; return true;

            // Sub-block items: use parent texture
            case BlockType.OakFence:
            case BlockType.OakFenceGate:
            case BlockType.OakSlab:
            case BlockType.OakStairs:
            case BlockType.OakSign:
                top = bottom = front = back = left = right = "oak_planks"; return true;
            case BlockType.OakTrapdoor:
                top = bottom = front = back = left = right = "oak_trapdoor"; return true;
            case BlockType.OakDoor:
                top = front = back = left = right = "oak_door_top"; bottom = "oak_door_bottom"; return true;
            case BlockType.IronDoor:
                top = front = back = left = right = "iron_door_top"; bottom = "iron_door_bottom"; return true;
            case BlockType.StoneSlab:
                top = bottom = front = back = left = right = "stone"; return true;
            case BlockType.CobbleSlab:
                top = bottom = front = back = left = right = "cobblestone"; return true;

            // Bed — entity-rendered, placeholder
            case BlockType.Bed:
                top = bottom = front = back = left = right = "red_wool"; return true;

            // Grass-like: top/bottom/side
            case BlockType.GrassBlock:
                top = "grass_block_top"; bottom = "dirt"; front = back = left = right = "grass_block_side"; return true;
            case BlockType.Podzol:
                top = "podzol_top"; bottom = "dirt"; front = back = left = right = "podzol_side"; return true;
            case BlockType.Mycelium:
                top = "mycelium_top"; bottom = "dirt"; front = back = left = right = "mycelium_side"; return true;
            case BlockType.GrassPath:
                top = "dirt_path_top"; bottom = "dirt"; front = back = left = right = "dirt_path_side"; return true;
            case BlockType.CrimsonNylium:
                top = "crimson_nylium"; bottom = "netherrack"; front = back = left = right = "crimson_nylium_side"; return true;
            case BlockType.WarpedNylium:
                top = "warped_nylium"; bottom = "netherrack"; front = back = left = right = "warped_nylium_side"; return true;

            // Logs: top/side
            case BlockType.OakLog: top = bottom = "oak_log_top"; front = back = left = right = "oak_log"; return true;
            case BlockType.BirchLog: top = bottom = "birch_log_top"; front = back = left = right = "birch_log"; return true;
            case BlockType.SpruceLog: top = bottom = "spruce_log_top"; front = back = left = right = "spruce_log"; return true;
            case BlockType.JungleLog: top = bottom = "jungle_log_top"; front = back = left = right = "jungle_log"; return true;
            case BlockType.AcaciaLog: top = bottom = "acacia_log_top"; front = back = left = right = "acacia_log"; return true;
            case BlockType.DarkOakLog: top = bottom = "dark_oak_log_top"; front = back = left = right = "dark_oak_log"; return true;
            case BlockType.CherryLog: top = bottom = "cherry_log_top"; front = back = left = right = "cherry_log"; return true;
            case BlockType.MangroveLog: top = bottom = "mangrove_log_top"; front = back = left = right = "mangrove_log"; return true;
            case BlockType.CrimsonStem: top = bottom = "crimson_stem_top"; front = back = left = right = "crimson_stem"; return true;
            case BlockType.WarpedStem: top = bottom = "warped_stem_top"; front = back = left = right = "warped_stem"; return true;

            // Pillar-style
            case BlockType.BoneBlock: top = bottom = "bone_block_top"; front = back = left = right = "bone_block_side"; return true;
            case BlockType.HayBale: top = bottom = "hay_block_top"; front = back = left = right = "hay_block_side"; return true;
            case BlockType.QuartzBlock: top = bottom = "quartz_block_top"; front = back = left = right = "quartz_block_side"; return true;
            case BlockType.PurpurPillar: top = bottom = "purpur_pillar_top"; front = back = left = right = "purpur_pillar"; return true;
            case BlockType.BasaltBlock: top = bottom = "basalt_top"; front = back = left = right = "basalt_side"; return true;
            case BlockType.Deepslate: top = bottom = "deepslate_top"; front = back = left = right = "deepslate"; return true;
            case BlockType.AncientDebris: top = bottom = "ancient_debris_top"; front = back = left = right = "ancient_debris_side"; return true;

            // Fluids
            case BlockType.Water: top = bottom = front = back = left = right = "water_still"; return true;
            case BlockType.Lava: top = bottom = front = back = left = right = "lava_still"; return true;

            // Sandstone
            case BlockType.Sandstone: top = "sandstone_top"; bottom = "sandstone_bottom"; front = back = left = right = "sandstone"; return true;
            case BlockType.RedSandstone: top = "red_sandstone_top"; bottom = "red_sandstone_bottom"; front = back = left = right = "red_sandstone"; return true;

            // TNT
            case BlockType.TNT: top = "tnt_top"; bottom = "tnt_bottom"; front = back = left = right = "tnt_side"; return true;

            // Bookshelf
            case BlockType.Bookshelf: top = bottom = "oak_planks"; front = back = left = right = "bookshelf"; return true;

            // Anvil
            case BlockType.Anvil: top = "anvil_top"; bottom = front = back = left = right = "anvil"; return true;

            // Enchanting Table (short block)
            case BlockType.EnchantingTable: top = "enchanting_table_top"; bottom = "enchanting_table_bottom"; front = back = left = right = "enchanting_table_side"; return true;

            // Obsidian-based
            case BlockType.Obsidian: top = bottom = front = back = left = right = "obsidian"; return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Устанавливает свойства блока (hardness, tool, sound, etc.) по типу.
    /// </summary>
    static void ApplyBlockProperties(BlockType type, BlockData block)
    {
        // Transparent / non-solid
        switch (type)
        {
            case BlockType.Air:
            case BlockType.Water:
            case BlockType.Lava:
                block.isSolid = false; block.isTransparent = true; block.hardness = 0f; break;
            case BlockType.Glass:
            case BlockType.OakLeaves:
            case BlockType.BirchLeaves:
            case BlockType.SpruceLeaves:
            case BlockType.JungleLeaves:
            case BlockType.AcaciaLeaves:
            case BlockType.DarkOakLeaves:
            case BlockType.CherryLeaves:
            case BlockType.MangroveLeaves:
            case BlockType.Ice:
                block.isTransparent = true; break;
        }

        // Leaves
        if (type.ToString().Contains("Leaves"))
        {
            block.hardness = 0.2f; block.bestTool = ToolType.Shears;
            block.soundCategory = SoundCategory.Grass; block.isFlammable = true;
        }

        // Wood
        if (type.ToString().Contains("Log") || type.ToString().Contains("Stem") ||
            type.ToString().Contains("Planks") || type.ToString().Contains("BambooBlock"))
        {
            block.bestTool = ToolType.Axe; block.hardness = 2.0f;
            block.soundCategory = SoundCategory.Wood; block.isFlammable = true;
        }

        // Dirt-family
        switch (type)
        {
            case BlockType.Dirt:
            case BlockType.GrassBlock:
            case BlockType.Podzol:
            case BlockType.Mycelium:
            case BlockType.GrassPath:
            case BlockType.Farmland:
            case BlockType.MudBlock:
                block.hardness = 0.5f; block.bestTool = ToolType.Shovel;
                block.soundCategory = SoundCategory.Grass; break;
        }

        // Sand/Gravel
        switch (type)
        {
            case BlockType.Sand:
            case BlockType.RedSand:
                block.hardness = 0.5f; block.bestTool = ToolType.Shovel;
                block.soundCategory = SoundCategory.Sand; block.isGravityAffected = true; break;
            case BlockType.Gravel:
                block.hardness = 0.6f; block.bestTool = ToolType.Shovel;
                block.soundCategory = SoundCategory.Gravel; block.isGravityAffected = true; break;
        }

        // Glass
        if (type.ToString().Contains("Glass"))
        {
            block.hardness = 0.3f; block.isTransparent = true;
            block.soundCategory = SoundCategory.Glass;
        }

        // Wool
        if (type.ToString().Contains("Wool"))
        {
            block.hardness = 0.8f; block.bestTool = ToolType.Shears;
            block.soundCategory = SoundCategory.Wool; block.isFlammable = true;
        }

        // Concrete
        if (type.ToString().Contains("Concrete"))
        {
            block.hardness = 1.8f; block.bestTool = ToolType.Pickaxe;
        }

        // Bedrock
        if (type == BlockType.Bedrock) { block.hardness = -1f; block.blastResistance = 3600000f; }

        // Obsidian
        if (type == BlockType.Obsidian || type == BlockType.CryingObsidian)
        {
            block.hardness = 50f; block.blastResistance = 1200f;
            block.harvestLevel = 3;
        }

        // Ancient Debris
        if (type == BlockType.AncientDebris)
        {
            block.hardness = 30f; block.blastResistance = 1200f;
            block.harvestLevel = 3;
        }

        // Deepslate
        if (type.ToString().Contains("Deepslate"))
        {
            block.hardness = 3.0f; block.soundCategory = SoundCategory.Deepslate;
        }

        // Light emitters
        switch (type)
        {
            case BlockType.Torch: case BlockType.SoulTorch: block.lightEmission = 14; block.isSolid = false; break;
            case BlockType.Glowstone: block.lightEmission = 15; break;
            case BlockType.SeaLantern: block.lightEmission = 15; break;
            case BlockType.Lantern: block.lightEmission = 15; break;
            case BlockType.SoulLantern: block.lightEmission = 10; break;
            case BlockType.RedstoneLamp: block.lightEmission = 15; break;
            case BlockType.ShroomLight: block.lightEmission = 15; break;
            case BlockType.JackOLantern: block.lightEmission = 15; break;
            case BlockType.MagmaBlock: block.lightEmission = 3; break;
            case BlockType.Lava: block.lightEmission = 15; break;
            case BlockType.CryingObsidian: block.lightEmission = 10; break;
            case BlockType.RespawnAnchor: block.lightEmission = 15; break;
            case BlockType.CampfireBlock: block.lightEmission = 15; break;
            case BlockType.SoulCampfire: block.lightEmission = 10; break;
            case BlockType.GlowBerries: block.lightEmission = 14; block.isSolid = false; break;
            case BlockType.GlowLichen: block.lightEmission = 7; block.isSolid = false; break;
        }

        // Interactable
        switch (type)
        {
            case BlockType.CraftingTable:
            case BlockType.Furnace:
            case BlockType.FurnaceLit:
            case BlockType.Chest:
            case BlockType.EnderChest:
            case BlockType.TrappedChest:
            case BlockType.Barrel:
            case BlockType.Smoker:
            case BlockType.BlastFurnace:
            case BlockType.EnchantingTable:
            case BlockType.Anvil:
            case BlockType.BrewingStand:
            case BlockType.Loom:
            case BlockType.CartographyTable:
            case BlockType.GrindstoneBlock:
            case BlockType.SmithingTable:
            case BlockType.Stonecutter:
            case BlockType.Lectern:
                block.isInteractable = true; break;
        }

        // Cross-shaped (non-solid, transparent)
        if (type.ToString().Contains("Sapling") || type.ToString().Contains("Tulip") ||
            type == BlockType.Dandelion || type == BlockType.Poppy || type == BlockType.BlueOrchid ||
            type == BlockType.Allium || type == BlockType.AzureBluet || type == BlockType.OxeyeDaisy ||
            type == BlockType.Cornflower || type == BlockType.LilyOfTheValley || type == BlockType.WitherRose ||
            type == BlockType.TallGrass || type == BlockType.ShortGrass || type == BlockType.Fern ||
            type == BlockType.DeadBush || type == BlockType.SweetBerryBush || type == BlockType.Torchflower)
        {
            block.isSolid = false; block.isTransparent = true; block.hardness = 0f;
            block.shape = BlockShape.Cross; block.soundCategory = SoundCategory.Grass;
        }
    }
}
