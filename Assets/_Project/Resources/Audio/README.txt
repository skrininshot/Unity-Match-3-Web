Drop audio clips in this folder to wire them into the game -- nothing else needs to change.
AudioService (Assets/_Project/Code/Presentation/Audio/AudioService.cs) looks each of these up by
name at the moment it's needed; a missing file is silently skipped, so the game plays fine with
none of them present.

Supported formats: whatever Unity's importer accepts for the target platform -- .ogg or .wav are
the safe, well-supported choices for WebGL.

Expected file names (extension doesn't matter, Unity strips it):

  music.<ext>            Looping background track. Starts once at boot and keeps playing across
                          the map and every level.
  swap.<ext>              A player-initiated swap is accepted (match, booster combo, or a booster
                          relocated onto a cell).
  match.<ext>              A cascade round clears pieces with no booster involved. Fires once per
                          round, not once per piece, so cascades don't turn into a flurry of
                          overlapping copies of the same sound.
  booster_line.<ext>      A Line booster fires.
  booster_bomb.<ext>       A Bomb booster fires.
  booster_rainbow.<ext>    A Rainbow booster fires.
  booster_plane.<ext>      A Plane booster fires.
  victory.<ext>            A level is won.
  defeat.<ext>             A level is lost.
  ui_click.<ext>           Reserved for menu/HUD buttons; not wired to any button yet.
