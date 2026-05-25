using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Locations;
using StardewValley.Internal;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Objects;
using xTile;
using xTile.Dimensions;
using xTile.Layers;
using xTile.Tiles;

namespace MazeQuest
{
    public class ModEntry : Mod
    {
        // -------------------------------------------------------------------------
        // Constants — change these if you rename your mod or assets
        // -------------------------------------------------------------------------

        // Local JSON file read directly by this SMAPI code mod.
        // This is NOT a Content Patcher file. It must be copied next to MazeQuest.dll.
        private const string MAZE_DATA_FILE = "content.json";

        // When a maze chest has been opened and the chest menu is closed, return the player here.
        private const string RETURN_LOCATION = "Custom_Moonvillage";
        private static readonly Vector2 RETURN_TILE = new Vector2(25, 25);
        private const string CHEST_MONEY_MODDATA_KEY = "MazeQuest.ChestMoney";

        // Internal tilesheet IDs added to the map at runtime.
        // Floor/path tiles still use the normal spring outdoor sheet.
        private const string TILESHEET_MAIN_ID = "Custom_MazeQuest_Main";
        private const string TILESHEET_MAIN_PATH = "Maps/spring_outdoorsTileSheet";

        // Hedge walls, fog/bush cover, and torches use the Festivals tilesheet.
        // If your TMX already has a tilesheet with ID "Festivals", the mod reuses it.
        private const string TILESHEET_FESTIVAL_ID = "Festivals";
        private const string TILESHEET_FESTIVAL_PATH = "Maps/Festivals";

        // Fallback tile sheet dimensions. If the map already contains the sheet, these are ignored.
        private static readonly Size TILESHEET_SHEET_SIZE  = new Size(25, 79);
        private static readonly Size TILESHEET_TILE_SIZE   = new Size(16, 16);

        // -------------------------------------------------------------------------
        // Static state
        // -------------------------------------------------------------------------
        public static IMonitor SMonitor;
        public static IModHelper SHelper;
        public static ModConfig Config;
        public static ModEntry context;

        // Inga externa beroenden — skattkistor hanteras internt

        public static Dictionary<string, List<MazeInstance>> mazeLocationDict = new Dictionary<string, List<MazeInstance>>();
        public static Dictionary<string, MazeData>           mazeDataDict     = new Dictionary<string, MazeData>();

        private static Random? mazeGenerationRandom;
        private static bool pendingMazeChestReturnWarp;
        private static bool pendingMazeChestMenuWasOpen;
        private static int pendingMazeChestReturnWarpDelayTicks;

        // Used by the fairy-drawing patch
        private static int fairyFrame;

        // Debug: captures every tile written by the maze renderer when Config.Debug is true.
        private static List<string> tileWriteReport = new List<string>();

        // Chest tint colours (darkgray → purple = low → high rarity)
        private static readonly Color[] tintColors = new Color[]
        {
            Color.DarkGray,
            Color.Brown,
            Color.Silver,
            Color.Gold,
            Color.Purple
        };

        // Maze-generation helpers
        // neighbours are 2-step offsets used by the recursive-backtracker algorithm
        public static Point[] neighbours = new Point[]
        {
            new Point( 0, -2),
            new Point( 2,  0),
            new Point( 0,  2),
            new Point(-2,  0)
        };

        // surrounding is used by the fog-of-war (HideMaze) tile-reveal patch
        public static Point[] surrounding = new Point[]
        {
            new Point( 0,  0),
            new Point( 0,  1),
            new Point( 1,  0),
            new Point( 1,  1),
            new Point( 0, -1),
            new Point(-1,  0),
            new Point(-1, -1),
            new Point( 1, -1),
            new Point(-1,  1),
            new Point(-1, -2),
            new Point( 0, -2),
            new Point( 1, -2)
        };

        // =========================================================================
        // SMAPI entry point
        // =========================================================================

        public override void Entry(IModHelper helper)
        {
            Config       = helper.ReadConfig<ModConfig>();
            SMonitor     = Monitor;
            SHelper      = helper;
            context      = this;

            helper.Events.GameLoop.GameLaunched   += GameLoop_GameLaunched;
            helper.Events.GameLoop.ReturnedToTitle += GameLoop_ReturnedToTitle;
            helper.Events.GameLoop.SaveLoaded     += GameLoop_SaveLoaded;
            helper.Events.GameLoop.DayStarted     += GameLoop_DayStarted;
            helper.Events.GameLoop.DayEnding      += GameLoop_DayEnding;
            helper.Events.Input.ButtonPressed     += Input_ButtonPressed;
            helper.Events.Player.Warped           += Player_Warped;
            helper.Events.Content.AssetRequested  += Content_AssetRequested;
            helper.Events.GameLoop.UpdateTicked    += GameLoop_UpdateTicked;

            new Harmony(ModManifest.UniqueID).PatchAll();
        }

        // =========================================================================
        // Event handlers
        // =========================================================================

        private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Optional integration: GenericModConfigMenu
            var configMenu = Helper.ModRegistry
                .GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

            if (configMenu != null)
            {
                configMenu.Register(
                    ModManifest,
                    reset: () => Config = new ModConfig(),
                    save:  () => Helper.WriteConfig(Config)
                );
                configMenu.AddBoolOption(
                    ModManifest,
                    getValue: () => Config.ModEnabled,
                    setValue: v => Config.ModEnabled = v,
                    name:     () => "Mod Enabled"
                );
            }
        }



        private void GameLoop_UpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!pendingMazeChestReturnWarp || !Context.IsWorldReady) return;

            // Do not check immediately on the same tick as the click. Vanilla still needs time
            // to open the chest menu, otherwise the player can be warped before seeing rewards.
            if (pendingMazeChestReturnWarpDelayTicks > 0)
            {
                pendingMazeChestReturnWarpDelayTicks--;
                return;
            }

            // The warp is only allowed after a chest/inventory menu has actually opened once.
            if (Game1.activeClickableMenu != null)
            {
                pendingMazeChestMenuWasOpen = true;
                return;
            }

            // If the menu has not opened yet, keep waiting. This prevents instant warp.
            if (!pendingMazeChestMenuWasOpen) return;
            if (!Context.IsPlayerFree) return;

            pendingMazeChestReturnWarp = false;
            pendingMazeChestMenuWasOpen = false;
            pendingMazeChestReturnWarpDelayTicks = 0;

            SMonitor.Log($"Maze chest menu closed. Warping player back to {RETURN_LOCATION} at {RETURN_TILE.X},{RETURN_TILE.Y}.", LogLevel.Info);
            Game1.warpFarmer(RETURN_LOCATION, (int)RETURN_TILE.X, (int)RETURN_TILE.Y, false);
        }

        private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (!Config.ModEnabled) return;

            SMonitor.Log("Save loaded. Loading maze data and invalidating target maps.", LogLevel.Info);
            ReloadMazes();
        }

        private void GameLoop_ReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            pendingMazeChestReturnWarp = false;
            pendingMazeChestMenuWasOpen = false;
            pendingMazeChestReturnWarpDelayTicks = 0;
            if (!Config.ModEnabled) return;
            // Invalidate your custom map so it reloads cleanly on the next save
            Helper.GameContent.InvalidateCache("Maps/Custom_Maze");
        }

        private void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
        {
            if (!Config.ModEnabled) return;
            // Defer one tick so the world is fully loaded before we modify maps
            SHelper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked_AfterDayStarted;
        }

        private void GameLoop_UpdateTicked_AfterDayStarted(object sender, UpdateTickedEventArgs e)
        {
            ReloadMazes();
            SHelper.Events.GameLoop.UpdateTicked -= GameLoop_UpdateTicked_AfterDayStarted;
        }


        private void Player_Warped(object sender, WarpedEventArgs e)
        {
            if (!Config.ModEnabled) return;
            if (e.NewLocation == null) return;

            SMonitor.Log($"Warped to location '{e.NewLocation.NameOrUniqueName}' with map path '{e.NewLocation.mapPath?.Value}'.", LogLevel.Info);

            if (e.NewLocation.NameOrUniqueName == "Custom_Maze" || e.NewLocation.Name == "Custom_Maze")
                Game1.addHUDMessage(new HUDMessage(SHelper.Translation.Get("hud.maze.enter").ToString()));

            // If the target map was already loaded before the content asset edit ran,
            // force-apply the maze directly when the player enters the location.
            ApplyMazeToLoadedLocation(e.NewLocation);
        }

        private void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
        {
            pendingMazeChestReturnWarp = false;
            pendingMazeChestMenuWasOpen = false;
            pendingMazeChestReturnWarpDelayTicks = 0;
            if (!Config.ModEnabled) return;
            DepopulateMaps();
        }

        private void Input_ButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // Press T in debug mode to manually repopulate mazes (useful for testing)
            if (Config.ModEnabled && Context.IsWorldReady && Config.Debug
                && Game1.IsMasterGame && e.Button == SButton.T)
            {
                PopulateMazes();
            }
        }

        // =========================================================================
        // Asset pipeline
        // =========================================================================

        private void Content_AssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!Config.ModEnabled) return;

            // When a map that contains a maze is loaded, inject the maze tiles.
            if (e.DataType == typeof(Map))
            {
                foreach (var kvp in mazeLocationDict)
                {
                    foreach (var inst in kvp.Value)
                    {
                        if (inst.mapPaths.Any(path => e.NameWithoutLocale.IsEquivalentTo(path)))
                        {
                            e.Edit(data =>
                            {
                                SMonitor.Log($"Adding maze to map asset {e.NameWithoutLocale} for maze '{inst.id}'", LogLevel.Info);
                                ModifyMap(data.AsMap().Data, inst);
                            }, AssetEditPriority.Late);
                        }
                    }
                }
            }
        }

        // =========================================================================
        // Maze lifecycle
        // =========================================================================

        /// <summary>
        /// Loads the maze data dictionary, regenerates the tile arrays, and
        /// invalidates the affected maps so they pick up the new maze geometry.
        /// </summary>
        private void ReloadMazes()
        {
            try
            {
                mazeDataDict = SHelper.Data.ReadJsonFile<Dictionary<string, MazeData>>(MAZE_DATA_FILE)
                    ?? new Dictionary<string, MazeData>();
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Could not read {MAZE_DATA_FILE}. No mazes will be loaded. Error: {ex}", LogLevel.Error);
                mazeDataDict = new Dictionary<string, MazeData>();
            }

            mazeLocationDict.Clear();

            SMonitor.Log($"Got {mazeDataDict.Count} maze(s) from {MAZE_DATA_FILE}", LogLevel.Info);

            var mapsToInvalidate = new HashSet<string>();

            foreach (var kvp in mazeDataDict.ToArray())
            {
                MazeData data = kvp.Value;
                string locationName = !string.IsNullOrWhiteSpace(data.gameLocation) ? data.gameLocation : kvp.Key;

                if (!mazeLocationDict.TryGetValue(locationName, out var instances))
                {
                    instances = new List<MazeInstance>();
                    mazeLocationDict[locationName] = instances;
                }

                var inst = new MazeInstance { id = kvp.Key };

                // Maze generation requires odd dimensions.
                if (data.mapSize.X % 2 == 0) data.mapSize.X--;
                if (data.mapSize.Y % 2 == 0) data.mapSize.Y--;

                mazeDataDict[kvp.Key] = data;

                var previousMazeGenerationRandom = mazeGenerationRandom;
                try
                {
                    mazeGenerationRandom = new Random(GetStableMazeSeed(kvp.Key));
                    inst.tiles = MakeMapArray(data, inst);
                }
                finally
                {
                    mazeGenerationRandom = previousMazeGenerationRandom;
                }

                // Do NOT require Game1.getLocationFromName(locationName) here.
                // Content Patcher custom locations may not exist yet when DayStarted runs.
                // The map asset path is enough for AssetRequested to patch the map when it loads.
                var mapPaths = new List<string>();

                if (data.mapAssets != null && data.mapAssets.Count > 0)
                    mapPaths.AddRange(data.mapAssets.Where(p => !string.IsNullOrWhiteSpace(p)));

                if (!string.IsNullOrWhiteSpace(data.mapAsset))
                    mapPaths.Add(data.mapAsset);

                // Always try the normal custom-location asset name too.
                mapPaths.Add($"Maps/{locationName}");

                // Remove duplicates while keeping order.
                inst.mapPaths = mapPaths
                    .Select(p => p.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                inst.mapPath = inst.mapPaths.FirstOrDefault() ?? $"Maps/{locationName}";

                foreach (string path in inst.mapPaths)
                    mapsToInvalidate.Add(path);

                instances.Add(inst);

                SMonitor.Log($"Registered maze '{kvp.Key}' for location '{locationName}' using map assets: {string.Join(", ", inst.mapPaths)}.", LogLevel.Info);
            }

            foreach (string map in mapsToInvalidate)
            {
                SMonitor.Log($"Invalidating map asset '{map}' so the maze can be injected.", LogLevel.Info);
                SHelper.GameContent.InvalidateCache(map);
            }

            foreach (string locationName in mazeLocationDict.Keys.ToArray())
            {
                GameLocation loaded = Game1.getLocationFromName(locationName);
                if (loaded != null)
                    ApplyMazeToLoadedLocation(loaded);
            }

            if (mazeLocationDict.Count == 0)
                SMonitor.Log("No active maze locations were registered. Check content.json.", LogLevel.Warn);

            if (Game1.IsMasterGame)
                PopulateMazes();
        }

        /// <summary>
        /// Applies generated maze tiles directly to an already-loaded GameLocation map.
        /// This is needed when the player starts the day on Farm and the custom maze
        /// location has already been created before our AssetRequested edit runs.
        /// </summary>
        private static void ApplyMazeToLoadedLocation(GameLocation gl)
        {
            if (gl == null) return;

            List<MazeInstance> instances = null;

            if (!mazeLocationDict.TryGetValue(gl.NameOrUniqueName, out instances))
                mazeLocationDict.TryGetValue(gl.Name, out instances);

            // Fallback: match by map asset path. This helps custom locations where the
            // location name and map asset are not identical.
            if (instances == null && gl.mapPath?.Value != null)
            {
                instances = mazeLocationDict
                    .SelectMany(p => p.Value)
                    .Where(inst => inst.mapPaths.Any(path => string.Equals(path, gl.mapPath.Value, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            if (instances == null || instances.Count == 0)
            {
                SMonitor.Log($"No maze matched loaded location '{gl.NameOrUniqueName}' with map asset '{gl.mapPath?.Value}'.", LogLevel.Trace);
                return;
            }

            foreach (var inst in instances)
            {
                try
                {
                    SMonitor.Log($"Applying maze directly to loaded location '{gl.NameOrUniqueName}' on map '{gl.mapPath.Value}'.", LogLevel.Info);
                    ModifyMap(gl.Map, inst);
                }
                catch (Exception ex)
                {
                    SMonitor.Log($"Failed to apply maze directly to loaded location '{gl.NameOrUniqueName}': {ex}", LogLevel.Error);
                }
            }
        }

        /// <summary>
        /// Removes all maze-spawned entities and clears the location dictionary.
        /// Called on DayEnding so the day ends cleanly.
        /// </summary>
        private void DepopulateMaps()
        {
            foreach (var kvp in mazeLocationDict)
            {
                GameLocation gl = Game1.getLocationFromName(kvp.Key);
                if (gl == null) continue;

                Helper.GameContent.InvalidateCache(gl.mapPath.Value);

                foreach (var inst in kvp.Value)
                {
                    MazeData data = mazeDataDict[inst.id];

                    // Remove monsters and the optional Dwarf NPC
                    for (int i = gl.characters.Count - 1; i >= 0; i--)
                    {
                        var ch = gl.characters[i];
                        if (IsTileInMaze(ch.TilePoint, data.mapSize, data.corner)
                            && (ch is Monster || ch.Name.Equals("Dwarf")))
                        {
                            gl.characters.RemoveAt(i);
                        }
                    }

                    // Remove dropped objects
                    foreach (var pair in gl.objects.Pairs.ToArray())
                    {
                        if (IsTileInMaze(Utility.Vector2ToPoint(pair.Key), data.mapSize, data.corner))
                            gl.objects.Remove(pair.Key);
                    }
                }
            }

            mazeLocationDict.Clear();
        }

        // =========================================================================
        // Population (enemies, forage, treasure, fairies, dwarf)
        // =========================================================================

        public static void PopulateMazes()
        {
            foreach (var kvp in mazeLocationDict)
            {
                foreach (var inst in kvp.Value)
                {
                    MazeData data = mazeDataDict[inst.id];
                    GameLocation gl = Game1.getLocationFromName(kvp.Key);
                    if (gl != null) PopulateMaze(gl, data, inst);
                }
            }
        }

        public static void PopulateMaze(GameLocation gl, MazeData mazeData, MazeInstance inst)
        {
            inst.chestTiles.Clear();
            inst.fairyTiles.Clear();

            // Clear any leftovers from the previous day
            for (int x = mazeData.corner.X; x < mazeData.corner.X + mazeData.mapSize.X; x++)
            for (int y = mazeData.corner.Y; y < mazeData.corner.Y + mazeData.mapSize.Y; y++)
            {
                var tile = new Vector2(x, y);
                gl.terrainFeatures.Remove(tile);
                gl.objects.Remove(tile);
                gl.overlayObjects.Remove(tile);
            }

            for (int i = gl.characters.Count - 1; i >= 0; i--)
            {
                var ch = gl.characters[i];
                if (IsTileInMaze(ch.TilePoint, mazeData.mapSize, mazeData.corner)
                    && (ch is Monster || ch.Name.Equals("Dwarf")))
                {
                    gl.characters.RemoveAt(i);
                }
            }

            var openTiles = inst.openTiles.ToList();
            var endTiles = inst.endTiles.ToList();
            var vertTiles = inst.vertTiles.ToList();

            // Roll counts for each entity type
            int slimes    = Game1.random.Next(mazeData.SlimeMin,       mazeData.SlimeMax + 1);
            int bats      = Game1.random.Next(mazeData.BatMin,         mazeData.BatMax + 1);
            int serpents  = Game1.random.Next(mazeData.SerpentMin,     mazeData.SerpentMax + 1);
            int brutes    = Game1.random.Next(mazeData.ShadowBruteMin, mazeData.ShadowBruteMax + 1);
            int shamans   = Game1.random.Next(mazeData.ShadowShamanMin,mazeData.ShadowShamanMax + 1);
            int squids    = Game1.random.Next(mazeData.SquidMin,       mazeData.SquidMax + 1);
            int skeletons = Game1.random.Next(mazeData.SkeletonMin,    mazeData.SkeletonMax + 1);
            int dusts     = Game1.random.Next(mazeData.DustSpriteMin,  mazeData.DustSpriteMax + 1);
            int fairies   = Game1.random.Next(mazeData.FairiesMin,     mazeData.FairiesMax + 1);
            int treasures = Game1.random.Next(mazeData.TreasureMin,    mazeData.TreasureMax + 1);
            int forages   = Game1.random.Next(mazeData.ForageMin,      mazeData.ForageMax + 1);

            // ---- Forage ----
            LocationData locData = gl.GetData();
            if (locData != null)
            {
                Season season = gl.GetSeason();
                var possibleForage = new List<SpawnForageData>();

                foreach (var spawn in GameLocation.GetData("Default").Forage.Concat(locData.Forage))
                {
                    if (spawn.Condition != null &&
                        !GameStateQuery.CheckConditions(spawn.Condition, gl, null, null, null, Game1.random))
                        continue;

                    if (spawn.Season.HasValue && spawn.Season.Value != season)
                        continue;

                    possibleForage.Add(spawn);
                }

                if (possibleForage.Any())
                {
                    for (int i = 0; i < forages; i++)
                    {
                        if (!vertTiles.Any()) break;

                        int   idx = Game1.random.Next(vertTiles.Count);
                        var   v   = vertTiles[idx].ToVector2();
                        vertTiles.RemoveAt(idx);

                        var ctx   = new ItemQueryContext(gl, null, Game1.random, $"location '{gl.NameOrUniqueName}' > forage");
                        var pick  = RandomExtensions.ChooseFrom(Game1.random, possibleForage);
                        var item  = ItemQueryResolver.TryResolveRandomItem(pick, ctx, false, null, null, null,
                            (query, error) => { });

                        if (item is StardewValley.Object obj)
                        {
                            if (Config.Debug) SMonitor.Log($"Spawning forage at {v}", LogLevel.Trace);
                            gl.dropObject(obj, new Vector2(v.X * 64f, v.Y * 64f), Game1.viewport, true, null);
                        }
                    }
                }
            }

            // ---- Skattkistor (utan externt beroende) ----
            if (mazeData.TreasureLoot.Any())
            {
                for (int j = 0; j < treasures; j++)
                {
                    if (!endTiles.Any()) break;

                    int idx = Game1.random.Next(endTiles.Count);
                    var v   = endTiles[idx].ToVector2();
                    endTiles.RemoveAt(idx);

                    var chest = MakeSimpleChest(mazeData, v);
                    gl.overlayObjects[v] = chest;
                    inst.chestTiles.Add(v);

                    if (Config.Debug) SMonitor.Log($"Spawnar kista på {v}. Opening this chest will return the player to {RETURN_LOCATION} {RETURN_TILE.X},{RETURN_TILE.Y} after the chest menu closes.", LogLevel.Trace);
                }
            }

            // ---- Fairies ----
            for (int k = 0; k < fairies; k++)
            {
                if (!endTiles.Any()) break;

                int idx = Game1.random.Next(endTiles.Count);
                var v   = endTiles[idx].ToVector2() - new Vector2(0f, 1f);
                endTiles.RemoveAt(idx);
                inst.fairyTiles.Add(v);

                if (Config.Debug) SMonitor.Log($"Spawning fairy at {v}", LogLevel.Trace);
            }

            // ---- Optional Dwarf NPC ----
            if (endTiles.Any() && mazeData.AddDwarf)
            {
                int idx = Game1.random.Next(endTiles.Count);
                var v   = endTiles[idx].ToVector2();
                endTiles.RemoveAt(idx);

                gl.addCharacter(new NPC(
                    new AnimatedSprite("Characters\\Dwarf", 0, 16, 24),
                    v * 64f, gl.NameOrUniqueName, 2, "Dwarf", false,
                    Game1.content.Load<Texture2D>("Portraits\\Dwarf"))
                {
                    Breather = false
                });

                if (Config.Debug) SMonitor.Log($"Spawning dwarf at {v}", LogLevel.Trace);
            }

            // ---- Monsters ----
            SpawnMonsters(gl, openTiles, mazeData, slimes,    (pos) => new GreenSlime(pos, Game1.random.Next(mazeData.MineLevelMin, mazeData.MineLevelMax)), "slime");
            SpawnMonsters(gl, openTiles, mazeData, bats,      (pos) => new Bat(pos,        Game1.random.Next(mazeData.MineLevelMin, mazeData.MineLevelMax)), "bat");
            SpawnMonsters(gl, openTiles, mazeData, serpents,  (pos) => new Serpent(pos),   "serpent");
            SpawnMonsters(gl, openTiles, mazeData, brutes,    (pos) => new ShadowBrute(pos), "shadow brute");
            SpawnMonsters(gl, openTiles, mazeData, shamans,   (pos) => new ShadowShaman(pos), "shadow shaman");
            SpawnMonsters(gl, openTiles, mazeData, squids,    (pos) => new SquidKid(pos),  "squid kid");
            SpawnMonsters(gl, openTiles, mazeData, skeletons, (pos) => new Skeleton(pos, false), "skeleton");
            SpawnMonsters(gl, openTiles, mazeData, dusts,     (pos) => new DustSpirit(pos), "dust sprite");
        }

        /// <summary>
        /// Generic helper that picks random open tiles and spawns monsters onto them.
        /// </summary>
        private static void SpawnMonsters(
            GameLocation gl, List<Point> openTiles, MazeData data,
            int count, Func<Vector2, Monster> factory, string label)
        {
            for (int i = 0; i < count; i++)
            {
                if (!openTiles.Any()) break;

                int idx = Game1.random.Next(openTiles.Count);
                var v   = openTiles[idx].ToVector2() * 64f;
                openTiles.RemoveAt(idx);

                gl.characters.Add(factory(v));

                if (Config.Debug) SMonitor.Log($"Spawning {label} at {v}", LogLevel.Trace);
            }
        }

        // =========================================================================
        // Map modification (tile drawing)
        // =========================================================================

        private static bool InLayerBounds(Layer layer, int x, int y)
        {
            return layer != null
                && x >= 0
                && y >= 0
                && x < layer.LayerSize.Width
                && y < layer.LayerSize.Height;
        }

        private static string DescribeTileIndex(int tileIndex)
        {
            return tileIndex switch
            {
                300 => "floor variant",
                304 => "floor variant",
                305 => "floor variant",
                351 => "main floor",
                659 => "wall base left-edge",
                660 => "wall base middle",
                661 => "wall base right/standalone",
                563 => "wall top connector",
                565 => "wall top side",
                597 => "wall top vertical cap",
                626 => "wall top upper middle",
                627 => "wall top upper left",
                628 => "wall top lower middle",
                629 => "wall top upper/right cap",
                665 => "wall top bottom cap",
                946 => "hidden/fog bush overlay",
                599 => "torch frame 1",
                600 => "torch frame 2",
                601 => "torch frame 3",
                _ => "unknown/extra"
            };
        }

        private static void SetStaticTileSafe(Layer layer, int x, int y, TileSheet sheet, int tileIndex)
        {
            if (tileIndex < 0 || !InLayerBounds(layer, x, y))
                return;

            layer.Tiles[x, y] = new StaticTile(layer, sheet, 0, tileIndex);

            if (Config != null && Config.Debug)
                tileWriteReport.Add($"{layer.Id} | {x},{y} | {tileIndex} | {DescribeTileIndex(tileIndex)}");
        }

        private static void SetTileSafe(Layer layer, int x, int y, Tile tile)
        {
            if (!InLayerBounds(layer, x, y))
                return;

            layer.Tiles[x, y] = tile;
        }

        /// <summary>
        /// Paints the maze walls, floor variation, and optional fog overlay
        /// directly onto the map's tile layers.
        /// </summary>
        private static void ModifyMap(Map map, MazeInstance inst)
        {
            MazeData data = mazeDataDict[inst.id];

            // Make sure the map is large enough to hold the maze
            ExtendMap(map, data.corner.X + data.mapSize.X, data.corner.Y + data.mapSize.Y);

            // Add/reuse the floor tilesheet if it isn't already on the map.
            if (map.GetTileSheet(TILESHEET_MAIN_ID) == null)
            {
                var mainSheet = new TileSheet(
                    TILESHEET_MAIN_ID, map,
                    TILESHEET_MAIN_PATH,
                    TILESHEET_SHEET_SIZE,
                    TILESHEET_TILE_SIZE);
                map.AddTileSheet(mainSheet);
            }

            // Add/reuse the Festivals tilesheet for hedge walls, fog, and torches.
            // Prefer the existing TMX sheet named "Festivals" if your map already includes it.
            if (map.GetTileSheet(TILESHEET_FESTIVAL_ID) == null)
            {
                var newFestivalSheet = new TileSheet(
                    TILESHEET_FESTIVAL_ID, map,
                    TILESHEET_FESTIVAL_PATH,
                    TILESHEET_SHEET_SIZE,
                    TILESHEET_TILE_SIZE);
                map.AddTileSheet(newFestivalSheet);
            }

            TileSheet floorSheet = map.GetTileSheet(TILESHEET_MAIN_ID);
            TileSheet festivalSheet = map.GetTileSheet(TILESHEET_FESTIVAL_ID);

            SMonitor.Log(
                "Maze tile setup: floor tilesheet='" + TILESHEET_MAIN_ID + "', path='" + TILESHEET_MAIN_PATH + "'. " +
                "Hedge/fog/torch tilesheet='" + TILESHEET_FESTIVAL_ID + "', path='" + TILESHEET_FESTIVAL_PATH + "'. " +
                "Back floors=[300,304,305,351], Buildings wall bases=[659,660,661], " +
                "Front wall tops=[563,565,597,626,627,628,629,665], Front fog=[946], AlwaysFront torches=[599,600,601].",
                LogLevel.Info);

            if (Config.Debug)
            {
                tileWriteReport = new List<string>();
                tileWriteReport.Add("MazeQuest tile write report");
                tileWriteReport.Add("Map asset/path: " + map.Id);
                tileWriteReport.Add("Floor tilesheet ID: " + TILESHEET_MAIN_ID);
                tileWriteReport.Add("Floor tilesheet path: " + TILESHEET_MAIN_PATH);
                tileWriteReport.Add("Hedge/Festival tilesheet ID: " + TILESHEET_FESTIVAL_ID);
                tileWriteReport.Add("Hedge/Festival tilesheet path: " + TILESHEET_FESTIVAL_PATH);
                tileWriteReport.Add("Format: Layer | X,Y | TileIndex | Meaning");
            }

            Layer back        = map.GetLayer("Back");
            Layer buildings   = map.GetLayer("Buildings");
            Layer front       = map.GetLayer("Front");
            Layer alwaysFront = map.GetLayer("AlwaysFront");

            if (back == null || buildings == null || front == null)
            {
                SMonitor.Log("Cannot draw maze: map must have Back, Buildings, and Front layers.", LogLevel.Error);
                return;
            }

            if (alwaysFront == null)
            {
                alwaysFront = new Layer("AlwaysFront", map, back.LayerSize, back.TileSize);
                map.AddLayer(alwaysFront);
            }

            // Clear the maze rectangle first. Custom TMX maps often already have
            // Buildings/Front tiles; if we only paint the Back layer for open paths,
            // those old upper-layer tiles hide the generated maze.
            for (int clearY = data.corner.Y; clearY < data.corner.Y + data.mapSize.Y; clearY++)
            for (int clearX = data.corner.X; clearX < data.corner.X + data.mapSize.X; clearX++)
            {
                ClearTile(buildings, clearX, clearY);
                ClearTile(front, clearX, clearY);
                ClearTile(alwaysFront, clearX, clearY);

                // Wall tops are drawn one tile above the wall base, so clear that too.
                if (clearY > 0)
                {
                    ClearTile(front, clearX, clearY - 1);
                    ClearTile(alwaysFront, clearX, clearY - 1);
                }
            }

            SMonitor.Log($"Cleared existing upper-layer tiles in maze area {data.corner.X},{data.corner.Y} size {data.mapSize.X}x{data.mapSize.Y}.", LogLevel.Info);

            // Reset tile lists (filled during the loop below)
            inst.openTiles  = new List<Point>();
            inst.endTiles   = new List<Point>();
            inst.vertTiles  = new List<Point>();
            inst.fairyTiles = new List<Vector2>();

            // ---- Torch decoration at the entrance ----
            if (data.AddTorches)
            {
                // Uses tile indices 599–601 from the Festivals tilesheet for a
                // simple 3-frame animated torch. Adjust indices to match your sheet.
                var torchFrames = new StaticTile[]
                {
                    new StaticTile(alwaysFront, festivalSheet, 0, 599),
                    new StaticTile(alwaysFront, festivalSheet, 0, 600),
                    new StaticTile(alwaysFront, festivalSheet, 0, 601)
                };
                var torch = new AnimatedTile(alwaysFront, torchFrames, 100L);

                switch (data.entranceSide)
                {
                    case EntranceSide.Bottom:
                        SetTileSafe(alwaysFront, data.corner.X + data.entranceOffset - 1, data.corner.Y + data.mapSize.Y - 2, torch);
                        SetTileSafe(alwaysFront, data.corner.X + data.entranceOffset + 1, data.corner.Y + data.mapSize.Y - 2, torch);
                        break;
                    case EntranceSide.Left:
                        SetTileSafe(alwaysFront, data.corner.X, data.corner.Y + data.entranceOffset - 2, torch);
                        SetTileSafe(alwaysFront, data.corner.X, data.corner.Y + data.entranceOffset, torch);
                        break;
                    case EntranceSide.Right:
                        SetTileSafe(alwaysFront, data.corner.X + data.mapSize.X - 1, data.corner.Y + data.entranceOffset - 2, torch);
                        SetTileSafe(alwaysFront, data.corner.X + data.mapSize.X - 1, data.corner.Y + data.entranceOffset, torch);
                        break;
                    default: // Top
                        SetTileSafe(alwaysFront, data.corner.X + data.entranceOffset - 1, data.corner.Y - 1, torch);
                        SetTileSafe(alwaysFront, data.corner.X + data.entranceOffset + 1, data.corner.Y - 1, torch);
                        break;
                }
            }

            // ---- Main tile loop ----
            for (int y = 0; y < data.mapSize.Y; y++)
            for (int x = 0; x < data.mapSize.X; x++)
            {
                int tx = x + data.corner.X;
                int ty = y + data.corner.Y;

                // Fog-of-war: paint the "hidden" overlay on the Front layer
                bool hideTile = data.HideMaze && (data.HideBorders ||
                    (tx != data.corner.X &&
                     tx < data.corner.X + data.mapSize.X - 1 &&
                     ty < data.corner.Y + data.mapSize.Y - 2));

                if (hideTile)
                {
                    // Tile index 946 in the Festivals tilesheet is a solid dark-green
                    // bush tile used as the fog overlay.
                    SetStaticTileSafe(front, tx, ty, festivalSheet, 946);
                    try
                    {
                        if (data.HideBorders && y == 0 &&
                            InLayerBounds(front, tx, ty - 1) && !(front.Tiles[tx, ty - 1] is AnimatedTile))
                        {
                            SetStaticTileSafe(front, tx, ty - 1, festivalSheet, 946);
                        }
                    }
                    catch { /* tile above may be out of bounds */ }
                }

                bool left  = x > 0                    && !inst.tiles[x - 1, y];
                bool right = x < data.mapSize.X - 2   && !inst.tiles[x + 1, y];
                bool up    = y > 0                    && !inst.tiles[x,     y - 1];
                bool down  = y < data.mapSize.Y - 2   && !inst.tiles[x,     y + 1];

                if (!inst.tiles[x, y])
                {
                    // Wall tile
                    int[] wallIdx = GetWallTiles(left, right, up, down);

                    if (wallIdx[0] > -1)
                        SetStaticTileSafe(buildings, tx, ty, festivalSheet, wallIdx[0]);

                    if (wallIdx[1] > -1)
                    {
                        Tile above = InLayerBounds(front, tx, ty - 1) ? front.Tiles[tx, ty - 1] : null;
                        if (above == null || above.TileIndex != 946)
                            SetStaticTileSafe(front, tx, ty - 1, festivalSheet, wallIdx[1]);
                    }
                }
                else
                {
                    // Floor tile — slight random variation for visual interest
                    double roll = Game1.random.NextDouble();
                    int floorIdx;
                    if      (roll < 0.025) floorIdx = 304;
                    else if (roll < 0.050) floorIdx = 305;
                    else if (roll < 0.150) floorIdx = 300;
                    else                   floorIdx = 351;

                    SetStaticTileSafe(back, tx, ty, floorSheet, floorIdx);

                    inst.openTiles.Add(new Point(tx, ty));

                    // Dead-end tiles (three sides blocked) — good spots for chests/fairies
                    bool isDeadEnd = (!down && up  && left && right)
                                  || ( down && !up && left && right)
                                  || ( down && up  && !left && right)
                                  || ( down && up  && left && !right);

                    // Vertical corridor (no up/down neighbours) — good for forage
                    if (!down && !up)
                        inst.vertTiles.Add(new Point(tx, ty));
                    else if (isDeadEnd)
                        inst.endTiles.Add(new Point(tx, ty));
                }
            }

            if (Config.Debug)
            {
                try
                {
                    string reportPath = Path.Combine(SHelper.DirectoryPath, "MazeQuest_tile_report.txt");
                    File.WriteAllLines(reportPath, tileWriteReport);
                    SMonitor.Log($"Wrote tile report to {reportPath}", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    SMonitor.Log($"Could not write tile report: {ex.Message}", LogLevel.Warn);
                }
            }
        }

        private static bool IsInLayerBounds(Layer layer, int x, int y)
        {
            return layer != null
                && x >= 0
                && y >= 0
                && x < layer.LayerSize.Width
                && y < layer.LayerSize.Height;
        }

        private static void ClearTile(Layer layer, int x, int y)
        {
            if (IsInLayerBounds(layer, x, y))
                layer.Tiles[x, y] = null;
        }

        private static Tile GetTile(Layer layer, int x, int y)
        {
            return IsInLayerBounds(layer, x, y) ? layer.Tiles[x, y] : null;
        }

        private static void SetTile(Layer layer, int x, int y, Tile tile)
        {
            if (IsInLayerBounds(layer, x, y))
                layer.Tiles[x, y] = tile;
        }

        // =========================================================================
        // Wall tile selector
        // =========================================================================

        /// <summary>
        /// Returns [buildingLayerIndex, frontLayerIndex] for a wall cell based on
        /// which cardinal directions are also walls.
        /// Back/floor indices reference spring_outdoorsTileSheet; hedge, fog, and torch indices reference Festivals.
        /// </summary>
        private static int[] GetWallTiles(bool left, bool right, bool up, bool down)
        {
            int[] w = { -1, -1 };

            if (left)
            {
                if (right)
                {
                    w[0] = 660;
                    w[1] = up ? (down ? 563 : 626) : (down ? 563 : 628);
                }
                else
                {
                    w[0] = 661;
                    w[1] = up ? (down ? 565 : 629) : (down ? 565 : 629);
                }
            }
            else if (right)
            {
                w[0] = 659;
                w[1] = up ? (down ? 563 : 627) : (down ? 563 : 627);
            }
            else
            {
                w[0] = 661;
                w[1] = up ? (down ? 597 : 629) : (down ? 565 : 665);
            }

            return w;
        }

        // =========================================================================
        // Maze generation (recursive backtracker)
        // =========================================================================

        /// <summary>
        /// Builds the boolean tile array for a maze using a recursive backtracker.
        /// true = passable (floor), false = wall.
        /// </summary>
        public static bool[,] MakeMapArray(MazeData data, MazeInstance inst)
        {
            Point mapSize = data.mapSize;
            bool[,] map = new bool[mapSize.X, mapSize.Y];

            var checkedTiles  = new List<Point>();
            var checkingTiles = new List<Point>();
            CheckTile(ref map, checkedTiles, checkingTiles, new Point(1, 1), mapSize);

            // Cut the entrance opening(s) into the border
            if (data.entranceOffset > -1)
            {
                switch (data.entranceSide)
                {
                    case EntranceSide.Bottom:
                        map[data.entranceOffset, mapSize.Y - 1] = true;
                        map[data.entranceOffset, mapSize.Y - 2] = true;
                        break;
                    case EntranceSide.Left:
                        map[0, data.entranceOffset]     = true;
                        map[0, data.entranceOffset + 1] = true;
                        break;
                    case EntranceSide.Right:
                        map[mapSize.X - 1, data.entranceOffset] = true;
                        map[mapSize.X - 2, data.entranceOffset] = true;
                        break;
                    default: // Top
                        map[data.entranceOffset, 0] = true;
                        map[data.entranceOffset, 1] = true;
                        break;
                }
            }
            else
            {
                foreach (int i in data.topEntranceOffsets)    { map[i, 0] = true; map[i, 1] = true; }
                foreach (int i in data.rightEntranceOffsets)  { map[mapSize.X - 1, i] = true; map[mapSize.X - 2, i] = true; }
                foreach (int i in data.leftEntranceOffsets)   { map[0, i] = true; map[1, i] = true; }
                foreach (int i in data.bottomEntranceOffsets) { map[i, mapSize.Y - 1] = true; map[i, mapSize.Y - 2] = true; }
            }

            // Debug: dump the maze to a text file so you can inspect it in-editor
            if (Config.Debug)
            {
                var lines = new List<string>();
                for (int y = 0; y < mapSize.Y; y++)
                {
                    string line = "";
                    for (int x = 0; x < mapSize.X; x++)
                        line += map[x, y] ? " " : "#";
                    lines.Add(line);
                }
                File.WriteAllLines(Path.Combine(SHelper.DirectoryPath, "maze_debug.txt"), lines);
            }

            return map;
        }

        private static void CheckTile(
            ref bool[,] map, List<Point> checkedTiles,
            List<Point> checkingTiles, Point tile, Point mapSize)
        {
            map[tile.X, tile.Y] = true;

            var dirs = neighbours.ToList();
            ShuffleList(dirs);

            for (int i = 0; i < 4; i++)
            {
                Point next = tile + dirs[i];
                if (IsInMap(next, mapSize)
                    && !checkedTiles.Contains(next)
                    && !checkingTiles.Contains(next))
                {
                    // Carve through the wall between tile and next
                    map[tile.X + dirs[i].X / 2, tile.Y + dirs[i].Y / 2] = true;
                    checkingTiles.Add(tile);
                    CheckTile(ref map, checkedTiles, checkingTiles, next, mapSize);
                    return;
                }
            }

            checkedTiles.Add(tile);
            if (checkingTiles.Any())
            {
                tile = checkingTiles[checkingTiles.Count - 1];
                checkingTiles.RemoveAt(checkingTiles.Count - 1);
                CheckTile(ref map, checkedTiles, checkingTiles, tile, mapSize);
            }
        }

        // =========================================================================
        // Utility helpers
        // =========================================================================

        private static bool IsTileOnMaze(Point n, Point mapSize) =>
            n.X >= 0 && n.X < mapSize.X && n.Y >= 0 && n.Y < mapSize.Y;

        private static bool IsInMap(Point n, Point mapSize) =>
            n.X > 0 && n.X < mapSize.X - 1 && n.Y > 0 && n.Y < mapSize.Y - 1;

        private static bool IsTileInMaze(Point n, Point mapSize, Point corner) =>
            n.X > corner.X && n.X < corner.X + mapSize.X &&
            n.Y > corner.Y && n.Y < corner.Y + mapSize.Y;

        public static void ShuffleList<T>(List<T> list)
        {
            Random rng = mazeGenerationRandom ?? Game1.random;
            int i = list.Count;
            while (i > 1)
            {
                i--;
                int j = rng.Next(i + 1);
                (list[j], list[i]) = (list[i], list[j]);
            }
        }

        private static int GetStableMazeSeed(string mazeId)
        {
            unchecked
            {
                int seed = Game1.uniqueIDForThisGame.GetHashCode();
                seed = seed * 397 + Game1.Date.TotalDays;
                foreach (char c in mazeId)
                    seed = seed * 397 + c;
                return seed;
            }
        }

        /// <summary>
        /// Skapar en kista med slumpmässiga föremål från MazeData.TreasureLoot
        /// och ett slumpmässigt antal guldmynt — inga externa beroenden.
        /// </summary>
        private static Chest MakeSimpleChest(MazeData data, Vector2 tile)
        {
            var items = new List<Item>();

            // Välj ett slumpmässigt antal föremål ur lootlistan
            int count = Game1.random.Next(data.ChestItemMin, data.ChestItemMax + 1);
            var pool  = data.TreasureLoot.ToList();

            for (int i = 0; i < count && pool.Any(); i++)
            {
                int   pick = Game1.random.Next(pool.Count);
                string id  = pool[pick];
                pool.RemoveAt(pick); // ta inte samma föremål två gånger

                var item = ItemRegistry.Create(id, 1);
                if (item != null) items.Add(item);
            }

            int coins = data.CoinBaseMax > 0
                ? Game1.random.Next(data.CoinBaseMin, data.CoinBaseMax + 1)
                : 0;

            var chest = new Chest(true, tile);
            chest.Items.AddRange(items);
            chest.CanBeGrabbed = false;
            if (coins > 0)
                chest.modData[CHEST_MONEY_MODDATA_KEY] = coins.ToString();
            return chest;
        }

        private static Color MakeTint(double fraction) =>
            tintColors[(int)Math.Floor(fraction * tintColors.Length)];

        /// <summary>
        /// Expands all layers of a map to at least (x, y) tiles using
        /// Harmony reflection (the same technique as the original mod).
        /// </summary>
        private static void ExtendMap(Map map, int x, int y)
        {
            SMonitor.Log($"Extending map to {x}x{y}", LogLevel.Trace);

            var layers = AccessTools.Field(typeof(Map), "m_layers").GetValue(map) as List<Layer>;
            for (int i = 0; i < layers.Count; i++)
            {
                var tiles = AccessTools.Field(typeof(Layer), "m_tiles").GetValue(layers[i]) as Tile[,];
                var size  = (Size)AccessTools.Field(typeof(Layer), "m_layerSize").GetValue(layers[i]);

                if (size.Width >= x && size.Height >= y) continue;

                int newW = size.Width  >= x ? size.Width  : x;
                int newH = size.Height >= y ? size.Height : y;

                var newSize  = new Size(newW, newH);
                AccessTools.Field(typeof(Layer), "m_layerSize").SetValue(layers[i], newSize);

                var newTiles = new Tile[newW, newH];
                for (int j = 0; j < tiles.GetLength(0); j++)
                for (int k = 0; k < tiles.GetLength(1); k++)
                    newTiles[j, k] = tiles[j, k];

                AccessTools.Field(typeof(Layer), "m_tiles").SetValue(layers[i], newTiles);
                AccessTools.Field(typeof(Layer), "m_tileArray").SetValue(layers[i], new TileArray(layers[i], newTiles));
            }

            AccessTools.Field(typeof(Map), "m_layers").SetValue(map, layers);
        }

        // =========================================================================
        // Harmony patches
        // =========================================================================

        /// <summary>
        /// Farmer_Update_Patch: when HideMaze is active, reveals tiles as the
        /// player walks near them by removing the fog-of-war overlay.
        /// </summary>
        [HarmonyPatch(typeof(Farmer), "Update")]
        public class Farmer_Update_Patch
        {
            public static void Postfix(Farmer __instance)
            {
                if (!Config.ModEnabled) return;
                if (!mazeLocationDict.TryGetValue(__instance.currentLocation.NameOrUniqueName, out var list)) return;

                Point tile  = __instance.TilePoint;
                Layer front = __instance.currentLocation.Map.GetLayer("Front");
                if (front == null) return;

                foreach (var inst in list)
                {
                    MazeData data = mazeDataDict[inst.id];
                    if (!IsTileInMaze(tile, data.mapSize, data.corner)) continue;

                    Point farmerMazeTile = tile - data.corner;

                    foreach (Point t in surrounding)
                    {
                        Point thisTile = farmerMazeTile + t;
                        if (!IsTileOnMaze(thisTile, data.mapSize)) continue;

                        // Don't reveal the tile directly below if it's a wall floor
                        if (t.Y == 1 && !inst.tiles[farmerMazeTile.X, farmerMazeTile.Y + 1]) continue;

                        Tile frontTile = front.Tiles.Array[t.X + tile.X, t.Y + tile.Y];
                        if (!(frontTile is StaticTile) || frontTile.TileIndex != 946) continue;

                        // Work out what the real front tile should be (hedge top graphic)
                        Tile newTile = null;
                        Point mazeTile  = farmerMazeTile + t;
                        Point belowTile = mazeTile + new Point(0, 1);

                        if (belowTile.Y < inst.tiles.GetLength(1) && !inst.tiles[belowTile.X, belowTile.Y])
                        {
                            try
                            {
                                bool bLeft  = belowTile.X > 0                    && !inst.tiles[belowTile.X - 1, belowTile.Y];
                                bool bRight = belowTile.X < data.mapSize.X - 2   && !inst.tiles[belowTile.X + 1, belowTile.Y];
                                bool bUp    = belowTile.Y > 0                    && !inst.tiles[belowTile.X,     belowTile.Y - 1];
                                bool bDown  = belowTile.Y < data.mapSize.Y - 2   && !inst.tiles[belowTile.X,     belowTile.Y + 1];

                                int idx = GetWallTiles(bLeft, bRight, bUp, bDown)[1];
                                if (idx > -1)
                                {
                                    var sheet = __instance.currentLocation.Map.GetTileSheet(TILESHEET_FESTIVAL_ID);
                                    newTile = new StaticTile(front, sheet, 0, idx);
                                }
                            }
                            catch { /* out-of-bounds edge case */ }
                        }

                        front.Tiles.Array[t.X + tile.X, t.Y + tile.Y] = newTile;
                    }
                }
            }
        }

        /// <summary>
        /// GameLocation_draw_Patch: draws the animated fairy sprites on top of the map.
        /// </summary>
        [HarmonyPatch(typeof(GameLocation), "draw", new Type[] { typeof(SpriteBatch) })]
        public class GameLocation_draw_Patch
        {
            public static void Postfix(GameLocation __instance, SpriteBatch b)
            {
                if (!Config.ModEnabled) return;
                if (!mazeLocationDict.TryGetValue(__instance.NameOrUniqueName, out var list)) return;

                fairyFrame = (fairyFrame + 1) % 32;

                foreach (var inst in list)
                {
                    Layer front = __instance.map.GetLayer("Front");
                    foreach (Vector2 f in inst.fairyTiles)
                    {
                        var tile = front.PickTile(new Location((int)f.X * 64, (int)f.Y * 64), Game1.viewport.Size);
                        if (tile != null && tile.TileIndex == 946) continue; // still hidden

                        b.Draw(
                            Game1.mouseCursors,
                            Game1.GlobalToLocal(Game1.viewport, f * 64f),
                            new Microsoft.Xna.Framework.Rectangle(16 + fairyFrame / 8 * 16, 592, 16, 16),
                            Color.White, 0f, Vector2.Zero, 4f,
                            SpriteEffects.None, 0.9999999f);
                    }
                }
            }
        }

        /// <summary>
        /// GameLocation_checkAction_Patch: touching a fairy tile restores HP and stamina.
        /// </summary>
        [HarmonyPatch(typeof(GameLocation), "checkAction")]
        public class GameLocation_checkAction_Patch
        {
            public static bool Prefix(GameLocation __instance, Location tileLocation, Farmer who, ref bool __result)
            {
                if (!Config.ModEnabled) return true;
                if (!mazeLocationDict.TryGetValue(__instance.NameOrUniqueName, out var list)) return true;

                var tv = new Vector2(tileLocation.X, tileLocation.Y);

                foreach (var inst in list)
                {
                    if (inst.chestTiles.Contains(tv)
                        && __instance.overlayObjects.TryGetValue(tv, out StardewValley.Object obj)
                        && obj is Chest chest)
                    {
                        if (chest.modData.TryGetValue(CHEST_MONEY_MODDATA_KEY, out string rawCoins)
                            && int.TryParse(rawCoins, out int coins)
                            && coins > 0)
                        {
                            who.Money += coins;
                            chest.modData.Remove(CHEST_MONEY_MODDATA_KEY);
                            Game1.dayTimeMoneyBox.moneyShakeTimer = 1000;
                        }

                        pendingMazeChestReturnWarp = true;
                        pendingMazeChestMenuWasOpen = false;
                        pendingMazeChestReturnWarpDelayTicks = 15;
                        if (Config.Debug) SMonitor.Log($"Maze chest opened at {tv}. Return warp armed for {RETURN_LOCATION} {RETURN_TILE.X},{RETURN_TILE.Y} after the chest menu opens and closes.", LogLevel.Debug);
                        return true; // let vanilla open the chest first
                    }

                    foreach (Vector2 f in inst.fairyTiles)
                    {
                        if (f != tv) continue;

                        __instance.localSound("yoba");
                        who.health  = who.maxHealth;
                        who.stamina = who.MaxStamina;
                        inst.fairyTiles.Remove(f);
                        __result = true;
                        return false; // skip vanilla checkAction
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// NPC_checkAction_Patch: lets the player open the Dwarf shop
        /// when talking to the Dwarf spawned inside the maze.
        /// </summary>
        [HarmonyPatch(typeof(NPC), "checkAction")]
        public class NPC_checkAction_Patch
        {
            public static bool Prefix(NPC __instance, Farmer who, GameLocation l, ref bool __result)
            {
                if (!Config.ModEnabled
                    || !__instance.Name.Equals("Dwarf")
                    || l is Mine
                    || !who.canUnderstandDwarves)
                    return true;

                Utility.TryOpenShopMenu("Dwarf", __instance.Name, true);
                __result = true;
                return false;
            }
        }
    }
}
