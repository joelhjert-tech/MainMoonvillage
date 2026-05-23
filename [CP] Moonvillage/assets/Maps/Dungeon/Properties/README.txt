Place the individual *_Properties.json files in:
[CP] Moonvillage/assets/Maps/Dungeon/Properties/

Then add this include to [CP] Moonvillage/content.json inside Changes:
{
  "LogName": "Load Moon Dungeon map properties",
  "Action": "Include",
  "FromFile": "assets/Maps/Dungeon/Properties/MoonDungeonMapProperties_All.json"
}

These patches set:
spacechase0.SpaceCore_DungeonLadderEntrance = 5 5
spacechase0.SpaceCore_DungeonElevatorEntrance = 5 5

Adjust 5 5 if the safe spawn tile on your dungeon maps is different.
