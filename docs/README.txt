VESPERFELL - The Silent Bell                                             v2.0
=============================================================================
A dark-fantasy action RPG demo for Windows 10 / 11 (64-bit).

For four hundred years the Vesper Bell rang at the close of every day and kept
the Gloam on its own side of the vale. Thirty nights ago it fell silent, and the
dusk has not lifted since. Walk the vale through your hero's eyes, fight what
came out of the dark, and find out who silenced the bell.

(Russian: see README_RU.txt.  По-русски: см. README_RU.txt)


-----------------------------------------------------------------------------
REQUIREMENTS
-----------------------------------------------------------------------------
  - Windows 10 or 11, 64-bit. Nothing to install: the game needs only the
    .NET Framework 4.x that ships with Windows.
  - For the 3D view: a graphics card with OpenGL 3.3 (any GeForce, Radeon or
    Intel graphics from roughly the last ten years). Without it the game starts
    in its 2D view on its own.
  - Measured on a GeForce RTX 2060: about 6-7 ms per frame at 1280 x 720 in
    first person, so it holds 60 frames per second with room to spare.


-----------------------------------------------------------------------------
HOW TO RUN
-----------------------------------------------------------------------------
  1. Unpack the ZIP anywhere, for example to the Desktop.
  2. Double-click Vesperfell.exe.

  Windows SmartScreen may say "Windows protected your PC", because the game is
  not signed by a publisher. Click "More info", then "Run anyway".

  Optional switches (for example in a shortcut):
    --windowed   --fullscreen   --mute   --3d   --2d

  Saves and settings are kept next to Vesperfell.exe (the save folder and
  settings.ini); if that folder is read-only they go to %LOCALAPPDATA%\Vesperfell.
  The game saves when you rest at a wayshrine, at the big moments of the story,
  from the pause menu (Esc > Save Game) and when you quit.


-----------------------------------------------------------------------------
CONTROLS
-----------------------------------------------------------------------------
  W A S D          Move                       Mouse         Look and aim
  Left mouse       Basic attack (hold)        Right mouse   Class secondary
  Q / E / R        Abilities                  Space         Dodge roll
  F                Interact / talk            1 / 2         Health / Spirit draught
  I or Tab         Inventory                  C             Character
  K                Skills                     J             Journal
  M                Map                        Esc           Pause / close
  F11              Fullscreen                 Arrow keys    Turn
  Enter            Party chat (co-op)

  In first person the mouse turns the view. Opening any window or the pause
  menu frees the cursor; closing it gives the view back to the mouse.


-----------------------------------------------------------------------------
VIEWS AND SETTINGS  (Esc > Settings, or Settings on the title screen)
-----------------------------------------------------------------------------
  Graphics 3D, camera First person   the default: you see through your hero's eyes
  Graphics 3D, camera Overhead       the same 3D world seen from above, aim with the mouse
  Graphics 2D                        the classic top-down pixel-art view (also the
                                     automatic fallback when 3D is not available)

  Also: volume sliders for Master, Music, Effects and Ambience; window size and
  fullscreen; mouse speed and invert; screen shake; damage numbers; frame
  counter; language (English / Russian); your name for co-op.


-----------------------------------------------------------------------------
THE FOUR CALLINGS
-----------------------------------------------------------------------------
  Warrior - Oathguard of the Bell (resource: Fury)
      Cleave (combo) / Bulwark (raise the shield) / Shield Rush /
      Whirlwind (level 2) / Vesper Oath (level 4)
  Mage - Cinderwright (Mana)
      Ember Bolt / Blink / Fireball / Frost Nova (level 2) / Stormcall (level 4)
  Rogue - Duskblade (Energy)
      Twin Fangs (stronger from behind) / Thrown Knife / Shadowstep /
      Veil (level 2) / Fan of Knives (level 4)
  Ranger - Fenwarden (Focus)
      Loose / Piercing Shot / Snare / Arrow Rain (level 2) / Hunter's Mark (level 4)

  Every class also has a dodge roll (Space) with a moment of invulnerability.


-----------------------------------------------------------------------------
FEATURES
-----------------------------------------------------------------------------
  - A first-person 3D world: the Low Road, the village of Emberwick, the
    Thornwood, Mirefen, the Barrowfield, Abbey Rise, the Ashen Fields and the
    Sunken Abbey beneath them, down to the Bell Crypt. Real 3D models for heroes,
    monsters, villagers and scenery, painted materials, moonlight shadows, fog,
    torch and spell light, bloom.
  - A smooth, high-resolution interface and text at every window size, drawn by
    the graphics card.
  - Real-time combat: combos, blocking, dodge rolls, critical hits, knock-back,
    stuns, roots, burning, poison, bleeding and freezing; elite foes, Sir Caldus
    the Barrow Knight and the final boss, the Silent Prior.
  - Loot in four rarities (Common, Fine, Rare, Relic): weapons for every class,
    armour, trinkets, gold and draughts. Green marks show upgrades. A smith
    tempers weapons, a herbalist sells draughts.
  - Levels 1 to 15, four attributes to spend points on, abilities that unlock at
    levels 2 and 4.
  - A main quest with a final boss and an ending, side quests, secrets (a hidden
    glade, rune stones, a cracked wall, a sealed vault), wayshrines to rest at,
    a quest journal, a world map and a minimap.
  - Music and 77 sound effects synthesised by the game itself (village, wild,
    ruins, dungeon, boss and dawn themes).
  - English and Russian, switchable at any time.
  - Co-op for up to four players.


-----------------------------------------------------------------------------
PLAYING WITH FRIENDS (CO-OP, UP TO 4 PLAYERS)
-----------------------------------------------------------------------------
  The host:   start or continue a game, press Esc and choose "Host Co-op".
              The game shows the addresses friends can use.
  A friend:   title screen > "Join Game", type your name and the host's address,
              choose a class and enter the host's world.

  - On the same home network, use the host's address that begins with 192.168
    or 10.
  - Over the internet, either forward TCP port 27015 on the host's router to the
    host's PC, or put everyone on a virtual network (Radmin VPN, ZeroTier,
    Tailscale) and use the host's address on it.
  - When Windows Firewall asks the host about Vesperfell, allow it (private
    networks are enough for a home network or a VPN).

  Guests are raised to the host's level and given gear to match. Quests,
  chests, wayshrines and the world are shared; every hero gets their own share
  of the spoils. A fallen hero can rise beside a companion. The journey is
  saved in the host's game; guests have nothing to save.


-----------------------------------------------------------------------------
HOW IT WAS TESTED
-----------------------------------------------------------------------------
  The game has a built-in automatic test (Vesperfell.exe --selftest <folder>),
  run in English and in Russian before release: 301 of 301 checks passed in each.
  It covers the menus, all four classes and every ability, combat, loot, the
  inventory, levelling, dialogue, death and respawn, saving and loading, a full
  playthrough of the main quest to the ending, sound and music rendering, and
  two game instances playing co-op over a real TCP connection on one PC.
  Scripted play sessions were also run through the shipped Vesperfell.exe in
  first person, overhead and 2D, with every class and in Russian, capturing
  screenshots of every area and every window.


-----------------------------------------------------------------------------
KNOWN LIMITATIONS
-----------------------------------------------------------------------------
  - It is a demo: one vale, one main quest and its side quests. After the
    ending you can keep exploring.
  - Keyboard and mouse only: no gamepad support, and keys cannot be rebound.
  - Co-op was tested on one PC (two game instances over a TCP connection), not
    across the internet. There is no server browser and no NAT punch-through:
    friends over the internet need port forwarding or a VPN (see above).
    Only the host can save.
  - The 3D view needs OpenGL 3.3. Over Remote Desktop, in some virtual machines
    or with very old drivers the game falls back to the 2D view.
  - The first moment in the world after starting or loading can take a fraction
    of a second longer while the scene is prepared (it happens behind the fade).
  - The executable is not code-signed, so Windows may ask for confirmation the
    first time (see How to run).
