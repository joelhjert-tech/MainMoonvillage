using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MazeQuest
{
    /// <summary>
    /// Konfiguration för en enskild labyrint.
    /// Registrera en post under din nyckel i innehållsordboken:
    ///   "YourName.MazeQuest/dictionary"
    /// </summary>
    public class MazeData
    {
        // ------------------------------------------------------------------
        // Plats och geometri
        // ------------------------------------------------------------------

        /// <summary>
        /// Internt namn på GameLocation där labyrinten finns.
        /// Om null används ordbokens nyckel som platsnamn.
        /// </summary>
        public string gameLocation;

        /// <summary>
        /// Map asset path to edit, e.g. "Maps/Custom_Maze".
        /// If empty, the mod uses "Maps/" + gameLocation.
        /// This avoids waiting for the GameLocation to exist before the map can be patched.
        /// </summary>
        public string mapAsset;

        /// <summary>
        /// Optional fallback map asset paths to try. Useful when Content Patcher
        /// custom location name and TMX source filename differ.
        /// Example: ["Maps/Custom_Maze", "Maps/Mazequest", "assets/Maps/Mazequest.tmx"].
        /// </summary>
        public List<string> mapAssets = new List<string>();

        /// <summary>
        /// Övre vänstra hörnet av labyrintens yta på kartan (0-baserat).
        /// För en 50x50-karta som täcker hela kartan, sätt {0, 0}.
        /// </summary>
        public Point corner = new Point(0, 0);

        /// <summary>
        /// Bredd och höjd på labyrinten i brickor.
        /// Måste vara udda; jämna tal minskas automatiskt med 1.
        /// En 50-bricks karta kläms till 49x49.
        /// </summary>
        public Point mapSize = new Point(49, 49);

        // ------------------------------------------------------------------
        // Ingång
        // ------------------------------------------------------------------

        public EntranceSide entranceSide    = EntranceSide.Top;
        public int          entranceOffset  = 24;

        public List<int> topEntranceOffsets    = new List<int>();
        public List<int> rightEntranceOffsets  = new List<int>();
        public List<int> leftEntranceOffsets   = new List<int>();
        public List<int> bottomEntranceOffsets = new List<int>();

        // ------------------------------------------------------------------
        // Visuella alternativ
        // ------------------------------------------------------------------

        public bool HideMaze    = false;
        public bool HideBorders = false;
        public bool AddTorches  = false;
        public bool AddDwarf    = false;

        // ------------------------------------------------------------------
        // Skattkistor — inga externa beroenden krävs
        //
        // TreasureLoot är en lista med föremåls-ID:n (samma format som
        // stardew Item-ID:n, t.ex. "(O)72" för diamant, "(W)4" för fisksvärd).
        // Vid spawning plockas ett slumpmässigt antal föremål ur listan och
        // placeras i en kista. Lämna listan tom för inga kistor.
        //
        // Exempel-ID:n:
        //   "(O)72"   = Diamant
        //   "(O)60"   = Smaragd
        //   "(O)749"  = Omni Geode
        //   "(O)535"  = Geode
        //   "(O)432"  = Spice Berry (frön/bär-exempel)
        //   "(W)4"    = Fisksvärd (vapen)
        //   "(B)506"  = Gummisstövlar (skor)
        //   "(H)27"   = Cowboy Hat (hatt)
        //   "(O)645"  = Iridium Sprinkler
        // ------------------------------------------------------------------

        /// <summary>
        /// Lista med föremåls-ID:n att välja bland när en kista fylls.
        /// Använd stardews kvalificerade format: "(O)ID", "(W)ID", "(B)ID" osv.
        /// </summary>
        public List<string> TreasureLoot = new List<string>
        {
            "(O)72",   // Diamant
            "(O)60",   // Smaragd
            "(O)62",   // Akvamarin
            "(O)64",   // Rubin
            "(O)66",   // Ametist
            "(O)68",   // Topas
            "(O)749",  // Omni Geode
            "(O)535",  // Geode
            "(O)536",  // Frozen Geode
            "(O)537",  // Magma Geode
            "(O)645",  // Iridium Sprinkler
            "(O)432",  // Spice Berry
            "(W)4",    // Fisksvärd
            "(H)27",   // Cowboy Hat
            "(B)506"   // Gummisstövlar
        };

        /// <summary>Minsta antal föremål i varje kista.</summary>
        public int ChestItemMin = 1;

        /// <summary>Högsta antal föremål i varje kista.</summary>
        public int ChestItemMax = 4;

        /// <summary>Lägsta mängd guld att lägga i kistor (0 = ingen guld).</summary>
        public int CoinBaseMin = 50;

        /// <summary>Högsta mängd guld att lägga i kistor.</summary>
        public int CoinBaseMax = 300;

        // Mingrupp-nivå för nivåskalade fiender (slimes, fladdermöss)
        public int MineLevelMin = 10;
        public int MineLevelMax = 100;

        // ------------------------------------------------------------------
        // Spawnantal (Min = 0, Max = 0 = ingen av den typen)
        // ------------------------------------------------------------------

        public int FairiesMin     = 0; public int FairiesMax     = 0;
        public int TreasureMin    = 0; public int TreasureMax    = 0;
        public int ForageMin      = 0; public int ForageMax      = 0;
        public int SlimeMin       = 0; public int SlimeMax       = 0;
        public int SerpentMin     = 0; public int SerpentMax     = 0;
        public int BatMin         = 0; public int BatMax         = 0;
        public int ShadowBruteMin = 0; public int ShadowBruteMax = 0;
        public int ShadowShamanMin= 0; public int ShadowShamanMax= 0;
        public int SquidMin       = 0; public int SquidMax       = 0;
        public int SkeletonMin    = 0; public int SkeletonMax    = 0;
        public int DustSpriteMin  = 0; public int DustSpriteMax  = 0;
    }
}
