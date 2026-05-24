namespace MazeQuest
{
    public class ModConfig
    {
        /// <summary>Master switch — set to false to disable the mod entirely.</summary>
        public bool ModEnabled { get; set; } = true;

        /// <summary>
        /// When true:
        ///  - Pressing T in-game repopulates mazes immediately (useful for testing).
        ///  - A maze_debug.txt is written to the mod folder showing the generated layout.
        ///  - Extra log lines appear in SMAPI's console.
        /// </summary>
        public bool Debug { get; set; } = false;
    }
}
