using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MazeQuest
{
    /// <summary>
    /// Runtime state for one generated maze.
    /// Created by ModEntry.ReloadMazes() and discarded on DayEnding.
    /// </summary>
    public class MazeInstance
    {
        /// <summary>Key that maps back to the MazeData dictionary entry.</summary>
        public string id;

        /// <summary>The primary map asset path of the GameLocation this maze lives on.</summary>
        public string mapPath;

        /// <summary>All possible map asset paths this maze may be loaded through.</summary>
        public List<string> mapPaths = new List<string>();

        /// <summary>Tiles where fairy sprites are currently active.</summary>
        public List<Vector2> fairyTiles = new List<Vector2>();

        /// <summary>All passable floor tiles — used for monster spawning.</summary>
        public List<Point> openTiles = new List<Point>();

        /// <summary>Dead-end tiles (3 walls, 1 opening) — good for chests and fairies.</summary>
        public List<Point> endTiles = new List<Point>();

        /// <summary>Tiles where maze treasure chests were spawned.</summary>
        public List<Vector2> chestTiles = new List<Vector2>();

        /// <summary>Vertical corridor tiles (no up/down neighbours) — good for forage.</summary>
        public List<Point> vertTiles = new List<Point>();

        /// <summary>
        /// The boolean tile array.
        /// true  = passable floor
        /// false = wall
        /// Indexed [x, y] relative to the maze origin (not the map corner).
        /// </summary>
        public bool[,] tiles;
    }
}
