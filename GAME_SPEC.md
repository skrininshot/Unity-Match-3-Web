# GAME SPEC — Match-3 Web Game

## Goal

Create a polished Match-3 game for Web.

The result should be:

* fully playable;
* visually polished;
* technically well-structured;
* maintainable and extensible;
* implemented with high-quality architecture and code.

Prioritize the quality of the final game over unnecessary complexity.

---

# 1. Core Match-3

Implement the core Match-3 gameplay:

* game board;
* colored pieces;
* matching;
* removal of matched pieces;
* pieces falling to fill empty spaces;
* spawning of new pieces.

Use successful Match-3 games such as Gardenscapes as a general gameplay reference.

## Automatic matches

The initial board must be random but must contain no automatic matches.

If falling pieces accidentally create a match of 3 or more, resolve it using the normal match rules.

Cascading matches must work correctly.

## Piece animations

Implement simple but polished effects for:

### Falling

Pieces should fall naturally:

* from outside the board when newly spawned;
* into empty spaces created below them.

### Removal

Matched pieces should have a simple visual removal effect, such as shrinking, fading, or a combination.

Pieces must not start falling until the removal effect has completed.

---

# 2. Level Goals

Each level has a goal:

> Collect a specified number of pieces of a specified color.

When the goal is completed, restart the level.

The following parameters must be configurable per level:

* board size;
* number of piece colors;
* target color;
* target quantity.

Level configuration should be designed so that these values can be changed without modifying gameplay code.

Art may be demo art or custom-created art.

---

# 3. Level System — Additional Task

Implement a proper level system.

There should be at least **10 levels**.

Each level must have a configurable move limit.

Implement:

* level victory;
* level defeat;
* progression to the next level after victory;
* restarting the current level after defeat;
* level map/progression;
* configurable board layout;
* ability to place a specific-colored piece on the map;
* ability to place a random-colored piece on the map.

A dedicated level editor is not required.

The level infrastructure should support:

* loading additional levels;
* replacing/reloading a level without restarting the game.

---

# 4. Boosters — Additional Task

Implement Match-3 boosters.

Use successful Match-3 games such as Homescapes or Royal Match as general gameplay references.

## Line

Created by matching 4 pieces.

When activated, destroys a line.

## Bomb

Created by a double match forming a corner.

When activated, destroys a 5×5 area.

## Rainbow Ball

Created by matching 5 pieces.

When activated, destroys pieces of one selected color.

## Plane

Created by matching a 2×2 square.

When activated, intelligently destroys an element on the board that helps progress toward the current level goal.

## Booster requirements

Implement:

* simple activation effects for all boosters;
* all booster combinations, including:

  * line + line;
  * line + bomb;
  * and other possible combinations;
* natural chain reactions when one booster activates another booster.

The booster system should be designed so that additional booster types can be added without major changes to existing gameplay code.

---

# 5. Board Elements — Additional Task

Implement extensible infrastructure for special board elements such as boxes.

The infrastructure should make it reasonably easy to introduce additional elements with similar mechanics later.

Implement these box types:

### Normal box

Destroyed by any adjacent match.

### Colored box

Destroyed only by an adjacent match of the corresponding color.

### Changing-color box

Changes its required color every turn.

### Blocker

Cannot be destroyed.

## Box properties

Any box type may support:

* containing another element, including another box;
* multiple lives / requiring multiple hits to destroy;
* occupying more than one board cell.

The architecture should support these properties without requiring separate implementations for every possible combination.

---

# Priority

All requirements above are part of the desired result.

The additional tasks (Levels, Boosters, and Board Elements) should be implemented **as fully as reasonably possible**.

If completing every additional task would significantly compromise the quality, stability, polish, or core gameplay, prioritize:

1. Core Match-3 gameplay
2. Level system
3. Boosters
4. Board elements

Do not sacrifice a polished and reliable core game merely to maximize the number of features.

---

# Quality Expectations

The final result should demonstrate:

* enjoyable and responsive gameplay;
* polished basic visual feedback and animations;
* clean and understandable architecture;
* maintainable code;
* extensibility where the specification explicitly requires it;
* reliable handling of cascades and chain reactions;
* configurable level data;
* a stable Web build.

Use your own judgment to determine the best architecture, implementation strategy, tools, and development process.

Do not treat this specification as prescribing how the implementation must be organized. It defines the desired result; determine the best way to achieve it.