# 🏗️ UNICRAFT — Полный анализ проекта и план реализации Minecraft 1.20

---

## 📊 ЧАСТЬ 1: АНАЛИЗ ТЕКУЩЕГО СОСТОЯНИЯ ПРОЕКТА

### 1.1 Общие сведения
| Параметр | Значение |
|---|---|
| **Unity** | 6000.4.8f1 (Unity 6) |
| **Render Pipeline** | URP 17.4.0 |
| **Input** | Input System 1.19.0 (New) |
| **Namespace** | `MinecraftEngine` |
| **Сцен** | 1 (`MainScene`) |
| **Скриптов (авторских)** | 20 шт. |
| **Пакеты** | URP, Input System, AI Navigation, TextMeshPro, Timeline |
| **InputActions файл** | `Assets/Unity/InputSystem_Actions.inputactions` — **не используется** (ввод читается напрямую через `Keyboard.current` / `Mouse.current`) |

### 1.2 Архитектура скриптов

```
Assets/Scripts/
├── Core/                         # Ядро движка
│   ├── BlockType.cs              # Enum 50 типов блоков (byte 0..49)
│   ├── VoxelData.cs              # Статические данные вершин/граней воксела
│   ├── VoxelSettings.cs          # Константы: 16×384×16 чанк, SeaLevel=63, YOffset=64
│   └── UIManager.cs              # Singleton. Программный UI: Loading Screen, Hotbar (9 слотов), Crosshair, 3D рендер иконок
│
├── Data/                         # ScriptableObject-система данных
│   ├── BiomeData.cs              # SO: Multi-Noise параметры биома (temp/hum/cont/ero/dep)
│   ├── BiomeDatabase.cs          # Singleton MonoBehaviour → NativeArray<BiomeStruct>
│   ├── BlockData.cs              # SO: 6 текстурных индексов, hardness, bestTool, dropItemBlockID
│   └── BlockDatabase.cs          # Singleton MonoBehaviour → NativeArray<BlockStruct> (256 слотов)
│
├── Player/                       # Игрок
│   ├── PlayerController.cs       # Движение (WASD), гравитация, прыжок, присед, AABB-коллизия воксельная
│   ├── PlayerInteraction.cs      # Ломание/установка блоков, VoxelRaycast (DDA), Survival-breaking с трещинами
│   ├── PlayerInventory.cs        # Hotbar 9 слотов (ItemStack), подбор, scroll выбор
│   └── SpectatorFly.cs           # Свободный полёт (F3)
│
├── Items/                        # Предметные сущности
│   ├── ItemEntity.cs             # Дроп-блок: физика, bobbing-анимация, pickup с засасыванием
│   └── ItemManager.cs            # Singleton. Object Pool для дропов, кэш мешей блоков
│
└── WorldGeneration/              # Генерация мира
    ├── TerrainJob.cs             # IJob + BurstCompile: Multi-Noise terrain, cheese/spaghetti caves, deepslate layer
    ├── LightUpdateJob.cs         # IJob + BurstCompile: Sunlight + BFS flood fill
    ├── ChunkMeshJob.cs           # IJob + BurstCompile: Mesh gen, 3 sub-mesh, vertex-baked lighting
    ├── ChunkRenderer.cs          # MonoBehaviour: pipeline (Terrain→Light→Mesh), NativeArray пулинг
    ├── WorldManager.cs           # MonoBehaviour: chunk loading/unloading, frustum culling
    └── DebugCamera.cs            # Свободная камера
```

### 1.3 Что уже реализовано ✅

| Категория | Статус | Детали |
|---|---|---|
| **Мир** | ✅ Рабочий | 16×384×16 чанки, координаты -64..+320 (как MC 1.18+) |
| **Terrain Gen** | ✅ Хороший | Multi-Noise (5 параметров), Cheese/Spaghetti caves, deepslate transition |
| **Биомы** | ⚠️ Базовые | 8 биомов, выбор по Евклидову расстоянию в 5D noise space. Нет интерполяции |
| **Освещение** | ✅ Рабочее | Sunlight (15 уровней) + BFS flood fill, cross-chunk propagation |
| **Чанк-менеджмент** | ✅ Хороший | Async pipeline (Terrain→Light→Mesh), object pooling, frustum culling |
| **Job System** | ✅ Burst | TerrainJob, LightUpdateJob, ChunkMeshJob — всё на IJob + BurstCompile |
| **Рендеринг** | ⚠️ Кастомный | Texture2DArray + CGPROGRAM шейдеры (НЕ URP-совместимые!), 3 sub-mesh |
| **Игрок** | ✅ Базовый | WASD + прыжок + присед + Sprint, AABB-коллизия, камера с sensitivity |
| **Взаимодействие** | ✅ Survival | DDA-рейкаст, ломание с трещинами (10 стадий), установка с rotation |
| **Инвентарь** | ⚠️ Только Hotbar | 9 слотов, scroll/1-9, стакинг до 64, подбор дропов |
| **Item Drops** | ✅ Рабочие | Object pool, физика, bobbing-анимация, pickup с засасыванием |
| **UI** | ⚠️ Хардкод | Весь UI создаётся программно. Loading Screen, Hotbar, Crosshair |
| **Block States** | ⚠️ Начальные | Только rotation OakLog (3 состояния через 4 бита blockData) |
| **Input System** | ⚠️ Частично | `Keyboard.current`/`Mouse.current` напрямую, `.inputactions` не подключён |

### 1.4 Критические проблемы / Technical Debt 🔴

1. **Шейдеры на CGPROGRAM** — не совместимы с URP
2. **`UnityEditor` в Runtime** — `UIManager.cs` и `PlayerInteraction.cs` → билд упадёт
3. **Singletons** — UIManager, BlockDatabase, BiomeDatabase, ItemManager — static без DI
4. **InputActions не используются** — весь ввод через прямые обращения
5. **Нет сохранения мира** — модификации теряются
6. **Нет системы предметов** — только BlockID
7. **Утечки NativeArray** — slice-ы на Persistent без гарантированного Dispose

---

## 📋 ЧАСТЬ 2: ДЕТАЛИЗИРОВАННЫЙ ПЛАН РЕАЛИЗАЦИИ MINECRAFT 1.20

> **Правила выполнения:**
> 1. Каждая задача выполняется до получения инструкции на следующую
> 2. После реализации каждой механики — обязательная отладка
> 3. Все механики максимально близки к оригиналу MC 1.20
> 4. Оптимизации применяются только когда результат **объективно лучше** оригинала

---

### ═══════════════════════════════════════════════════════
### ФАЗА 0 — Рефакторинг и Фундамент
### ═══════════════════════════════════════════════════════

#### 0.1 Убрать `UnityEditor` из Runtime-кода
- **0.1.1** В `UIManager.cs` → `SetupItemRenderer()`: заменить `UnityEditor.AssetDatabase.LoadAssetAtPath<Material>` на `Resources.Load<Material>("Materials/ChunkMaterial")` (материал уже лежит в `Assets/Materials/`, нужно переместить в `Resources/Materials/` или использовать `[SerializeField]` ссылку)
- **0.1.2** В `UIManager.cs` → `CreateHotbarUI()`: аналогичная замена для `widgets.png` (уже загружается через `Resources.Load`, fallback на `AssetDatabase` не нужен)
- **0.1.3** В `PlayerInteraction.cs` → `CreateCracksMesh()`: заменить `UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BlockCracksMat.mat")` на `Resources.Load` или `[SerializeField]`
- **0.1.4** Обернуть все оставшиеся `UnityEditor` вызовы в `#if UNITY_EDITOR` / `#endif` для безопасности
- **0.1.5** 🔧 **Отладка:** Сделать Development Build → проверить отсутствие ошибок компиляции и загрузки ассетов

#### 0.2 Миграция шейдеров CGPROGRAM → URP HLSL
- **0.2.1** `TextureArrayShader.shader` → `URP_TextureArray.shader`: переписать на URP HLSL (`#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"`), сохранить логику Texture2DArray + vertex color lighting + alpha clip + per-face dimming
- **0.2.2** `BlockCracks.shader` → `URP_BlockCracks.shader`: URP transparent pass, Texture2DArray с `_Stage` float, `Offset -1, -1` для Z-fighting prevention
- **0.2.3** `ThickLines.shader` → `URP_ThickLines.shader`: geometry shader для wireframe выделения блока (проверить, поддерживает ли URP geometry stage; если нет — альтернатива через расширенный line mesh или screen-space outline)
- **0.2.4** `UI_TextureArrayShader.shader` → `URP_UI_TextureArray.shader`: Lambert lighting для 3D иконок в инвентаре, URP ForwardLit pass
- **0.2.5** Обновить все `.mat` файлы: `ChunkMaterial.mat` → назначить `URP_TextureArray`, `BlockCracksMat.mat` → назначить `URP_BlockCracks`
- **0.2.6** 🔧 **Отладка:** Запустить сцену → проверить рендеринг чанков (solid/cutout/transparent sub-meshes), трещины разрушения, wireframe выделения, 3D иконки в hotbar. Сравнить визуально с текущим видом

#### 0.3 Подключение Input System (InputActions)
- **0.3.1** Создать `Assets/Input/PlayerInputActions.inputactions` с Action Maps:
  - **Player**: `Move` (Vector2, WASD), `Look` (Vector2, Mouse Delta), `Jump` (Button, Space), `Sprint` (Button, LCtrl), `Sneak` (Button, LShift), `Attack` (Button, LMB), `UseItem` (Button, RMB), `DropItem` (Button, Q), `ToggleInventory` (Button, E), `Hotbar1-9` (Button, 1-9), `ScrollHotbar` (Value, Scroll Y), `ToggleDebug` (Button, F3), `TogglePerspective` (Button, F5), `OpenChat` (Button, T), `Pause` (Button, Esc)
  - **UI**: `Navigate` (Vector2), `Submit` (Button, Enter), `Cancel` (Button, Esc), `Point` (Vector2, Mouse Position), `Click` (Button, LMB), `RightClick` (Button, RMB), `MiddleClick` (Button, MMB), `ShiftClick` (Button, LShift+LMB)
  - **Spectator**: `Move` (Vector2, WASD), `Look` (Vector2, Mouse Delta), `Ascend` (Button, Space), `Descend` (Button, LShift), `SpeedBoost` (Button, LCtrl), `ExitSpectator` (Button, F3)
- **0.3.2** Сгенерировать C# класс `PlayerInputActions` (через Inspector → Generate C# Class)
- **0.3.3** Рефакторить `PlayerController.cs`: заменить все `Keyboard.current.wKey.isPressed` на `_inputActions.Player.Move.ReadValue<Vector2>()`, `Mouse.current.delta` → `_inputActions.Player.Look.ReadValue<Vector2>()` и т.д.
- **0.3.4** Рефакторить `PlayerInteraction.cs`: `Mouse.current.leftButton` → `_inputActions.Player.Attack`, `rightButton` → `_inputActions.Player.UseItem`
- **0.3.5** Рефакторить `PlayerInventory.cs`: `Mouse.current.scroll` → `_inputActions.Player.ScrollHotbar`, digit keys → `_inputActions.Player.Hotbar1` и т.д.
- **0.3.6** Рефакторить `SpectatorFly.cs` и `DebugCamera.cs`: подключить Action Map `Spectator`
- **0.3.7** Реализовать переключение Action Maps: при открытии UI → `Player.Disable()` + `UI.Enable()`, при закрытии → обратно
- **0.3.8** 🔧 **Отладка:** Проверить все биндинги: WASD движение, мышь камера, LMB/RMB взаимодействие, scroll/1-9 хотбар, F3 спектатор, Esc пауза. Убедиться что input не работает при открытом UI

#### 0.4 ServiceLocator / архитектурный рефакторинг
- **0.4.1** Создать `ServiceLocator.cs`: статический реестр сервисов (`Register<T>`, `Get<T>`) — замена прямых static singleton-ов
- **0.4.2** Зарегистрировать: `BlockDatabase`, `BiomeDatabase`, `ItemManager`, `UIManager`, `WorldManager` через ServiceLocator в `GameManager.cs`
- **0.4.3** Создать `GameManager.cs` — state machine: `enum GameState { MainMenu, Loading, Playing, Paused, Dead }` с событиями `OnStateChanged`
- **0.4.4** Заменить `Instance` обращения по всем скриптам на `ServiceLocator.Get<T>()` (или оставить `Instance` как shorthand, но инициализировать через ServiceLocator)
- **0.4.5** 🔧 **Отладка:** Проверить порядок инициализации — все сервисы должны быть доступны до первого обращения. Тест: удалить объект с `BlockDatabase` из сцены → должно быть внятное сообщение об ошибке, а не NullRef

#### 0.5 Addressables (опционально, отложить если не блокирует)
- **0.5.1** Установить пакет `com.unity.addressables`
- **0.5.2** Пометить `Textures/BlockTexturesArray.asset`, `Textures/CracksArray.asset`, все SO в `Resources/Blocks`, `Resources/Biomes` как Addressable
- **0.5.3** Заменить `Resources.Load` на `Addressables.LoadAssetAsync` с await
- **0.5.4** 🔧 **Отладка:** Проверить async загрузку — Loading Screen должен корректно показывать прогресс

---

### ═══════════════════════════════════════════════════════
### ФАЗА 1 — Система данных и Предметы
### ═══════════════════════════════════════════════════════

#### 1.1 Расширение BlockType и переход на ushort ID
- **1.1.1** Заменить `enum BlockType : byte` (макс 256) на `enum BlockType : ushort` (макс 65535) — все блоки MC 1.20
- **1.1.2** В `VoxelMap` (уже `NativeArray<ushort>`) изменить битовую упаковку: сейчас `blockID = data & 0x0FFF` (12 бит = 4096 блоков), `state = (data >> 12) & 0xF` (4 бит). Оценить достаточность 12 бит для ID (~800 блоков в MC 1.20 = хватает)
- **1.1.3** Добавить все блоки MC 1.20 в enum: все виды дерева (Oak/Birch/Spruce/Jungle/Acacia/Dark Oak/Cherry/Mangrove/Bamboo — Log, Planks, Leaves, Sapling, Slab, Stairs, Fence, Door, Trapdoor, Button, PressurePlate, Sign, WallSign, HangingSign), все руды, все камни, все цветы, все шерсти/стёкла/терракоты (16 цветов каждого), грибные блоки, кораллы, skulk, copper variants, bamboo variants, cherry blossom, все 1.20 Trail Ruins блоки, suspicious sand/gravel, decorated pots, sniffer egg, torchflower, pitcher plant
- **1.1.4** Обновить `BlockDatabase` → `NativeArray<BlockStruct>` с новым размером (увеличить до 1024 или 2048)
- **1.1.5** Обновить `BlockDatabaseGenerator.cs` — добавить текстурные маппинги для всех новых блоков
- **1.1.6** 🔧 **Отладка:** Пересоздать Texture2DArray → пересоздать Block SO → проверить что все индексы текстур корректны, нет чёрных/белых граней

#### 1.2 ItemData ScriptableObject
- **1.2.1** Создать `ItemData.cs` — базовый SO:
  ```
  ushort itemID
  string itemName
  string itemDescription
  Sprite icon
  int maxStackSize (1 для инструментов, 16 для яиц/жемчуга, 64 по умолчанию)
  ItemType type (Block, Tool, Weapon, Armor, Food, Material, Potion, SpawnEgg, Misc)
  ushort blockToPlace (для Block-типа, 0 если не блок)
  float attackDamage (для оружия/инструментов)
  float attackSpeed (MC 1.9+: sword=1.6, axe=0.8-1.0, pickaxe=1.2, shovel=1.0)
  ```
- **1.2.2** Создать `ToolData.cs : ItemData`:
  ```
  ToolType toolType (Pickaxe/Axe/Shovel/Hoe/Sword/Shears)
  ToolTier tier (Wood/Stone/Iron/Gold/Diamond/Netherite)
  int durability (Wood=59, Stone=131, Iron=250, Gold=32, Diamond=1561, Netherite=2031)
  float miningSpeedMultiplier (Wood=2, Stone=4, Iron=6, Gold=12, Diamond=8, Netherite=9)
  int harvestLevel (Wood=0, Stone=1, Iron=2, Diamond/Netherite=3)
  int enchantability (Wood=15, Stone=5, Iron=14, Gold=22, Diamond=10, Netherite=15)
  ```
- **1.2.3** Создать `FoodData.cs : ItemData`:
  ```
  int nutrition (Apple=4, Bread=5, CookedBeef=8, GoldenApple=4, GoldenCarrot=6)
  float saturationModifier (Apple=0.3, Bread=0.6, CookedBeef=0.8, GoldenApple=1.2, GoldenCarrot=1.2)
  float eatDuration (по умолчанию 1.61 секунды, мгновенно для Dried Kelp)
  bool canAlwaysEat (GoldenApple, Chorus Fruit — можно есть при полном голоде)
  StatusEffectInstance[] effectsOnEat (Rotten Flesh → Hunger 80%, Golden Apple → Absorption + Regen)
  ```
- **1.2.4** Создать `ArmorData.cs : ItemData`:
  ```
  ArmorSlot slot (Helmet/Chestplate/Leggings/Boots)
  int defensePoints (Leather Helmet=1, Iron Chestplate=6, Diamond Leggings=6, Netherite Boots=3)
  float toughness (Diamond=2, Netherite=3, остальные=0)
  float knockbackResistance (Netherite=0.1 за элемент, остальные=0)
  int durability (по слотам: Helmet*11, Chestplate*16, Leggings*15, Boots*13 от базы материала)
  ```
- **1.2.5** 🔧 **Отладка:** Создать вручную 5-10 тестовых SO предметов → проверить что все поля корректно сериализуются в Inspector

#### 1.3 ItemDatabase
- **1.3.1** Создать `ItemDatabase.cs` — MonoBehaviour singleton с `Dictionary<ushort, ItemData>` и `NativeHashMap<ushort, ItemStruct>` для Jobs
- **1.3.2** `ItemStruct` — blittable-версия для Burst: `ushort id, byte maxStack, byte type, float attackDamage, float attackSpeed`
- **1.3.3** Auto-загрузка из `Resources/Items/` через `Resources.LoadAll<ItemData>`
- **1.3.4** Auto-генерация Block-предметов: для каждого BlockData SO автоматически создавать ItemData с `blockToPlace = blockID`, `maxStackSize = 64`, `icon = сгенерированная иконка`
- **1.3.5** Создать `ItemDatabaseGenerator.cs` (Editor) — генерация SO предметов из MC wiki данных
- **1.3.6** 🔧 **Отладка:** Убедиться что `ItemDatabase.Get(id)` возвращает корректные данные, проверить что нет дубликатов ID

#### 1.4 Расширение BlockData SO
- **1.4.1** Добавить поля в `BlockData.cs`:
  ```
  byte lightEmission (0-15: torch=14, glowstone=15, redstone_ore_active=9, sea_lantern=15, lava=15, magma=3, crying_obsidian=10, sculk_catalyst=6)
  byte opacity (0-15: glass=0, water=1, leaves=1, solid=15)
  float blastResistance (dirt=0.5, stone=6, obsidian=1200, bedrock=3600000, end_portal_frame=3600000)
  bool isFlammable (wood=true, wool=true, leaves=true)
  int flammability (leaves=60, wool=30, planks=20, log=5) — шанс загореться
  int fireSpread (leaves=30, wool=60, planks=5, log=5) — шанс распространить огонь
  bool isGravityAffected (sand=true, gravel=true, anvil=true, dragon_egg=true, concrete_powder=true)
  bool isInteractable (crafting_table=true, furnace=true, chest=true, door=true, lever=true)
  int harvestLevel (0=hand, 1=wood, 2=stone, 3=iron, 4=diamond)
  ToolType requiredTool (stone→Pickaxe, dirt→Shovel, log→Axe)
  BlockShape shape (FullBlock, Slab, Stairs, Fence, Door, Trapdoor, ThinPane, Cross, Torch, Button, PressurePlate, Carpet, Sign, Wall, Chain, Lantern, Ladder)
  SoundCategory soundCategory (Wood, Stone, Grass, Sand, Gravel, Glass, Metal, Wool, Snow, Coral, Sculk, CherryWood, BambooWood, Nether, Deepslate, Amethyst, MudBrick, PackedMud)
  ```
- **1.4.2** Обновить `BlockStruct` (blittable для NativeArray): добавить `byte lightEmission`, `byte opacity`, `byte shape`
- **1.4.3** Обновить `BlockDatabaseGenerator.cs` — проставить все значения для всех 800+ блоков
- **1.4.4** 🔧 **Отладка:** Spot-check 20 случайных блоков — hardness, blastResistance, lightEmission, requiredTool должны совпадать с MC wiki

#### 1.5 Block States расширение
- **1.5.1** Текущая упаковка: `ushort = 12 бит ID + 4 бит state`. 4 бита (16 значений) недостаточно для всех состояний MC. Варианты:
  - **A) Перейти на uint32:** 16 бит ID + 16 бит state. Вдвое больше памяти (384 * 16 * 16 * 4 = ~1.5 MB на чанк вместо ~750 KB)
  - **B) Palette-based:** как MC Java — palette сжатие, хранить compact indices. Сложнее, но экономнее
  - **C) Оставить ushort + дополнительный NativeArray<byte> для extended state** — отдельный массив для блоков с > 16 состояниями
  - Рекомендация: **Вариант A (uint32)** — проще, надёжнее, 1.5 MB на чанк при 32 чанках = 48 MB — приемлемо
- **1.5.2** Определить состояния для ключевых блоков:
  - **Logs/Wood:** axis (X/Y/Z) — 2 бита
  - **Slabs:** type (Top/Bottom/Double) — 2 бита
  - **Stairs:** facing (N/S/E/W) + half (Top/Bottom) + shape (Straight/InnerLeft/InnerRight/OuterLeft/OuterRight) — 7 бит
  - **Doors:** facing (2 бит) + half (1 бит) + open (1 бит) + hinge (1 бит) + powered (1 бит) — 6 бит
  - **Trapdoors:** facing (2 бит) + half (1 бит) + open (1 бит) + powered (1 бит) + waterlogged (1 бит) — 6 бит
  - **Fences:** north/south/east/west connections (4 бита) + waterlogged (1 бит)
  - **Buttons:** facing (2 бит) + face (Floor/Wall/Ceiling, 2 бита) + powered (1 бит)
  - **Crops:** age (0-7) — 3 бита
  - **Redstone Wire:** power level (0-15) — 4 бита + connections (4×3 = 12 бит для none/side/up)
  - **Chest:** facing (2 бит) + type (Single/Left/Right, 2 бита) + waterlogged (1 бит)
  - **Furnace/Smoker/Blast Furnace:** facing (2 бит) + lit (1 бит)
  - **Water/Lava:** level (0-15) — 4 бита + falling (1 бит)
  - **Snow layer:** layers (1-8) — 3 бита
  - **Leaves:** distance (1-7) — 3 бита + persistent (1 бит) + waterlogged (1 бит)
- **1.5.3** Обновить `VoxelMap` тип с `NativeArray<ushort>` на `NativeArray<uint>` (или создать separate state array)
- **1.5.4** Обновить все Jobs (TerrainJob, ChunkMeshJob, LightUpdateJob) для работы с новым форматом
- **1.5.5** Обновить `ChunkMeshJob` — при генерации меша читать block state и выбирать правильную геометрию (slab = half-block, stairs = complex shape, fence = connected)
- **1.5.6** 🔧 **Отладка:** Разместить OakLog в 3 ориентациях → проверить что текстуры корректны. Разместить Slab → половина блока. Дверь → 2 блока высотой, открытие/закрытие

#### 1.6 Лут-таблицы (LootTable SO)
- **1.6.1** Создать `LootTable.cs` — SO:
  ```
  LootPool[] pools
  ── LootPool:
      int rolls (min/max)
      float bonusRollsPerLuck
      LootEntry[] entries
      LootCondition[] conditions
  ── LootEntry:
      ushort itemID
      int countMin, countMax
      float weight
      LootFunction[] functions
  ── LootFunction: (enum)
      SetCount, EnchantRandomly, EnchantWithLevels, FurnaceSmelt, ApplyBonus(Fortune/OreDrops), LootingEnchant, SetDamage, ExplorationMap, SetNBT
  ── LootCondition: (enum)
      SurvivesExplosion, MatchTool(SilkTouch), RandomChance, KilledByPlayer, EntityProperties
  ```
- **1.6.2** Реализовать ключевые лут-таблицы:
  - **Stone** → Cobblestone (SilkTouch → Stone)
  - **Coal Ore** → 1 Coal (Fortune I: 1-2, II: 1-3, III: 1-4. SilkTouch → Coal Ore). 0-2 XP
  - **Diamond Ore** → 1 Diamond (Fortune: 1-4. SilkTouch → Diamond Ore). 3-7 XP
  - **Lapis Ore** → 4-9 Lapis Lazuli (Fortune multiplied). 2-5 XP
  - **Redstone Ore** → 4-5 Redstone Dust (Fortune: +1 max per level). 1-5 XP
  - **Leaves** → Saplings (5% Oak/Birch/Cherry, 2.5% Jungle, 5% без Shears). Sticks (2%). Apple (0.5% for Oak/Dark Oak)
  - **Grass Block** → Dirt (SilkTouch → Grass Block)
  - **Glass** → Nothing (SilkTouch → Glass)
  - **Ice** → Nothing (SilkTouch → Ice). Spawns water if above solid
  - **Tall Grass** → Seeds (12.5%) (Fortune increases). Shears → Tall Grass
  - **Gravel** → Gravel (SilkTouch) или Flint (10%, Fortune: I=14%, II=25%, III=100%)
  - **Spawner** → Nothing + 15-43 XP
  - **Bookshelf** → 3 Books (SilkTouch → Bookshelf)
  - **Glowstone** → 2-4 Glowstone Dust (Fortune: max 4. SilkTouch → Glowstone)
  - **Melon** → 3-7 Melon Slices (Fortune: max 9. SilkTouch → Melon)
- **1.6.3** Интегрировать в `PlayerInteraction.cs`: при ломании блока → вызвать `LootTable.Roll(tool, fortune, silkTouch)` вместо прямого `dropItemBlockID`
- **1.6.4** 🔧 **Отладка:** Ломать Diamond Ore голой рукой (→ ничего, т.к. нужна Iron Pickaxe). Iron Pickaxe (→ 1 diamond). Fortune III (→ 1-4 diamonds). Silk Touch (→ Diamond Ore блок). Проверить XP дроп (когда будет XP система)

#### 1.7 Рецепты крафта (CraftingRecipe SO)
- **1.7.1** Создать `CraftingRecipe.cs` — SO:
  ```
  RecipeType type (Shaped/Shapeless)
  ItemStack[] pattern (3×3 = 9 слотов для Shaped, любой размер для Shapeless)
  int patternWidth, patternHeight (для Shaped)
  ItemStack result
  string group (для Recipe Book группировки)
  ```
- **1.7.2** Создать `SmeltingRecipe.cs` — SO:
  ```
  ItemStack input
  ItemStack output
  float experience (Iron=0.7, Gold=1.0, Diamond=1.0, CookedBeef=0.35, Charcoal=0.15)
  int cookingTime (Furnace=200 ticks, Smoker/BlastFurnace=100 ticks)
  SmeltingType type (Furnace/BlastFurnace/Smoker/Campfire)
  ```
- **1.7.3** Создать `SmithingRecipe.cs` — SO:
  ```
  ItemStack template (Netherite Upgrade Smithing Template / Armor Trim Template)
  ItemStack base (Diamond Tool/Armor)
  ItemStack addition (Netherite Ingot / Trim Material)
  ItemStack result
  ```
- **1.7.4** Реализовать **все базовые рецепты** MC 1.20 (через Editor-генератор):
  - Деревянные инструменты (5 видов × 8 типов дерева = 40 рецептов)
  - Каменные, железные, золотые, алмазные инструменты (5×4 = 20)
  - Все брони (4 слота × 6 материалов = 24)
  - Палки, доски, верстак, печь, сундук, кровать, лестницы, двери, люки, заборы, ворота, плиты, ступени
  - Стрелы, луки, арбалеты, щиты, удочки, ведра, компасы, часы, карты
  - Декоративные блоки (шерсть→ковёр, камень→кирпичи, медь→восковая медь)
  - Красный камень (факел, повторитель, компаратор, поршень, наблюдатель, раздатчик, воронка)
- **1.7.5** 🔧 **Отладка:** Загрузить все рецепты → проверить отсутствие дубликатов. Тест-кейс: 2×2 крафт досок из бревна (любое дерево → 4 доски своего типа)

---

### ═══════════════════════════════════════════════════════
### ФАЗА 2 — Полный инвентарь и Крафт
### ═══════════════════════════════════════════════════════

#### 2.1 Полный инвентарь (данные)
- **2.1.1** Расширить `PlayerInventory.cs`: `ItemStack[36] mainInventory` (27 основных + 9 hotbar), `ItemStack[4] armorSlots` (Helmet/Chestplate/Leggings/Boots), `ItemStack offhandSlot`, `ItemStack[4] craftingGrid` (2×2), `ItemStack craftingResult`
- **2.1.2** Реализовать `ItemStack` с полной информацией: `ushort itemID`, `byte count`, `short durability` (-1 = не имеет), `EnchantmentInstance[] enchantments`, `string customName`
- **2.1.3** Логика перемещения предметов:
  - **ЛКМ на слоте:** Взять стак целиком (если курсор пуст) / Положить стак (если курсор не пуст и слот пуст) / Обменять стаки (если разные предметы) / Объединить стаки (если одинаковые, до maxStack)
  - **ПКМ на слоте:** Положить 1 предмет из курсора / Взять половину стака
  - **Shift+ЛКМ:** Быстрое перемещение: из хотбара в основной, из основного в хотбар, из контейнера в инвентарь
  - **Числа 1-9 с hover:** Обмен с хотбар-слотом
  - **Q (на hover):** Выбросить 1 предмет. Ctrl+Q — выбросить весь стак
  - **Двойной ЛКМ:** Собрать все одинаковые предметы из открытых слотов в один стак
  - **Перетаскивание с зажатым ЛКМ:** Равномерно распределить по пустым слотам
  - **Перетаскивание с зажатым ПКМ:** Положить по 1 предмету в каждый слот
- **2.1.4** Слоты брони: принимают только `ArmorData` соответствующего `ArmorSlot`. Визуальное отображение protection points
- **2.1.5** Offhand: принимают любой предмет. Отображение в HUD слева от хотбара (зеркальная рамка)
- **2.1.6** 🔧 **Отладка:** Юнит-тесты перемещения: взять-положить, split, merge, shift-click, double-click, drag-distribute. Проверить edge-case: стак 63 + стак 63 одного предмета → один 64 + один 62

#### 2.2 UI инвентаря (Canvas-based)
- **2.2.1** Создать `SlotUI.cs` — reusable компонент:
  - RawImage для фона слота (из `inventory.png` atlas)
  - RawImage для иконки предмета (3D рендер или sprite)
  - TextMeshPro для количества (шрифт MinecraftDefault, тень +1px)
  - TextMeshPro для durability bar (зелёный→жёлтый→красный, как в MC)
  - EventTrigger: OnPointerDown (LMB/RMB), OnPointerEnter (hover → tooltip), OnDrag (drag & drop)
  - Enchantment glint effect (фиолетовый shimmer overlay для зачарованных предметов)
- **2.2.2** Создать `InventoryUI.cs`:
  - Canvas overlay, фон затемнение (50% чёрный)
  - Текстура фона инвентаря из `gui/container/inventory.png` (176×166 пикселей, масштаб ×3)
  - 27 SlotUI для основного инвентаря (3 ряда × 9)
  - 9 SlotUI для хотбара (нижний ряд)
  - 4 SlotUI для крафта 2×2 (правый верхний угол)
  - 1 SlotUI для результата крафта (стрелка →)
  - 4 SlotUI для брони (левая колонка, с ghost-иконками шлема/кирасы/поножей/ботинок)
  - 1 SlotUI для offhand (правая от брони)
  - Рендер модели игрока в окне (повторяет MC: поворот по курсору мыши)
  - Переключение на `UI` Action Map при открытии (E), обратно при закрытии
  - Cursor unlock → Cursor.lockState = CursorLockMode.None при открытии
- **2.2.3** Создать `CursorItem` — предмет «на курсоре»:
  - Следует за позицией мыши
  - RawImage + TextMeshPro количество
  - Привязан к `InventoryUI`, обнуляется при закрытии инвентаря (предмет возвращается)
- **2.2.4** Создать `TooltipUI.cs`:
  - Появляется при hover над слотом с предметом
  - Показывает: имя предмета (белый, для Rare — голубой, для Epic — фиолетовый), enchantments (серый), lore (фиолетовый), durability (если инструмент)
  - Позиционируется справа-снизу от курсора, не выходит за экран
- **2.2.5** 🔧 **Отладка:** Открыть инвентарь (E) → перемещение предметов всеми способами. Проверить что предметы не дублируются и не теряются. Tooltip корректный. При закрытии — предмет на курсоре возвращается в инвентарь

#### 2.3 Крафт 2×2 (в инвентаре)
- **2.3.1** Создать `CraftingSystem.cs`:
  - `TryMatchRecipe(ItemStack[] grid, int width, int height)` → `CraftingRecipe?`
  - **Shaped matching:** проверить все смещения (сдвиг рецепта внутри сетки), отзеркаленные варианты
  - **Shapeless matching:** проверить наличие всех ингредиентов в любом порядке
  - Кэширование результата: пересчёт только при изменении сетки
- **2.3.2** Подключить к `InventoryUI`: при изменении крафт-сетки 2×2 → `CraftingSystem.TryMatchRecipe` → показать результат в output-слоте
- **2.3.3** При взятии результата: уменьшить каждый ингредиент на 1 (или заменить: ведро молока → пустое ведро)
- **2.3.4** При Shift+Click на результате: скрафтить максимальное количество (до заполнения инвентаря или исчерпания ингредиентов)
- **2.3.5** Рецепты 2×2: Доски (1 бревно → 4 доски), Палки (2 доски → 4 палки), Верстак (4 доски → 1 верстак), кнопки, плиты малые
- **2.3.6** 🔧 **Отладка:** Положить бревно в 2×2 → появляются 4 доски. Shift+click → крафтить все доски сразу. Положить 2 доски вертикально → 4 палки

#### 2.4 Верстак (Crafting Table)
- **2.4.1** Блок Crafting Table: `blockID`, текстура top/front/side, `isInteractable = true`, `hardness = 2.5`, `bestTool = Axe`
- **2.4.2** Правый клик на верстаке → открыть `CraftingTableUI.cs`:
  - Текстура из `gui/container/crafting_table.png`
  - Крафт-сетка 3×3 (9 SlotUI)
  - Output SlotUI
  - 27+9 слотов инвентаря (нижняя часть)
- **2.4.3** Логика крафта 3×3: аналогична 2×2, но с поддержкой больших рецептов (кирка, меч, сундук, печь, и т.д.)
- **2.4.4** При закрытии верстака: предметы из крафт-сетки выбрасываются (drop) или возвращаются в инвентарь
- **2.4.5** 🔧 **Отладка:** Скрафтить деревянную кирку (2 палки + 3 доски в Т-форме). Скрафтить железный меч. Shift+click на всё. Закрыть верстак с предметами в сетке → предметы падают

#### 2.5 Печь (Furnace)
- **2.5.1** Блок Furnace: текстура front/front_on/side/top, `isInteractable = true`, facing state (N/S/E/W), lit state (on/off → меняет текстуру и light emission)
- **2.5.2** `FurnaceBlockEntity` — данные привязанные к позиции блока:
  ```
  ItemStack inputSlot
  ItemStack fuelSlot
  ItemStack outputSlot
  float burnTimeRemaining (тики оставшегося горения топлива)
  float totalBurnTime (полное время горения текущего топлива — для прогресс-бара огня)
  float cookProgress (0..200 тиков = 10 секунд для Furnace)
  float totalCookTime (200 для Furnace, 100 для Blast Furnace / Smoker)
  ```
- **2.5.3** `FurnaceSystem.cs` — тиковая логика (каждый игровой тик = 50ms):
  - Если есть топливо, есть input, есть подходящий рецепт, и output пуст или совместим → начать плавку
  - `cookProgress++` каждый тик пока горит топливо
  - При `cookProgress >= totalCookTime` → переместить результат в output, уменьшить input на 1
  - Топливо: Coal = 1600 тиков (8 items), Wood Planks = 300 тиков (1.5 items), Stick = 100 тиков (0.5 items), Lava Bucket = 20000 тиков (100 items), Blaze Rod = 2400 тиков (12 items), Dried Kelp Block = 4001 тиков (20 items + сам себя)
  - XP: накапливается в печи, выпадает при извлечении output
- **2.5.4** `FurnaceUI.cs`:
  - Текстура из `gui/container/furnace.png`
  - 3 SlotUI (input/fuel/output)
  - Анимация огня (sprite animation 13 кадров, из `widgets.png`)
  - Прогресс-бар стрелки (sprite animation, заполняется слева направо)
  - 27+9 слотов инвентаря
- **2.5.5** Печь продолжает работать когда UI закрыт (через `BlockUpdateManager`)
- **2.5.6** Варианты: Smoker (только еда, в 2 раза быстрее), Blast Furnace (только руда/броня, в 2 раза быстрее). Campfire (300 тиков, 4 слота, без топлива, только еда)
- **2.5.7** 🔧 **Отладка:** Положить Iron Ore + Coal → железный слиток через 10 сек. Проверить огонь и стрелку. Закрыть UI → печь продолжает работать. Извлечь результат → XP orbs. Smoker: мясо за 5 сек

#### 2.6 Сундук (Chest)
- **2.6.1** Блок Chest: facing state, модель с крышкой (не full-block, 14×14×14 пикселей с приподнятой крышкой)
- **2.6.2** `ChestBlockEntity`: `ItemStack[27] contents`, `bool isOpen` (для анимации крышки и звука)
- **2.6.3** Double Chest: два соседних сундука (одного типа, одного facing) объединяются в 54 слота. `ChestType` state: Single/Left/Right. При установке рядом → автоматически менять state обоих
- **2.6.4** `ChestUI.cs`:
  - Текстура из `gui/container/generic_54.png` (для double) или `gui/container/shulker_box.png` (для single = 27)
  - Заголовок "Chest" / "Large Chest"
  - 27/54 слотов контейнера + 27+9 инвентаря
  - Shift+click: из сундука в инвентарь, из инвентаря в сундук
- **2.6.5** Ender Chest: 27 слотов, содержимое привязано к игроку (не к позиции), одинаковое для всех Ender Chest-ов
- **2.6.6** Trapped Chest: аналогичен Chest, но при открытии генерирует redstone signal (15 - пропорционально количеству игроков)
- **2.6.7** Barrel: аналогичен Chest (27 слотов), но может открываться даже если сверху блок. Другая текстура/звук
- **2.6.8** Shulker Box (16 цветов): 27 слотов, при ломании сохраняет содержимое как NBT в предмете. При установке — восстанавливает. Нельзя класть Shulker Box в Shulker Box
- **2.6.9** 🔧 **Отладка:** Положить предмет в сундук → закрыть → выйти из чанка → вернуться → предмет на месте. Double chest: поставить 2 → 54 слота. Сломать одну половину → содержимое выпадает, другая становится single

#### 2.7 Креативный инвентарь
- **2.7.1** `CreativeInventoryUI.cs`:
  - 5 вкладок: Building Blocks, Decoration Blocks, Redstone, Transportation, Miscellaneous (+ Search, + Survival inventory)
  - Текстура из `gui/container/creative_inventory/`
  - Scrollbar для контента (может быть сотни предметов в табе)
  - ЛКМ на предмете → получить полный стак (64 или maxStack)
  - ПКМ → получить 1
  - Поиск (вкладка Search): фильтр по имени предмета, обновление в реальном времени
  - Удаление предметов: drag предмет за пределы инвентаря → удалить (не drop, а именно delete)
- **2.7.2** Режим Creative: `PlayerController` → бесконечный полёт (двойной Space для взлёта, Shift для спуска), бессмертие, мгновенное разрушение блоков
- **2.7.3** Переключение `/gamemode creative` и `/gamemode survival`
- **2.7.4** 🔧 **Отладка:** Переключить в Creative → двойной Space → полёт. Открыть креатив-инвентарь → все предметы отображаются. Поиск "diamond" → фильтрация работает. Infinite stacks

---

### ═══════════════════════════════════════════════════════
### ФАЗА 3 — Генерация мира 1.20
### ═══════════════════════════════════════════════════════

#### 3.1 Расширение биомов до ~60
- **3.1.1** Расширить `BiomeData.cs`:
  ```
  Color foliageColor (для leaves и vine tinting)
  Color grassColor (для grass_block top и tall_grass)
  Color waterColor (по биому: Ocean=#3F76E4, Swamp=#617B64, Warm Ocean=#43D5EE)
  Color waterFogColor
  Color fogColor
  Color skyColor
  float temperature (для визуального: снег, лёд)
  float downfall (осадки)
  TreeType[] trees (типы деревьев и их частота)
  StructureType[] structures (деревни, подземелья, и т.д.)
  DecorationType[] decorations (цветы, трава, грибы)
  MobSpawnEntry[] spawns (какие мобы и с какой частотой)
  AmbientSound[] ambientSounds
  MusicTrack[] musicTracks
  float baseHeight (для surface rule fine-tuning)
  ```
- **3.1.2** Все биомы Overworld MC 1.20 (с target noise точками):
  - **Температурные:** Ice Spikes, Frozen River, Snowy Plains, Snowy Taiga, Snowy Beach, Grove, Frozen Peaks, Jagged Peaks, Stony Peaks
  - **Умеренные:** Plains ✅, Sunflower Plains, Flower Forest, Forest, Birch Forest, Old Growth Birch Forest, Dark Forest, Taiga, Old Growth Spruce Taiga, Old Growth Pine Taiga, Meadow, Cherry Grove, Windswept Hills, Windswept Gravelly Hills, Windswept Forest, Stony Shore
  - **Тёплые:** Desert ✅, Savanna, Savanna Plateau, Windswept Savanna, Jungle ✅, Sparse Jungle, Bamboo Jungle, Badlands ✅, Eroded Badlands, Wooded Badlands
  - **Влажные:** Swamp, Mangrove Swamp, River, Beach, Mushroom Fields
  - **Водные:** Ocean ✅, Deep Ocean, Warm Ocean, Lukewarm Ocean, Cold Ocean, Deep Lukewarm Ocean, Deep Cold Ocean, Deep Frozen Ocean, Frozen Ocean
  - **Подземные:** Lush Caves ✅, Dripstone Caves ✅, Deep Dark
- **3.1.3** 🔧 **Отладка:** F3 экран → отображение текущего биома. Перемещаться по миру → биомы меняются плавно. Визуальная проверка: пустыня не граничит напрямую со снежным биомом (continentalness/temperature должны плавно переходить)

#### 3.2 Интерполяция биомов
- **3.2.1** Заменить «ближайший биом» на взвешенную интерполяцию:
  - На каждый столбец XZ → рассчитать 3 ближайших биома с весами (Inverse Distance Weighting)
  - Интерполировать `surfaceBlock`, `foliageColor`, `grassColor`, `waterColor`
  - Для блоков: Voronoi-based — каждый блок принадлежит одному биому, но границы сглажены
  - Для цветов: vertex color blending в шейдере (передать biome color per-vertex)
- **3.2.2** Biome edge smoothing: если 2 биома граничат, создать переходную зону шириной 4-8 блоков с случайным выбором блоков обоих биомов
- **3.2.3** 🔧 **Отладка:** Граница Plains/Desert → плавный переход Grass→Dirt→Sand. Граница Forest/Plains → деревья постепенно разрежаются. Нет резких линий

#### 3.3 Деревья
- **3.3.1** Создать `StructureData.cs` — SO для хранения структур:
  ```
  int3 size
  int3 origin (точка «ствола» / центр)
  StructureBlock[] blocks:
    int3 localPosition
    ushort blockID
    uint blockState
    PlacementRule rule (Replace/KeepExisting/ReplaceAir)
  ```
- **3.3.2** Реализовать все типы деревьев:
  - **Oak:** Small (4-6 tall, round canopy 5×5×3) + Large (6-14 tall, branches, bigger canopy). Chance of bee nest (5%)
  - **Birch:** 5-7 tall, narrow canopy (3×3×3 top). Tall Birch в Old Growth: 10-14 tall
  - **Spruce:** 7-11 tall, конусовидная крона. Mega Spruce: 2×2 trunk, 20-30 tall (Old Growth Taiga)
  - **Jungle:** Small (4-7 tall) + Mega (2×2 trunk, 15-30 tall, vines, cocoa beans). Shrub (1 tall)
  - **Acacia:** 6-8 tall, наклонный ствол, плоская крона смещённая
  - **Dark Oak:** 2×2 trunk, 6-8 tall, thick canopy, mushrooms underneath
  - **Cherry:** 4-7 tall, pink petals particles, round canopy, petal-covered ground
  - **Mangrove:** 3-7 tall, propagules hanging, root blocks в воде, moss carpet
  - **Azalea:** 3-5 tall, flowering leaves, rooted dirt underneath
  - **Mushroom (Huge):** Brown (flat top) и Red (round top), 5-13 tall
- **3.3.3** `StructureGenerator.cs` — placement logic:
  - Во время TerrainJob помечать позиции деревьев (noise-based + random per biome)
  - После генерации terrain → отдельный pass для placement структур (может затрагивать соседние чанки → cross-chunk queue)
  - Деревья не ставятся на: воду (кроме Mangrove), песок (кроме Palm/Acacia), скалу, другие деревья
  - Минимальное расстояние между деревьями: 2-4 блока
- **3.3.4** 🔧 **Отладка:** Plains → разреженные Oak. Forest → плотный Oak/Birch. Taiga → Spruce. Jungle → Mega trees с лианами. Cherry Grove → розовые деревья с частицами

#### 3.4 Руды по высотам (Triangular Distribution)
- **3.4.1** Реализовать в `TerrainJob` или отдельном `OreGenerationJob`:
  - **Coal:** Y=0..320, пик Y=96, 20 вейнов/чанк по 17 блоков + 20 вейнов/чанк по 17 блоков above Y=136
  - **Iron:** Y=-64..72, пик Y=16, 20 вейнов/чанк по 9 блоков + Y=80..384 uniform 10 вейнов по 9 блоков
  - **Copper:** Y=-16..112, пик Y=48, 16 вейнов/чанк по 10 блоков (Dripstone Caves: ×2)
  - **Gold:** Y=-64..32, пик Y=-16, 4 вейна/чанк по 9 блоков + Badlands Y=32..256 extra
  - **Lapis:** Y=-64..64, пик Y=0, 2 вейна/чанк по 7 блоков (uniform) + 4 вейна buried
  - **Redstone:** Y=-64..15, пик Y=-59, 4 вейна/чанк по 8 блоков
  - **Diamond:** Y=-64..16, пик Y=-59, 1 вейн/чанк по 4 блока (reduced air exposure). Below Y=-58: дополнительные
  - **Emerald:** Y=-16..320, пик Y=236, Mountains only, 1 блок per vein, ~3-8 per chunk in Mountains
  - **Nether Gold Ore:** Y=10..117, 10 вейнов/чанк по 10 блоков
  - **Ancient Debris:** Y=8..22, пик Y=15, 1 вейн по 1-3 блока + Y=8..119 1 вейн по 1-2 блока
- **3.4.2** Deepslate variant: ниже Y=0 Stone заменяется на Deepslate, все руды → Deepslate variants
- **3.4.3** Vein generation: blob-shape через noise-based sphere с random radius
- **3.4.4** Amethyst Geodes: сферические структуры из Smooth Basalt (outer) → Calcite (middle) → Amethyst Block (inner) + Budding Amethyst, Y=-58..30, ~1 per 24 chunks
- **3.4.5** 🔧 **Отладка:** Создать мир с seed=0 → копать на разных высотах → проверить: Diamond только ниже Y=16, Coal обильно наверху, Iron максимум около Y=16. Deepslate ниже Y=0

#### 3.5 Декорации
- **3.5.1** `DecorationJob.cs` (IJob, Burst) — после terrain + structures:
  - **Трава/Цветы:** Tall Grass, Short Grass (Plains: 70% покрытие), Fern (Taiga: 50%), Flowers: Poppy, Dandelion, Blue Orchid (Swamp), Allium, Azure Bluet, Tulips (4 цвета), Oxeye Daisy, Cornflower, Lily of the Valley, Wither Rose (только при убийстве мобов Wither-ом), Torchflower (Trail Ruins), Pitcher Plant
  - **Кактусы:** Desert/Badlands, 1-3 блока в высоту, только на Sand, не рядом с другими блоками
  - **Сахарный тростник:** рядом с водой, на Sand/Dirt/Grass, 1-4 блока высоты
  - **Тыквы/Арбузы:** редкие, Plains/Forest, на Grass Block
  - **Грибы:** Dark Forest, Mushroom Fields, caves (light level < 12)
  - **Bamboo:** Jungle/Bamboo Jungle, 12-16 блоков высоты, dense clusters
  - **Lava/Water pools:** Surface pools (Plains, Desert), Underground pools
  - **Moss carpet, Azalea, Dripleaf:** Lush Caves
  - **Pointed Dripstone:** Dripstone Caves, сталактиты (сверху) и сталагмиты (снизу), 1-7 блоков
  - **Sculk/Sculk Veins/Sculk Catalyst/Sculk Shrieker/Sculk Sensor:** Deep Dark (Y=-64..-1, below Erosion -0.7)
  - **Coral:** Warm Ocean, все 5 цветов + мёртвые варианты
  - **Kelp:** Ocean, 2-25 блоков высоты
  - **Sea Grass:** Ocean floor
  - **Lily Pad:** Swamp, на поверхности воды
  - **Vine:** Jungle, Swamp, Lush Caves — на стенах и потолках
  - **Glow Lichen:** Caves, на стенах, light level 7
  - **Snow layer:** Snowy biomes, 1 слой на Grass/Stone выше Y=95
- **3.5.2** Cross-biome decoration rules: декорации не spawn-ятся за пределы своего биома (проверка biome at decoration position)
- **3.5.3** 🔧 **Отладка:** Plains → трава + цветы. Jungle → бамбук + лианы. Lush Caves → dripleaf + azalea + glow berries. Warm Ocean → кораллы

#### 3.6 Подземные структуры
- **3.6.1** **Mineshaft:** Y=0..60, деревянные столбы (Fence + Planks), рельсы, Chest Minecart (лут-таблица), Cave Spider spawner
- **3.6.2** **Dungeon (Monster Room):** 5×5×5 или 7×7×7 кобблстоун, 1-2 сундука, 1 Spawner (Zombie 50%, Skeleton 25%, Spider 25%)
- **3.6.3** **Stronghold:** 3 per world, расположение кольцом (1408-2688 блоков от спавна). End Portal Room + Library + Staircase + Corridor + Prison + Fountain
- **3.6.4** **Ocean Monument:** Deep Ocean, Guardian spawns, Sponge rooms, Gold blocks, Elder Guardian (3 per monument)
- **3.6.5** **Ancient City:** Deep Dark (Y=-52), Sculk-covered, Redstone puzzles, Enchanted Golden Apple chest loot, Warden spawn trigger (Sculk Shrieker)
- **3.6.6** **Trail Ruins:** Desert/Taiga/Jungle/Old Growth, partially buried, Suspicious Sand/Gravel with loot (archaeology brushing)
- **3.6.7** 🔧 **Отладка:** Seed exploration → найти каждый тип структуры → проверить layout, loot, spawners

#### 3.7 Надземные структуры
- **3.7.1** **Village:** 5 типов (Plains/Desert/Savanna/Taiga/Snowy). Дома, кузница, церковь, колодец, грядки. Villager spawns по 2 на дом
- **3.7.2** **Pillager Outpost:** около Village, деревянная башня, Pillager spawns
- **3.7.3** **Desert Pyramid:** Sandstone структура, 4 сундука в TNT-ловушке, Husks
- **3.7.4** **Jungle Temple:** Cobblestone/Mossy, 2 сундука, стрелковая ловушка (Dispenser + Tripwire)
- **3.7.5** **Witch Hut:** Swamp, Witch spawn, Cauldron + Flower Pot
- **3.7.6** **Ruined Portal:** Overworld + Nether, частично разрушенный портал, Gold Block, сундук
- **3.7.7** **Shipwreck:** Ocean, деревянный корабль (3 варианта: complete, missing stern, missing bow), 1-3 сундука
- **3.7.8** **Ocean Ruins:** Cold/Warm, Stone/Sandstone, 1 сундук, Drowned spawns
- **3.7.9** **Igloo:** Snowy, 50% с подвалом (Zombie Villager + Golden Apple + Weakness Potion = cure puzzle)
- **3.7.10** 🔧 **Отладка:** Найти Village → villagers + торговля. Desert Pyramid → TNT trap работает

#### 3.8 Nether dimension
- **3.8.1** `DimensionManager.cs`: управление переключением Overworld↔Nether↔End
  - Портал Nether: 4×5 обсидиан рамка, зажигание Flint and Steel, фиолетовые частицы
  - Координатное преобразование: Nether X/Z = Overworld X/Z ÷ 8
  - При входе в портал: 4 секунды ожидания (отменяется движением), поиск/создание paired портала
- **3.8.2** Nether terrain generation (отдельный `NetherTerrainJob.cs`):
  - Y=0..128 (Bedrock floor Y=0..4, Bedrock ceiling Y=124..128)
  - Lava sea: Y=31
  - Netherrack заполняет всё, 3D noise для пещер
  - Basalt pillars: Soul Sand Valley
  - Glowstone blobs: потолок, 10 per chunk
  - Nether Quartz Ore: Y=10..117
  - Magma blocks: около лавы
  - Soul Fire: Soul Sand Valley
- **3.8.3** Nether биомы (5):
  - **Nether Wastes:** Netherrack, лавовые озёра, Zombie Piglin, Ghast, Magma Cube, Nether Fortress
  - **Crimson Forest:** Crimson Nylium, Huge Crimson Fungi, Weeping Vines, Hoglin, Piglin
  - **Warped Forest:** Warped Nylium, Huge Warped Fungi, Twisting Vines, Enderman (единственный моб)
  - **Soul Sand Valley:** Soul Sand, Soul Soil, Basalt Pillars, Skeleton, Ghast (много)
  - **Basalt Deltas:** Basalt, Blackstone, Magma Cube (много), Ghast
- **3.8.4** Nether структуры:
  - **Nether Fortress:** Blaze spawner, Wither Skeleton spawns, Nether Wart, сундуки
  - **Bastion Remnant:** Piglin Brute, Gold blocks, сундуки (Pigstep music disc, Snout Banner Pattern)
- **3.8.5** 🔧 **Отладка:** Создать портал → войти → Nether генерируется. Координаты ÷8. Найти Nether Fortress. Вернуться через портал → правильная точка

#### 3.9 The End dimension
- **3.9.1** End terrain generation (`EndTerrainJob.cs`):
  - Central island: 100-блочный радиус, Y=0..75, End Stone
  - Obsidian pillars: 10, случайные высоты 76-103, Iron Bars cage на некоторых
  - End Fountain: Bedrock + Torch, активируется после убийства Dragon
  - Outer islands: 1000+ блоков от центра, floating End Stone islands
- **3.9.2** End Gateway: появляется после убийства Dragon, телепортирует на outer islands
- **3.9.3** End Cities: outer islands, Shulker mobs, End Ship (Elytra, Dragon Head), Purpur blocks
- **3.9.4** Chorus Plants: outer islands, растут на End Stone, Chorus Fruit (teleport при поедании)
- **3.9.5** 🔧 **Отладка:** Enter End → Dragon fight arena. Kill Dragon → credits. Outer islands → End City + Elytra

#### 3.10 Aquifer system (подземные водоёмы)
- **3.10.1** Noise-based aquifer level: каждая XZ колонка имеет «уровень водоносного слоя» определённый noise-ом
- **3.10.2** Если Y ниже aquifer level и блок = воздух внутри пещеры → заполнить водой (или лавой ниже Y=0)
- **3.10.3** Лавовые озёра: отдельные noise-пулы ниже Y=0
- **3.10.4** 🔧 **Отладка:** Копать глубоко → обнаружить подземные озёра. Ниже Y=0 → лавовые озёра

#### 3.11 Chunk serialization (сохранение мира)
- **3.11.1** `ChunkSerializer.cs`:
  - Формат: binary, per-chunk файл или Region file (32×32 chunks per file, как MC)
  - Данные: VoxelMap (uint[98304]), modified flag, block entity data (furnace contents, chest contents, sign text)
  - Compression: RLE или LZ4 (большинство чанков — 90%+ воздух)
  - Путь: `Application.persistentDataPath/worlds/{worldName}/region/r.{rx}.{rz}.dat`
- **3.11.2** `SaveManager.cs`:
  - `SaveChunk(int2 coord)` — вызывается при unload модифицированного чанка
  - `LoadChunk(int2 coord)` — вызывается вместо TerrainJob если файл существует
  - `SaveWorld()` — save all modified chunks + world metadata (seed, player position, time, gamemode)
  - Auto-save каждые 5 минут
  - Корректный shutdown: OnApplicationQuit → SaveWorld
- **3.11.3** World metadata: `world.dat` — seed, playerPos, playerRotation, gameTime, gameMode, difficulty, spawnPoint
- **3.11.4** Block Entity storage: Dictionary<int3, BlockEntityData> → сериализуется вместе с чанком
- **3.11.5** 🔧 **Отладка:** Модифицировать чанк (поставить/сломать блок) → выйти → перезапустить → изменения сохранены. Сундук с предметами → сохраняется. Печь в процессе плавки → состояние сохраняется

---

### ═══════════════════════════════════════════════════════
### ФАЗА 4 — Мобы и AI
### ═══════════════════════════════════════════════════════

#### 4.1 Entity System (базовый)
- **4.1.1** `Entity.cs` — абстрактный базовый класс:
  ```
  System.Guid uuid
  Vector3 position, velocity
  float yaw, pitch (body rotation)
  AABB boundingBox (width, height)
  bool isOnGround, isInWater, isInLava
  bool isDead, bool isRemoved
  int fireTicks (если > 0, горит, -1 tick/sec, damage каждые 20 ticks)
  int noAirTicks (drowning: > 300 ticks = damage каждые 20 ticks)
  float fallDistance (damage = fallDistance - 3, min 0)
  ```
  Методы: `Tick()`, `Move(Vector3 motion)` с AABB воксельной коллизией (как у Player), `Hurt(float damage, DamageSource)`, `Kill()`
- **4.1.2** `LivingEntity.cs : Entity`:
  ```
  float health, maxHealth
  float absorptionAmount
  int hurtTime (красная вспышка, 10 тиков)
  int deathTime (анимация смерти, 20 тиков → remove)
  int invulnerabilityTicks (0.5 сек после получения урона)
  StatusEffect[] activeEffects
  ItemStack mainHandItem, offHandItem
  ItemStack[4] armorItems
  ```
  Методы: `ApplyDamage()` с учётом брони/enchantments/resistance, `Heal()`, `OnDeath()` → loot drop + XP
- **4.1.3** `MobEntity.cs : LivingEntity`:
  ```
  MobAI ai (goal selector)
  LivingEntity target
  float attackDamage
  float moveSpeed
  float knockbackResistance
  bool isPersistent (name tag → не despawn-ится)
  int despawnTimer (если > 600 тиков без игрока в 32 блоках → 1/800 шанс despawn per tick. > 128 блоков → instant despawn)
  MobCategory category (Passive/Hostile/Ambient/WaterCreature/WaterAmbient/UndergroundWaterCreature)
  ```
- **4.1.4** 🔧 **Отладка:** Создать тестовый Zombie entity → движение, коллизия с блоками, падение, урон от падения, смерть + лут

#### 4.2 AI система (Goal-based)
- **4.2.1** `MobAI.cs` — Goal Selector (приоритетная очередь):
  ```
  List<(int priority, AIGoal goal)> goals
  AIGoal activeGoal
  Tick():
    Для каждого goal по приоритету:
      Если activeGoal != null && activeGoal.priority > goal.priority: continue
      Если goal.CanStart(): activeGoal = goal; goal.Start()
    Если activeGoal != null: activeGoal.Tick()
    Если activeGoal.IsFinished(): activeGoal.Stop(); activeGoal = null
  ```
- **4.2.2** Target Selector (отдельный от Goal Selector):
  ```
  NearestAttackableTargetGoal(targetType, distance, mustSee, mustReach)
  HurtByTargetGoal(alertOthers: bool) — мстит обидчику
  DefendVillageTargetGoal (Iron Golem)
  ```
- **4.2.3** Базовые Goals:
  - **FloatGoal** (priority=0): если в воде → random jump для дыхания
  - **PanicGoal** (priority=1, passive mobs): если hurt → бежать прочь, speed ×1.25
  - **BreedGoal** (priority=2): если 2 животных одного вида с "love mode" → сближаться → spawn baby
  - **TemptGoal** (priority=3): следовать за игроком с определённым предметом (Pig → Carrot, Cow → Wheat)
  - **FollowParentGoal** (priority=4): baby → следовать за ближайшим взрослым того же вида
  - **MeleeAttackGoal** (priority=2, hostile): идти к target → при distance < 2 → attack каждые 20 тиков
  - **RangedAttackGoal** (priority=2, Skeleton): идти к target до distance 15 → стрелять Arrow каждые 20-60 тиков (зависит от difficulty)
  - **WanderGoal** (priority=5): каждые 120 тиков → random position в радиусе 10 → идти
  - **LookAtPlayerGoal** (priority=6): если игрок в 8 блоках → повернуть голову
  - **RandomLookAroundGoal** (priority=7): случайные повороты головы
  - **AvoidEntityGoal** (priority=1, Cat→Creeper, Rabbit→Fox): убегать от определённого типа
  - **SwellGoal** (priority=1, Creeper): если target в 3 блоках → начать свечение (30 тиков → explode)
  - **SpiderAttackGoal** (priority=1): днём → нейтральный, ночью → aggressive
  - **EndermanRandomStrollGoal**: телепортация при получении урона, ярость при взгляде
- **4.2.4** `PathfindingJob.cs` (IJob, Burst): A* на воксельной сетке
  - Учёт: isSolid (стены), isWater (плавание), height > 1.8 (не влезть), падение > 3 блоков (избегать)
  - Max distance: 32 блока (дальше → дорожки бессмысленны)
  - Результат: NativeList<int3> waypoints
  - Сглаживание пути: удаление промежуточных waypoints на прямой линии
- **4.2.5** 🔧 **Отладка:** Zombie → идёт к игроку → атакует → knockback. Pig → wander, panic при ударе. Creeper → подходит → свечение → взрыв. Skeleton → стреляет на расстоянии

#### 4.3 Passive Mobs
- **4.3.1** **Pig:** 10 HP, drop: 1-3 Raw Porkchop (Cooked если горел), XP: 1-3, saddleable, Carrot on a Stick управление
- **4.3.2** **Cow:** 10 HP, drop: 1-3 Raw Beef + 0-2 Leather, Right-click с Bucket → Milk Bucket
- **4.3.3** **Sheep:** 8 HP, drop: 1 Wool (цвет), Right-click с Shears → 1-3 Wool + Sheep без шерсти, шерсть отрастает при поедании Grass Block (Grass → Dirt), 16 цветов + natural color distribution (81.8% white, 5% black, 5% gray, 5% light gray, 3% brown, 0.2% pink)
- **4.3.4** **Chicken:** 4 HP, drop: 1 Raw Chicken + 0-2 Feather, lays Egg каждые 5-10 минут, follow Seeds (Wheat Seeds, Melon Seeds, Pumpkin Seeds, Beetroot Seeds), Feather Falling = -1 block/sec fall speed
- **4.3.5** **Rabbit:** 3 HP, drop: 0-1 Rabbit Hide + 0-1 Raw Rabbit, 6 цветов + Killer Bunny (Easter egg), быстрые прыжки
- **4.3.6** **Horse/Donkey/Mule:** 15-30 HP, saddleable, Jump Boost, different speeds/jump heights, taming (repeated mounting), Donkey/Mule → Chest (15 slots), Armor (Horse only)
- **4.3.7** **Llama/Trader Llama:** 15-30 HP, spit attack, Carpet decoration (16 цветов), Chest (3-15 slots depending on Strength), caravan (follow each other)
- **4.3.8** **Fox:** 10 HP, ночной, подбирает предметы (mouth carry), спит днём, flee от игрока (если не tamed via breeding near player), Sweet Berries → breeding
- **4.3.9** **Axolotl:** 14 HP, Lush Caves, 5 цветов (Leucistic/Wild/Gold/Cyan/Blue=rare), play dead (feign death → regen), attack Drowned/Guardian/Fish
- **4.3.10** **Frog:** 10 HP, Swamp, 3 варианта (Temperate/Cold/Warm), eat small Slime/Magma Cube → drop Froglight (Pearlescent/Verdant/Ochre), Tadpole → Frog (biome-dependent)
- **4.3.11** **Camel (1.20):** 32 HP, Desert Village, 2-player riding, dash ability (long jump), tall (2.5 blocks → most melee mobs can't reach rider)
- **4.3.12** **Sniffer (1.20):** 14 HP, bred from Sniffer Egg (found in Suspicious Sand in Ocean Ruins), sniffs ground → digs Torchflower Seeds / Pitcher Pod
- **4.3.13** 🔧 **Отладка:** Каждый моб: spawn, wander, breeding (2 adult + food → baby + XP), drops, unique mechanics

#### 4.4 Hostile Mobs
- **4.4.1** **Zombie:** 20 HP, 3 damage, drop: 0-2 Rotten Flesh + rare Iron Ingot/Carrot/Potato, burns in sun (unless helmet), can break doors (Hard), Baby Zombie (faster, doesn't burn), Zombie Villager (cure: Weakness Potion + Golden Apple → 3-5 min)
- **4.4.2** **Skeleton:** 20 HP, bow attack (damage varies by difficulty), drop: 0-2 Bones + 0-2 Arrows + equipped Bow, burns in sun, Stray (Snow biome, Slowness arrows)
- **4.4.3** **Creeper:** 20 HP, explosion (radius 3, powered=6), silent approach, 1.5 sec fuse, drop: 0-2 Gunpowder + Music Disc (if killed by Skeleton/Stray), Charged Creeper (lightning → supercharged, mob heads drop)
- **4.4.4** **Spider:** 16 HP, 2-3 damage, climb walls, hostile at night (light < 7), passive during day, Cave Spider (12 HP, Poison on attack, Mineshaft spawner)
- **4.4.5** **Enderman:** 40 HP, 4.5-7 damage, teleport, neutral (hostile when looked at or attacked), pickup/place certain blocks, Water damage, Pearl drop: 0-1 Ender Pearl
- **4.4.6** **Witch:** 26 HP, throws Splash Potions (Poison, Slowness, Weakness, Harming), drinks Health/Fire Resistance/Water Breathing when threatened, drop: 0-6 various potion ingredients
- **4.4.7** **Slime:** Small (1 HP), Medium (4 HP, splits into 2-4 Small), Large (16 HP, splits into 2-4 Medium), Swamp (Y=50..70, light any) + Slime chunks (below Y=40)
- **4.4.8** **Phantom:** 20 HP, attacks players who haven't slept 3+ nights, swooping attack, burns in sun, drop: 0-1 Phantom Membrane (slow falling + elytra repair)
- **4.4.9** **Drowned:** 20 HP, underwater Zombie, Trident attack (ranged, 8 damage), Copper Ingot drop, Trident drop (8.5% Bedrock)
- **4.4.10** **Pillager:** 24 HP, Crossbow attack, Patrol spawn (5 Pillagers), Raid trigger (Bad Omen effect when Captain killed), Outpost respawn
- **4.4.11** **Warden:** 500 HP, 16-45 damage (highest in game), Deep Dark only, responds to vibrations (Sculk Sensor/Shrieker), darkness effect, sonic boom ranged attack (bypasses armor), no drops (discourage fighting), doesn't despawn until calm + no vibrations for 60 seconds
- **4.4.12** 🔧 **Отладка:** Zombie → night spawn, attack player, burn in sun, Baby variant. Creeper → sneak, explode, Charged by lightning. Warden → vibration detection, darkness, overwhelming damage. Каждый моб: урон, лут, spawn conditions

#### 4.5 Boss Mobs
- **4.5.1** **Ender Dragon:** 200 HP, End dimension, circle+strafe+dive AI, End Crystal healing (destroy crystals first), Breath attack (Dragon's Breath collectible), death → XP fountain (12000 XP), End Gateway spawn, Egg drop, respawn mechanic (4 End Crystals on portal)
- **4.5.2** **Wither:** 300 HP (600 Bedrock), summoned from 4 Soul Sand + 3 Wither Skeleton Skulls, explosion on spawn, Wither Skull projectiles, half-health → Wither Armor (arrow immune), destroys blocks on contact, Nether Star drop, Wither effect (poison-like, hearts turn black)
- **4.5.3** Boss bar UI: полоска HP вверху экрана, имя босса, цвет (Dragon=pink, Wither=purple)
- **4.5.4** 🔧 **Отладка:** Dragon fight: crystals heal, destroy → dragon vulnerable, phases (circle/strafe/dive/perch), death animation + XP. Wither: spawn explosion, skull attacks, half-health armor

#### 4.6 Villager System
- **4.6.1** 14 профессий: Armorer, Butcher, Cartographer, Cleric, Farmer, Fisherman, Fletcher, Leatherworker, Librarian, Mason, Nitwit, Shepherd, Toolsmith, Weaponsmith
- **4.6.2** Workstation association: Armorer→Blast Furnace, Farmer→Composter, Librarian→Lectern, etc.
- **4.6.3** 5 уровней торговли: Novice → Apprentice → Journeyman → Expert → Master (XP-based)
- **4.6.4** Trading UI: 2 input slots + 1 output, price adjustment (reputation, demand), locked trades (restock 2x/day at workstation)
- **4.6.5** Breeding: 2 Villagers + enough beds + food (Bread×3, Carrot×12, Potato×12, Beetroot×12) → Baby Villager (20 min to grow)
- **4.6.6** Iron Golem spawning: 3+ Villagers + 3+ beds + recent sleep + gossip → Iron Golem (100 HP, 7.5-21.5 damage, flower giving)
- **4.6.7** Zombie Villager curing: Splash Potion of Weakness + Golden Apple → 3-5 min → Villager (discount trades!)
- **4.6.8** Raid system: killing Pillager Captain → Bad Omen → entering Village → Raid (7 waves on Hard, Pillagers/Vindicators/Evokers/Ravagers/Witches), Hero of the Village reward (discount trades)
- **4.6.9** 🔧 **Отладка:** Village → villagers have professions → trading works → breeding → Iron Golem spawns. Raid: Bad Omen → waves → victory → Hero of the Village

#### 4.7 Mob Spawning
- **4.7.1** `SpawnManager.cs`:
  - Hostile: light level ≤ 0 (surface, 1.18+ only at night in complete darkness) or underground at any time
  - Passive: light level ≥ 9, Grass Block surface, biome-specific
  - Spawn caps per player: Hostile=70, Passive=10, Ambient(Bat)=15, WaterCreature=5, WaterAmbient=20, UndergroundWaterCreature=5
  - Despawn: > 128 blocks from player → instant. > 32 blocks → random chance. Named mobs/tamed animals → never
  - Spawn cycle: every game tick, check chunks in 128-block sphere, attempt spawns in valid positions
  - Pack spawning: Zombies 1-4, Skeletons 1-4, Creepers 1, Spiders 1, Pigs 1-4, Sheep 2-3, Cows 1-4
- **4.7.2** 🔧 **Отладка:** Ночью → мобы spawn-ятся на поверхности (dark). Пещеры → мобы в темноте. Факелы → безопасная зона (light ≥ 1 surface, ≥ 1 cave). Spawn caps → не более 70 hostile

#### 4.8 Mob Models и Animation
- **4.8.1** Система: кубоидные bone-based модели (как в MC: голова, тело, руки, ноги — отдельные кубоиды)
- **4.8.2** Процедурная генерация мешей из текстур entity/ (каждый моб = 64×32 или 64×64 texture sheet → box mesh mapping)
- **4.8.3** Animator Controller: Walk (ноги, руки качаются), Idle (дыхание), Attack (swing), Hurt (красная вспышка), Death (падение набок)
- **4.8.4** Текстурные варианты: Sheep без шерсти, Baby scale (0.5), Creeper charged glow overlay
- **4.8.5** 🔧 **Отладка:** Визуальная проверка каждого моба: модель соответствует MC, анимации плавные, нет z-fighting между частями тела

---

### ═══════════════════════════════════════════════════════
### ФАЗА 5 — Игровые механики
### ═══════════════════════════════════════════════════════

#### 5.1 Health System
- **5.1.1** `PlayerHealth.cs`:
  - 20 HP (10 сердец по 2 HP каждое), визуал: hearts HUD (из `gui/icons.png`)
  - Damage types: `Fall, Mob, Player, Fire, Lava, Drown, Suffocation, Explosion, Projectile, Magic, Starve, Wither, Void, Cactus, SweetBerryBush, Freeze`
  - Damage calculation: `finalDamage = incomingDamage - (armorDefense × (1 - (armorToughness / (armorToughness + 8)))) - enchantmentProtection`
  - Armor protection: `damageReduction = defensePoints / 25.0` (capped at 80% for full Diamond=20 points)
  - Enchantment Protection Factor (EPF): `Protection=4×level`, `FireProtection=8×level`, `BlastProtection=8×level`, `ProjectileProtection=8×level`, cap EPF=20 → final modifier = EPF × (1 + random(0,1)) / 25, cap 80%
  - Invulnerability: 0.5 секунды после получения урона (10 ticks)
  - Totem of Undying: если в offhand/mainhand при смертельном уроне → 1 HP + Regeneration II + Absorption II + Fire Resistance I (40 sec)
  - Hearts animation: wiggle при low HP, flash при damage
- **5.1.2** **Death:** Death screen ("You Died!", score, Respawn / Title Screen), drop all items at death location, respawn at world spawn or bed
- **5.1.3** **Natural Regeneration:** если hunger ≥ 18 (9 drumsticks) → heal 1 HP каждые 4 сек
- **5.1.4** **Void damage:** below Y=-64 → 4 damage per 0.5 sec
- **5.1.5** 🔧 **Отладка:** Fall damage (4+ blocks → damage), Lava (instant damage, fire 15 sec), Drowning (bubbles HUD), Void kill. Armor reduces damage. Death → items drop → respawn at spawn/bed

#### 5.2 Hunger System
- **5.2.1** `PlayerHunger.cs`:
  - 20 Hunger Points (10 drumsticks × 2), визуал: drumstick HUD
  - Saturation: hidden value, decreases before hunger. Max saturation = current hunger level
  - Exhaustion: hidden float, accumulates from actions:
    - Sprint: 0.1 per meter
    - Jump: 0.05 (Sprint Jump: 0.2)
    - Attack: 0.1
    - Block break: 0.005
    - Swim: 0.01 per meter
    - Walk: 0 (свободно)
  - When exhaustion ≥ 4.0: exhaustion -= 4.0, saturation -= 1.0 (if saturation > 0) или hunger -= 1 (if saturation = 0)
  - Hunger ≤ 0: starve damage (1 HP per 4 sec, stops at 1 HP on Easy, kills on Hard)
  - Hunger ≥ 18: natural regen (1 HP per 4 sec, costs 6 exhaustion)
  - Hunger ≥ 20 + saturation > 0: fast regen (1 HP per 0.5 sec, costs 6 exhaustion)
  - Hunger ≤ 6: cannot sprint
  - Drumstick shaking animation when hunger ≤ 6
- **5.2.2** 🔧 **Отладка:** Sprint → hunger decreases. Eat food → hunger + saturation restore. Hunger = 0 → starve damage. Hunger = 20 → regen

#### 5.3 Food System
- **5.3.1** Eating mechanic: hold RMB на food item → eating animation (arm raised) + звук (chomp) × 4 за 1.61 sec → restore hunger+saturation
- **5.3.2** Полная таблица food items (из MC 1.20):
  - Apple (4🍖, 2.4sat), Baked Potato (5, 6), Beetroot (1, 1.2), Bread (5, 6), Carrot (3, 3.6)
  - Cooked Beef (8, 12.8), Cooked Chicken (6, 7.2), Cooked Cod (5, 6), Cooked Mutton (6, 9.6)
  - Cooked Porkchop (8, 12.8), Cooked Rabbit (5, 6), Cooked Salmon (6, 9.6)
  - Cookie (2, 0.4), Dried Kelp (1, 0.6, eat speed 0.865 sec — fastest food!)
  - Enchanted Golden Apple (4, 9.6, Absorption IV 2min + Regeneration II 20sec + Resistance 5min + Fire Resistance 5min)
  - Golden Apple (4, 9.6, Absorption I 2min + Regeneration II 5sec)
  - Golden Carrot (6, 14.4 — highest saturation in game)
  - Honey Bottle (6, 1.2, clears Poison)
  - Melon Slice (2, 1.2), Mushroom Stew (6, 7.2, bowl returned)
  - Poisonous Potato (2, 1.2, 60% Poison 4sec)
  - Pumpkin Pie (8, 4.8), Raw Beef (3, 1.8), Raw Chicken (2, 1.2, 30% Hunger effect 30sec)
  - Raw Cod (2, 0.4), Raw Mutton (2, 1.2), Raw Porkchop (3, 1.8), Raw Rabbit (3, 1.8)
  - Raw Salmon (2, 0.4), Rotten Flesh (4, 0.8, 80% Hunger effect 30sec)
  - Spider Eye (2, 3.2, Poison 4sec), Sweet Berries (2, 0.4), Glow Berries (2, 0.4)
  - Suspicious Stew (6, 7.2, random effect based on flower used)
  - Chorus Fruit (4, 2.4, random teleport 8-block radius)
- **5.3.3** 🔧 **Отладка:** Eat Golden Carrot → most saturation. Rotten Flesh → Hunger effect (green HUD). Chorus Fruit → teleport. Golden Apple → glowing hearts (Absorption)

#### 5.4 Combat System
- **5.4.1** `PlayerCombat.cs`:
  - Attack cooldown (MC 1.9+): после атаки → cooldown indicator (sword-shaped progress bar below crosshair)
  - Attack speeds: Sword=1.6/sec, Axe=0.8-1.0, Pickaxe=1.2, Shovel=1.0, Hand=4.0, Trident=1.1
  - Full charge bonus: 100% damage at full cooldown, reduced at partial
  - Critical hit: jumping + falling + full charge → 1.5× damage + particles
  - Sweeping Edge (sword only, on ground): hit all enemies in 1-block radius in front, sweep damage = 1 + (attack_damage - 1) × sweepLevel / (sweepLevel + 1)
  - Knockback: base 0.4 blocks, +0.4 per Knockback enchantment level, sprint attack → +0.5
  - Shield blocking: hold RMB with shield → block 100% melee/arrow damage from front, Axe attack → disable shield 5 sec
- **5.4.2** Bow: RMB hold → charge (1 sec full), release → Arrow. Damage: 1-6 (no charge) to 6-25 (critical, full charge + Power V). Infinity enchantment → no arrow consumption (except tipped)
- **5.4.3** Crossbow: RMB hold → charge (1.25 sec), stays loaded, RMB → fire. Multishot → 3 arrows. Piercing → through entities. Firework Rocket ammunition
- **5.4.4** Trident: melee 9 damage, throw 8 damage, Loyalty → returns, Riptide → dash in rain/water, Channeling → lightning on thunderstorm
- **5.4.5** 🔧 **Отладка:** Attack cooldown indicator visible. Full charge → full damage. Critical hit: jump+fall+swing → 1.5× + stars. Shield: block Skeleton arrow. Bow: charge + release → arrow physics

#### 5.5 Status Effects
- **5.5.1** `PlayerEffects.cs` + `StatusEffectSystem.cs`:
  - Каждый эффект: ID, amplifier (0-255), duration (ticks), particle visibility, icon (в HUD corner)
  - **Speed (I-II):** move speed +20%/+40%
  - **Slowness (I-IV):** move speed -15%×level
  - **Haste (I-II):** mining speed +20%/+40%, attack speed +10%/+20%
  - **Mining Fatigue (I-III):** mining speed ×0.3/×0.09/×0.0027
  - **Strength (I-II):** +3/+6 attack damage
  - **Instant Health (I-II):** heal 4/8 HP (damages undead)
  - **Instant Damage (I-II):** 6/12 damage (heals undead)
  - **Jump Boost (I-II):** +50%/+100% jump height
  - **Nausea:** screen wobble effect (shader distortion)
  - **Regeneration (I-II):** heal 1 HP every 50/25 ticks
  - **Resistance (I-IV):** -20%×level damage
  - **Fire Resistance:** immune to fire/lava/blaze
  - **Water Breathing:** no drowning, clear underwater vision
  - **Invisibility:** mobs don't target (unless bumped), armor still visible
  - **Night Vision:** full brightness everywhere
  - **Hunger:** exhaustion +0.005×level per tick
  - **Weakness (I):** -4 attack damage
  - **Poison (I-II):** 1 damage every 25/12 ticks (can't kill, stops at 1 HP)
  - **Wither (I-II):** like Poison but can kill, hearts turn black
  - **Absorption (I-IV):** +4×level temporary HP (golden hearts, don't regen)
  - **Bad Omen (I-V):** triggers Raid on Village entry
  - **Hero of the Village (I-V):** trade discounts
  - **Darkness:** screen darkens periodically (Warden)
- **5.5.2** HUD: effect icons top-right corner with remaining duration, blinking when < 10 sec
- **5.5.3** 🔧 **Отладка:** Drink Speed potion → visually faster. Night Vision → bright everywhere. Poison → damage tick + hearts turn green. Wither → hearts turn black, can kill

#### 5.6 Enchanting System
- **5.6.1** Enchantment Table: requires Bookshelves (up to 15, placed 1 block away with air between), 3 enchantment options shown (level 1-30), Lapis Lazuli cost (1/2/3), XP level cost (1/2/3)
- **5.6.2** Enchantment algorithm:
  - Modified level = base_level + random(0, bookshelf_count/2) + random(0, bookshelf_count/2) + 1
  - Select enchantments from pool based on modified level
  - Treasure enchantments (Mending, Frost Walker, Soul Speed, Swift Sneak, Curse of Binding/Vanishing) — only from chest/trading/fishing, not from table
- **5.6.3** All enchantments (37 in MC 1.20):
  - **Weapons:** Sharpness (I-V), Smite (I-V), Bane of Arthropods (I-V), Knockback (I-II), Fire Aspect (I-II), Looting (I-III), Sweeping Edge (I-III)
  - **Tools:** Efficiency (I-V), Fortune (I-III), Silk Touch (I), Unbreaking (I-III)
  - **Bow:** Power (I-V), Punch (I-II), Flame (I), Infinity (I)
  - **Crossbow:** Quick Charge (I-III), Multishot (I), Piercing (I-IV)
  - **Trident:** Loyalty (I-III), Riptide (I-III), Channeling (I), Impaling (I-V)
  - **Armor:** Protection (I-IV), Fire Protection (I-IV), Blast Protection (I-IV), Projectile Protection (I-IV), Thorns (I-III), Depth Strider (I-III), Frost Walker (I-II), Respiration (I-III), Aqua Affinity (I), Feather Falling (I-IV), Soul Speed (I-III), Swift Sneak (I-III)
  - **General:** Unbreaking (I-III), Mending (I), Curse of Binding, Curse of Vanishing
  - Mutual exclusions: Silk Touch ↔ Fortune, Protection ↔ Fire/Blast/Projectile Protection, Sharpness ↔ Smite ↔ Bane, Infinity ↔ Mending, Riptide ↔ Loyalty ↔ Channeling, Depth Strider ↔ Frost Walker
- **5.6.4** Enchanted item rendering: purple shimmer overlay (animated UV scrolling in shader)
- **5.6.5** 🔧 **Отладка:** 15 bookshelves → level 30 options. Enchant Diamond Sword → Sharpness V possible. Fortune III on Diamond Ore → 1-4 diamonds. Silk Touch on Grass → Grass Block drop

#### 5.7 Brewing System
- **5.7.1** Brewing Stand block entity: 3 bottle slots + 1 ingredient slot + 1 fuel slot (Blaze Powder, 20 uses each)
- **5.7.2** Brewing chain:
  - Water Bottle → Awkward Potion (Nether Wart)
  - Awkward → Night Vision (Golden Carrot), Invisibility (Fermented Spider Eye on Night Vision), Water Breathing (Pufferfish), Fire Resistance (Magma Cream), Speed (Sugar), Slowness (Fermented Spider Eye on Speed), Jump Boost (Rabbit's Foot), Healing (Glistering Melon), Harming (Fermented Spider Eye on Healing), Poison (Spider Eye), Regeneration (Ghast Tear), Strength (Blaze Powder), Weakness (Fermented Spider Eye direct on Water Bottle — only exception), Turtle Master (Turtle Shell), Slow Falling (Phantom Membrane)
  - Modifiers: Redstone Dust → extend duration, Glowstone Dust → amplify (II), Gunpowder → Splash, Dragon's Breath → Lingering
- **5.7.3** 🔧 **Отладка:** Brew Night Vision potion (3 bottles). Add Redstone → extended. Add Fermented Spider Eye → Invisibility. Splash → throwable. Lingering → area effect cloud

#### 5.8 Anvil System
- **5.8.1** Anvil block: Heavy (falls like sand), 3 damage states (Anvil → Chipped → Damaged → break), 12% chance of degrading per use
- **5.8.2** Functions: Rename (1 level cost), Repair (combine 2 same-type items → durability sum + 12%), Combine enchantments (from sacrifice item onto target)
- **5.8.3** XP cost calculation: base + rename penalty + repair cost + enchantment cost. Max 39 levels (otherwise "Too Expensive!")
- **5.8.4** Prior Work Penalty: doubles with each anvil use (0, 1, 3, 7, 15, 31 → Too Expensive)
- **5.8.5** 🔧 **Отладка:** Rename sword → 1 level. Combine 2 enchanted swords → merged enchantments. Repair Diamond Pickaxe with Diamond → durability restored

#### 5.9 Redstone System
- **5.9.1** `RedstoneSignalManager.cs`: BFS-based signal propagation
  - **Redstone Dust:** signal strength 0-15, decreases by 1 per block, visual red intensity
  - **Redstone Torch:** outputs 15, inverts input (OFF when powered from below)
  - **Lever:** toggle, outputs 15 when ON
  - **Button:** pulse 15 for 10 ticks (stone) or 15 ticks (wood)
  - **Pressure Plate:** entity detection, wood = any entity, stone = only players/mobs (not items)
  - **Repeater:** 1-4 tick delay, signal refresh to 15, can be locked by side repeater
  - **Comparator:** measure mode (container fullness → signal 0-15) or subtract mode (rear - side)
  - **Observer:** detects block state change, outputs 1 tick pulse
  - **Piston/Sticky Piston:** push up to 12 blocks, Sticky pulls 1 block back. Quasi-connectivity (BUD behavior)
  - **Dispenser:** RMB-click to open (9 slots), redstone pulse → dispense item (arrows shot, water/lava placed, TNT ignited, spawn eggs used, fireworks launched)
  - **Dropper:** like Dispenser but always drops item entity (never uses)
  - **Hopper:** 5 slots, transfers 1 item per 8 ticks, chain-able, disabled by redstone
  - **Note Block:** musical notes, pitch depends on clicks, instrument depends on block below (wood=bass, stone=bass drum, sand=snare, glass=hi-hat, etc.)
  - **TNT:** activated by redstone/fire/explosion, 40 tick fuse, power 4 explosion
  - **Target Block:** signal strength proportional to accuracy (center=15)
  - **Daylight Detector:** signal based on sun position (inverted mode available)
  - **Tripwire/Tripwire Hook:** string detection, signal when entity crosses
- **5.9.2** Block update order: MC-consistent (West, East, Down, Up, North, South)
- **5.9.3** 🔧 **Отладка:** Simple circuit: Lever → Dust → Lamp. Repeater chain → clock. Piston door (2×2). Hopper into chest. Comparator reading chest fullness. TNT cannon

#### 5.10 Farming System
- **5.10.1** Farmland block: created by Hoe on Dirt/Grass, moistened if Water within 4 blocks (Y=same or Y+1), dries out → Dirt if no water, trampled by jumping
- **5.10.2** Crop growth:
  - **Wheat/Carrots/Potatoes/Beetroot:** 8 growth stages (7 for Beetroot=4), random tick → growth check
  - Growth rate: base 1/3 chance per random tick, multiplied by:
    - Hydrated farmland: ×4
    - Adjacent crops (same type reduces growth, alternating rows optimal)
    - Light level ≥ 9 required
  - Average time: ~24 min with ideal conditions (41 min single crop)
  - Bone Meal: advance 2-5 stages (Wheat/Carrot/Potato), 1-3 stages (Beetroot)
  - Harvest: break mature crop → seeds + product (Wheat=1 Wheat + 0-3 Seeds, Potato=1-4 Potatoes, Carrot=1-4 Carrots, Beetroot=1 Beetroot + 0-3 Seeds)
  - Fortune enchantment increases max drops
- **5.10.3** **Melon/Pumpkin:** plant Melon/Pumpkin Seeds on farmland, grow stem (8 stages), mature stem produces fruit on adjacent Dirt/Grass/Farmland, 1 fruit per stem, Silk Touch → stem itself
- **5.10.4** **Sugar Cane:** plant on Sand/Dirt next to water, grows 1-3 blocks tall, random tick, no bone meal (Java)
- **5.10.5** **Cactus:** plant on Sand, grows 1-3 tall, destroys items on contact, no adjacent blocks
- **5.10.6** **Bamboo:** plant on Grass/Dirt/Sand/Podzol/Gravel, grows to 12-16, bone meal works, fastest growing "crop"
- **5.10.7** **Nether Wart:** plant on Soul Sand, 4 stages, grows in any dimension, no bone meal
- **5.10.8** **Cocoa Beans:** plant on Jungle Log, 3 stages, bone meal works
- **5.10.9** **Sweet Berry Bush:** 4 stages, hurts + slows entities, harvest with RMB (1-3 berries)
- **5.10.10** **Chorus Plant:** End-only, grows on End Stone, break bottom → cascade break, Chorus Fruit drop
- **5.10.11** 🔧 **Отладка:** Plant wheat → wait/bonemeal → harvest → seeds+wheat. Automatic farm: Water canal + farmland rows + hopper collection. Melon: stem grows → fruit appears on adjacent block. Cactus auto-farm: stack + water collection

#### 5.11 Fishing System
- **5.11.1** Fishing Rod: RMB to cast (bobber entity), wait for splash particle + sound → RMB to reel in
- **5.11.2** Catch categories:
  - **Fish (85%):** Cod (60%), Salmon (25%), Tropical Fish (2%), Pufferfish (13%)
  - **Treasure (5%):** Bow, Enchanted Book, Fishing Rod, Name Tag, Nautilus Shell, Saddle (all 16.7%)
  - **Junk (10%):** Lily Pad, Bowl, Fishing Rod (damaged), Leather, Leather Boots, Rotten Flesh, Stick, String, Water Bottle, Bone, Ink Sac, Tripwire Hook
  - Luck of the Sea enchantment: +2% treasure per level, -2% junk per level
  - Lure enchantment: -5 seconds per level from wait time
- **5.11.3** Wait time: 5-30 seconds base
- **5.11.4** 🔧 **Отладка:** Cast into water → wait → splash → reel in → fish. AFK fishing farm (note: MC 1.16+ nerfed this requiring open sky + water)

#### 5.12 Armor Trims (1.20)
- **5.12.1** Smithing Table UI: 3 input slots (Template + Armor + Trim Material) → trimmed armor
- **5.12.2** 16 Trim Templates: Sentry, Dune, Coast, Wild, Ward, Eye, Vex, Tide, Snout, Rib, Wayfinder, Shaper, Silence, Raiser, Host, Spire (each found in specific structures)
- **5.12.3** 10 Trim Materials: Quartz, Iron, Netherite, Redstone, Copper, Gold, Emerald, Diamond, Lapis, Amethyst (each gives unique color)
- **5.12.4** Visual: texture overlay on armor (shader-based tinting)
- **5.12.5** Template duplication: 7 Diamonds + Template + block (specific per template) → 2 Templates
- **5.12.6** 🔧 **Отладка:** Apply Coast trim with Gold to Iron Chestplate → golden pattern visible

---

### ═══════════════════════════════════════════════════════
### ФАЗА 6 — Физика жидкостей и блочные обновления
### ═══════════════════════════════════════════════════════

#### 6.1 Water Flow
- **6.1.1** `WaterSimulation.cs`:
  - Source block: level 0 (full, placed by player or natural)
  - Flowing: levels 1-7 (decreasing), spread horizontally to adjacent Air (level +1), fall → reset to 0
  - Search for path downward: BFS to find nearest downhill → flow preferentially toward drops
  - Two sources adjacent → create new source between (infinite water)
  - Waterlogged blocks: Slabs, Stairs, Fences, Signs, etc. can contain water
  - Water removes: Torch, Redstone Dust, Flower, Tall Grass, etc.
  - Water pushes entities (flow direction, strength based on level)
  - Obsidian: Water + Lava source = Obsidian
  - Cobblestone: Water flowing + Lava flowing = Cobblestone
  - Stone: Lava source + Water flowing on top = Stone
- **6.1.2** Water tick: every 5 game ticks (vs Lava every 30 in Overworld, 10 in Nether)
- **6.1.3** 🔧 **Отладка:** Place water → spreads 7 blocks. Remove source → dries up. 2 sources in 2×2 → infinite. Water + Lava → Obsidian/Cobblestone/Stone (correct placement)

#### 6.2 Lava Flow
- **6.2.1** Like water but: spread distance 3 (Overworld) / 7 (Nether), tick rate 30/10, fire spread to flammable blocks in 1-block radius, stone generation rules (see 6.1.1)
- **6.2.2** Fire spread: lava sets fire to flammable blocks (wood, wool, leaves) within radius 2
- **6.2.3** 🔧 **Отладка:** Lava spreads 3 blocks in Overworld. Sets wooden planks on fire. Lava + Water → correct stone types

#### 6.3 Block Updates (Gravity, Fire, Leaf Decay)
- **6.3.1** `BlockUpdateManager.cs` — scheduled tick system:
  - **Sand/Gravel/Concrete Powder:** when block below is Air/Water → become FallingBlock entity → fall → place at landing (or drop as item if can't place). Check on neighbor change
  - **Fire:** spread to flammable blocks (probability based on flammability + fireSpread values), burn out after 15-20 sec if not on Netherrack/Magma, rain extinguishes
  - **Leaf Decay:** when no Log within 7 blocks (distance state 1-7) → decay (drop saplings/sticks/apples per loot table)
  - **Crop Growth:** random tick → growth stage advance (see 5.10)
  - **Ice Formation:** Water in cold biomes → Ice if exposed to sky + light level < 12
  - **Snow Accumulation:** Snow layer in snowy biomes during snowfall
  - **Grass Spread:** Dirt block with Grass Block adjacent + light ≥ 9 above Dirt → Dirt becomes Grass
  - **Mycelium Spread:** like Grass but for Mushroom Fields
  - **Coral Dying:** Coral block not in water → Dead Coral after 1-5 ticks
  - **Copper Oxidation:** Exposed → Weathered → Oxidized (random tick, very slow), wax with Honeycomb to prevent, scrape with Axe to reverse
  - **Bubble Columns:** Magma block under water → downward pull, Soul Sand → upward push
- **6.3.2** Random tick: 3 random blocks per chunk section (16×16×16) per game tick, used for crop growth, grass spread, leaf decay
- **6.3.3** 🔧 **Отладка:** Place Sand above Air → falls. Remove Log → leaves decay (within ~30 sec). Fire on Wool → burns and spreads. Grass spreads to adjacent Dirt. Copper ages over time

#### 6.4 Explosions
- **6.4.1** `ExplosionSystem.cs`:
  - Ray-based: cast 1352 rays from center, each ray starts with power, decreases by 0.225 + block_blastResistance × 0.3 per block
  - TNT: power 4 (radius ~4-5 blocks in open). Creeper: power 3. Charged Creeper: power 6. Ghast Fireball: power 1. Wither Skull: power 1. Bed (Nether/End): power 5
  - Block drops: 1/power chance (TNT = 100% at power 4, but in MC actual drop rate = 1/power ≈ 25%)
  - Entity damage: exposure-based (raycast from explosion center, % of bounding box visible), damage = (1 - distance/power) × 2 × power × 7 + 1
  - Entity knockback: push away from center, scaled by distance
  - Fire: TNT does NOT create fire. Ghast Fireball and Blaze Fireball DO create fire
  - Visual: particles (explosion_large), sound, block break particles
- **6.4.2** 🔧 **Отладка:** TNT → appropriate crater, items drop. Creeper → smaller explosion. Chain TNT → cascading explosion. Obsidian survives TNT

---

### ═══════════════════════════════════════════════════════
### ФАЗА 7 — Звук и Частицы
### ═══════════════════════════════════════════════════════

#### 7.1 Sound System
- **7.1.1** `SoundManager.cs`:
  - AudioSource pool (32+ sources)
  - 3D spatial audio (linear rolloff, max distance 16-64 blocks depending on sound)
  - Random pitch variation: ×0.8..1.2 for most sounds
  - Sound categories: Master, Music, Blocks, Hostile, Friendly, Players, Ambient, Weather, UI
  - Each category → AudioMixerGroup with independent volume slider
- **7.1.2** `SoundData.cs` SO:
  ```
  AudioClip[] variants (random selection)
  float volume (0-1)
  float minPitch, maxPitch
  float maxDistance
  SoundCategory category
  bool loop
  ```
- **7.1.3** Block sounds (6 events per SoundCategory):
  - **Dig** (during breaking), **Break** (block destroyed), **Place** (block placed), **Step** (walk on), **Hit** (punch without breaking), **Fall** (land on)
  - Categories: Stone, Wood, Grass, Gravel, Sand, Glass, Metal, Wool, Snow, Coral, Amethyst, Deepslate, Sculk, CherryWood, BambooWood, Mud, PackedMud, Nether
- **7.1.4** 🔧 **Отладка:** Walk on stone → stone step sound with random pitch. Break glass → glass shatter. Place wood → wood thump. All categories audible and distinct

#### 7.2 Music System
- **7.2.1** Background music: random track every 3-10 minutes, fade in/out over 2 seconds
- **7.2.2** Biome-specific ambient loops: Cave (dripping, wind), Ocean (underwater echo), Nether (ominous drone), End (eerie)
- **7.2.3** Music Discs: 16 discs (13, cat, blocks, chirp, far, mall, mellohi, stal, strad, ward, 11, wait, otherside, 5, Relic, Pigstep), play in Jukebox
- **7.2.4** 🔧 **Отладка:** Stand still in Plains → calm music plays after 3-10 min. Enter cave → ambient changes. Jukebox + disc → disc plays

#### 7.3 Particle System
- **7.3.1** Unity ParticleSystem prefabs:
  - **Block Break:** on destroy, block texture color tinted cubes (8-16 particles per block face), gravity, bounce
  - **Torch Flame:** constant upward flame + smoke particles
  - **Water Splash:** on entity entering water, ring of blue particles
  - **Bubble:** underwater, rising
  - **Critical Hit:** stars burst on crit attack
  - **Enchant Glyphs:** float toward Enchanting Table from bookshelves
  - **Portal:** purple swirl in Nether Portal + End Portal
  - **Explosion:** expanding sphere of smoke + fire particles
  - **Rain:** per-chunk column particles, splashes on ground
  - **Snow:** per-chunk gentle falling white particles
  - **Dragon Breath:** purple cloud, area-of-effect
  - **Campfire Smoke:** tall signal column (Signal Fire = 24 blocks tall)
  - **Cherry Petals:** falling pink particles in Cherry Grove
  - **Drip:** stalactite water/lava dripping
  - **Sculk Soul:** blue particles from Sculk Catalyst when mob dies nearby
- **7.3.2** 🔧 **Отладка:** Break stone → gray-ish particles. Torch → flame + smoke. Rain → particles + splashes. Cherry Grove → pink petals falling

---

### ═══════════════════════════════════════════════════════
### ФАЗА 8 — UI и Меню
### ═══════════════════════════════════════════════════════

#### 8.1 Main Menu
- **8.1.1** Title Screen: rotating panorama background (6 cubemap images from world render), Minecraft logo (top), Splash text (yellow, random from list, rotated 20°)
- **8.1.2** Buttons: Singleplayer, Multiplayer (grayed out initially), Settings, Quit Game
- **8.1.3** Version text: "Unicraft 1.20" bottom-left
- **8.1.4** World selection screen: list of saved worlds (name, last played date, game mode, icon), Create New World, Delete, Play
- **8.1.5** 🔧 **Отладка:** Launch → title screen. Click Singleplayer → world list. Create world → loading → gameplay

#### 8.2 World Creation
- **8.2.1** World Name input field (default "New World")
- **8.2.2** Seed input (empty = random, number or text)
- **8.2.3** Game Mode toggle: Survival / Creative / Hardcore (1 life, Hard difficulty locked)
- **8.2.4** Difficulty: Peaceful / Easy / Normal / Hard
- **8.2.5** Allow Cheats toggle (enables /gamemode, /give, etc.)
- **8.2.6** World Type: Default / Superflat / Large Biomes
- **8.2.7** 🔧 **Отладка:** Create world with specific seed → same terrain every time. Superflat → flat world

#### 8.3 Pause Menu
- **8.3.1** Esc → Pause (game freezes in singleplayer), buttons: Back to Game, Settings, Save and Quit to Title
- **8.3.2** Settings accessible from pause: same as main menu settings
- **8.3.3** 🔧 **Отладка:** Pause → game stops. Resume → continues. Save and Quit → world saved, return to title

#### 8.4 Settings
- **8.4.1** **Video:** Render Distance (2-32 chunks slider), FOV (30-110°), VSync, Max FPS, GUI Scale (1-4×), Fullscreen/Windowed, Brightness (0-100%), Smooth Lighting (Off/Minimum/Maximum), Clouds (Off/Fast/Fancy), Particles (All/Decreased/Minimal)
- **8.4.2** **Audio:** Master Volume, Music, Blocks, Hostile Creatures, Friendly Creatures, Players, Ambient/Environment, Weather
- **8.4.3** **Controls:** Key rebinding (click button → press key → bound), Mouse Sensitivity slider (1-200%), Invert Y axis, Auto-Jump toggle, Touchscreen Mode
- **8.4.4** **Skin:** Steve/Alex toggle (placeholder until custom skins)
- **8.4.5** 🔧 **Отладка:** Change render distance → chunks load/unload. Change FOV → camera updates. Rebind keys → new bindings work

#### 8.5 Death Screen
- **8.5.1** "You Died!" text (red, large), death message (e.g., "Player was slain by Zombie"), Score (XP total)
- **8.5.2** Buttons: Respawn, Title Screen
- **8.5.3** Hardcore: "Game Over!" → Spectate World / Delete World (no respawn)
- **8.5.4** 🔧 **Отладка:** Die → death screen. Respawn → spawn point (bed or world spawn). Items dropped at death location

#### 8.6 Chat and Commands
- **8.6.1** `ChatUI.cs`: T to open, Enter to send, Esc to close. Message history (scroll), semi-transparent background
- **8.6.2** `CommandManager.cs`:
  - `/gamemode creative|survival|spectator|adventure` (if cheats enabled)
  - `/give <item> [amount]`
  - `/tp <x> <y> <z>`
  - `/time set day|night|noon|midnight|<ticks>`
  - `/weather clear|rain|thunder`
  - `/kill` — instant death
  - `/seed` — show world seed
  - `/difficulty peaceful|easy|normal|hard`
  - `/effect give <effect> [duration] [amplifier]`
  - `/effect clear`
  - `/enchant <enchantment> [level]`
  - `/xp add <amount> levels|points`
  - `/summon <entity>` — spawn mob at position
  - `/locate structure <type>` — find nearest structure
  - `/fill <x1> <y1> <z1> <x2> <y2> <z2> <block>` — fill region
  - `/setblock <x> <y> <z> <block>` — place single block
- **8.6.3** Tab-completion for commands, item names, entity types
- **8.6.4** 🔧 **Отладка:** `/give diamond 64` → получить стак. `/tp 0 100 0` → телепорт. `/time set night` → ночь. `/gamemode creative` → полёт

#### 8.7 F3 Debug Screen
- **8.7.1** Left side: FPS, GPU, X/Y/Z coordinates (feet+eyes), Block position, Chunk position, Facing direction (N/S/E/W + yaw/pitch), Light level (sky+block), Biome name, Memory usage
- **8.7.2** Right side: Java version equivalent, Renderer info, chunk stats (loaded/rendered/total)
- **8.7.3** F3+G: chunk borders (wireframe lines at chunk boundaries — already have ThickLines shader!)
- **8.7.4** F3+B: entity hitboxes (wireframe AABB around all entities)
- **8.7.5** 🔧 **Отладка:** F3 → all values update in real-time. Position matches world. Biome correct. FPS stable

#### 8.8 Advancements
- **8.8.1** Tree-based system: 5 tabs (Minecraft, Nether, End, Adventure, Husbandry)
- **8.8.2** Trigger-based: `OnBlockBreak`, `OnItemPickup`, `OnEntityKill`, `OnCraft`, `OnEnterBiome`, `OnEnterDimension`
- **8.8.3** Toast notification: pop-up top-right (Achievement Made! / Goal! / Challenge Complete!)
- **8.8.4** Key advancements: "Getting an Upgrade" (Stone Pickaxe), "Diamonds!" (pick up Diamond), "We Need to Go Deeper" (enter Nether), "The End" (enter End), "Free the End" (kill Dragon)
- **8.8.5** 🔧 **Отладка:** Mine stone → "Stone Age" advancement popup. Craft pickaxe → "Getting an Upgrade". Enter Nether → advancement

---

### ═══════════════════════════════════════════════════════
### ФАЗА 9 — День/Ночь, Погода, Skybox
### ═══════════════════════════════════════════════════════

#### 9.1 Day/Night Cycle
- **9.1.1** `TimeManager.cs`: 24000 ticks per day (20 min real-time), 0=sunrise, 6000=noon, 12000=sunset, 18000=midnight
- **9.1.2** Light level adjustment: sky light multiplier based on time (night = 4/15 of day brightness)
- **9.1.3** Mob spawning ties to light level (hostile at night on surface)
- **9.1.4** Bed: skip to dawn (only if all players sleeping in multiplayer), set respawn point
- **9.1.5** Clock item: shows current time visually (rotating dial)
- **9.1.6** 🔧 **Отладка:** Watch sky → sun rises/sets. Night → dark + mobs. Bed → skip to morning. Clock item works

#### 9.2 Weather
- **9.2.1** States: Clear, Rain, Thunder
- **9.2.2** Rain: particle effects, darkened sky, fills Cauldrons, hydrates Farmland, extinguishes fire, Trident Channeling works, Enderman takes damage
- **9.2.3** Thunder: lightning strikes (random, near player), Channeling Trident → targeted strike, Pig → Zombified Piglin, Villager → Witch, Creeper → Charged Creeper, fire start, 5 damage + 5 sec fire
- **9.2.4** Snow: in cold biomes instead of rain, Snow layer accumulates
- **9.2.5** Duration: 0.5-1 day rain, 0.5-1 day thunder, 0.5-7.5 days clear between
- **9.2.6** 🔧 **Отладка:** Wait for rain → particles, darkened, cauldrons fill. Thunder → lightning strikes, mob conversions

#### 9.3 Skybox
- **9.3.1** `URP_Sky.shader`: procedural sky:
  - Day: gradient blue (#78A7FF top → #C9E3FF horizon)
  - Sunrise/Sunset: orange/pink gradient at horizon (30° arc)
  - Night: dark blue (#0D0D25) + stars (random static points) + moon (8 phases, texture)
  - Sun: bright disk, rotates 360° per day cycle
  - Moon: opposite sun, 8 phase textures (Full → Waning → New → Waxing)
- **9.3.2** Fog: distance-based, color matches sky horizon, reduces with altitude
- **9.3.3** Void fog: below Y=0, increases darkness (removed in MC 1.18+ but cool for deep mining ambiance, optional)
- **9.3.4** 🔧 **Отладка:** Time transitions smoothly. Stars visible at night. Moon phases cycle. Fog matches horizon

---

### ═══════════════════════════════════════════════════════
### ФАЗА 10 — Мультиплеер (Опционально)
### ═══════════════════════════════════════════════════════

#### 10.1 Networking Foundation
- **10.1.1** Choose: Unity Netcode for GameObjects (NGO) or custom transport (`com.unity.transport`)
- **10.1.2** Server-authoritative architecture: server owns world state, clients predict movement
- **10.1.3** Packet types: ChunkData, BlockChange, EntitySpawn/Move/Remove, PlayerPosition, ChatMessage, InventoryUpdate
- **10.1.4** Chunk streaming: server sends chunks to client as needed (based on viewDistance)
- **10.1.5** 🔧 **Отладка:** 2 clients connect → see each other → block changes sync

#### 10.2 Player Sync
- **10.2.1** Steve/Alex model visible to other players
- **10.2.2** Position + rotation interpolation (client-side prediction + server reconciliation)
- **10.2.3** Equipment visible (armor, held item)
- **10.2.4** Animation sync (walk, swing, sneak)
- **10.2.5** Name tag above head
- **10.2.6** 🔧 **Отладка:** Move around → other player sees smooth movement. Attack → swing visible. Change armor → updated on other screens

---

### ═══════════════════════════════════════════════════════
### ФАЗА 11 — Оптимизация и Полировка
### ═══════════════════════════════════════════════════════

#### 11.1 Chunk System Optimization
- **11.1.1** `IJobParallelFor` для terrain generation (по столбцам XZ → 256 параллельных jobs per chunk)
- **11.1.2** Greedy meshing: merge coplanar same-texture faces into larger quads (reduces vertex count 60-80%)
- **11.1.3** LOD: distant chunks (> 8 from player) → simplified mesh (only top surface). Very distant (> 16) → even simpler
- **11.1.4** Sub-chunk sectioning: divide 384-tall chunk into 24 sections of 16×16×16. Only rebuild modified section, not entire chunk
- **11.1.5** 🔧 **Отладка:** Profiler → mesh gen time reduced. Vertex count reduced. FPS improved at high view distance

#### 11.2 Memory Optimization
- **11.2.1** `Allocator.TempJob` для slice NativeArray-ов (вместо `Persistent`)
- **11.2.2** Palette-based chunk storage: instead of `uint[98304]`, use block palette + compact bit array (как MC Java)
- **11.2.3** Unload chunks → release NativeArrays, return to pool
- **11.2.4** Texture atlas: single Texture2DArray for all blocks, minimize material swaps
- **11.2.5** Object pooling: entities, particles, AudioSource, UI elements
- **11.2.6** 🔧 **Отладка:** Memory Profiler → no leaks during play session. GC spikes < 1ms. Stable memory after initial load

#### 11.3 Rendering Optimization
- **11.3.1** Occlusion culling: if player is underground, don't render surface chunks (depth-based test from player Y)
- **11.3.2** Ambient Occlusion: per-vertex AO for voxels (check 8 corners, darken concave edges) — improves visual quality significantly over MC
- **11.3.3** GPU Instancing for entities (same mesh/material → batch)
- **11.3.4** Draw call batching: SRP Batcher compatibility for all URP shaders
- **11.3.5** 🔧 **Отладка:** Profiler → draw calls reduced. Underground rendering culled. AO visible on block edges

#### 11.4 Gameplay Polish
- **11.4.1** Camera bob while walking (subtle, toggleable)
- **11.4.2** Hand swing animation (first-person arm + held item)
- **11.4.3** Damage tilt (camera tilts toward damage source)
- **11.4.4** Screen effects: underwater blue overlay, lava red overlay, Nether fog, Powder Snow frost border
- **11.4.5** Item tooltip with all MC formatting (enchantment list, "When in Main Hand" stats, unbreakable tag)
- **11.4.6** 🔧 **Отладка:** Walk → subtle bob. Take damage → red tilt. Go underwater → blue overlay. Hold enchanted item → shimmer + tooltip

---

## 📁 ЧАСТЬ 3: ИЕРАРХИЯ WORKSPACE

*(Идентична предыдущей версии — см. структуру дерева выше. Не дублирую для краткости)*

---

## 📌 ЧАСТЬ 4: ПРОТОКОЛ РАБОТЫ

```
┌─────────────────────────────────────────────────────────────────┐
│  ДЛЯ КАЖДОЙ ЗАДАЧИ (X.Y.Z):                                   │
│                                                                 │
│  1. Реализовать механику в полном объёме                         │
│  2. Привести максимально близко к оригиналу MC 1.20             │
│  3. Если можно сделать ОБЪЕКТИВНО лучше — оптимизировать        │
│  4. Провести отладку (🔧 пункты)                                │
│  5. ⏸ ОСТАНОВИТЬСЯ и ждать инструкции на следующую задачу        │
│                                                                 │
│  НЕ ПЕРЕХОДИТЬ к следующей задаче без явного указания!          │
└─────────────────────────────────────────────────────────────────┘
```

---

*Документ обновлён 02.06.2026*
*Unity 6000.4.8f1 | URP 17.4.0 | Input System 1.19.0*
*Всего задач: ~180 конкретных подпунктов по 11 фазам*
