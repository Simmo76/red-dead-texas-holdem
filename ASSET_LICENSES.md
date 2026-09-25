# Asset licence register

Every third-party asset in this repository is listed here **before** being imported,
per the development plan's licensing rules. Sources must be permissively licensed
(CC0 / public-domain equivalent) or properly licensed to the repository owner (e.g.
Unity Asset Store purchases, embedded only as converted, integrated derivatives);
no unlicensed third-party IP.

| Asset | Files in repo | Source | Author | Licence |
|---|---|---|---|---|
| Boardgame Pack v2 — playing card faces, card back, poker chips | `Assets/Resources/Poker/Cards/*.png`, `Assets/Resources/Poker/Chips/*.png`, `Web/CinematicPoker.Web/wwwroot/img/cards/*.png`, `Web/CinematicPoker.Web/wwwroot/img/chip_*.png` | [kenney.nl/assets/boardgame-pack](https://kenney.nl/assets/boardgame-pack) | Kenney Vleugels (kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Casino Audio 1.1 — card shuffle/slide/place/shove, chip stack/handle/collide | `Assets/Resources/Poker/Audio/*.ogg`, `Web/CinematicPoker.Web/wwwroot/audio/*.ogg` | [kenney.nl/assets/casino-audio](https://kenney.nl/assets/casino-audio) | Kenney Vleugels (kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Polar Fleece — fabric texture (diffuse 1K), tinted green at runtime as table felt | `Assets/Resources/Poker/Textures/felt.jpg`, `Web/CinematicPoker.Web/wwwroot/img/felt.jpg` | [polyhaven.com/a/polar_fleece](https://polyhaven.com/a/polar_fleece) | Poly Haven (texture team) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Wood Table 001 — wood texture (diffuse 1K), table rail/base and floor | `Assets/Resources/Poker/Textures/wood.jpg`, `Web/CinematicPoker.Web/wwwroot/img/wood.jpg` | [polyhaven.com/a/wood_table_001](https://polyhaven.com/a/wood_table_001) | Poly Haven (texture team) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Mini Characters — five rigged 3D NPCs (FBX, embedded idle/sit/emote/die animations) + colormap texture | `Assets/Resources/Poker/Models/Characters/*.fbx`, `Assets/Resources/Poker/Models/Characters/Textures/colormap.png` | [kenney.nl/assets/mini-characters](https://kenney.nl/assets/mini-characters) | Kenney Vleugels (kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Furniture Kit — chair, ceiling lamp, round rug, potted plant, radio (FBX); chair (GLB) for web scene | `Assets/Resources/Poker/Models/Furniture/*.fbx`, `Web/CinematicPoker.Web/wwwroot/models/props/furniture/chair.glb` | [kenney.nl/assets/furniture-kit](https://kenney.nl/assets/furniture-kit) | Kenney Vleugels (kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Arcade Fighters — two rigged fighter characters used as the web scene's table NPCs. Converted to web-optimised GLB (baked seated pose + Idle/Attack/Crouch/Jump clips) plus recoloured outfit textures. The original `.unitypackage` and its raw FBX/TGA sources are **not** included in this repository — only these converted derivatives. | `Web/CinematicPoker.Web/wwwroot/models/characters/Fighter1.glb`, `Fighter2.glb`, `fighter1_var.jpg`, `fighter1_green.jpg`, `fighter2_var.jpg` | Unity Asset Store (pack licensed to the repository owner, supplied privately for this project) | Original pack author (Unity Asset Store) | [Unity Asset Store EULA](https://unity.com/legal/as-terms) — single-entity licence held by the repo owner; assets embedded as integrated, converted parts of this project only |
| Survival Kit — barrel, crate, bottles (GLB) + colormap, saloon props | `Web/CinematicPoker.Web/wwwroot/models/props/survival/*.glb`, `Web/CinematicPoker.Web/wwwroot/models/props/survival/Textures/colormap.png` | [kenney.nl/assets/survival-kit](https://kenney.nl/assets/survival-kit) | Kenney Vleugels (kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Food Kit — mug (GLB) + colormap, saloon prop | `Web/CinematicPoker.Web/wwwroot/models/props/food/mug.glb`, `Web/CinematicPoker.Web/wwwroot/models/props/food/Textures/colormap.png` | [kenney.nl/assets/food-kit](https://kenney.nl/assets/food-kit) | Kenney Vleugels (kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Worn Planks — wood plank texture (diffuse 1K), saloon floor | `Web/CinematicPoker.Web/wwwroot/img/floor_planks.jpg` | [polyhaven.com/a/worn_planks](https://polyhaven.com/a/worn_planks) | Poly Haven (texture team) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Wood Plank Wall — wood plank texture (diffuse 1K), saloon walls/ceiling | `Web/CinematicPoker.Web/wwwroot/img/wall_planks.jpg` | [polyhaven.com/a/wood_plank_wall](https://polyhaven.com/a/wood_plank_wall) | Poly Haven (texture team) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| three.js 0.169.0 — 3D rendering library (module build, GLTFLoader, SkeletonUtils, BufferGeometryUtils) | `Web/CinematicPoker.Web/wwwroot/js/vendor/*.js`, `Web/CinematicPoker.Web/wwwroot/js/utils/BufferGeometryUtils.js` | [threejs.org](https://threejs.org) | three.js authors | [MIT](https://github.com/mrdoob/three.js/blob/dev/LICENSE) |
| Lasso Lady (seamless loop) — western background music, re-encoded to OGG | `Web/CinematicPoker.Web/wwwroot/audio/saloon-music.ogg` | [opengameart.org/content/lasso-lady-seamless-loop](https://opengameart.org/content/lasso-lady-seamless-loop) | looneybits | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Universal Animation Library (Standard) — `Sitting_Idle_Loop` and `Sitting_Talking_Loop` skeletal animations, retargeted in Blender onto the Arcade Fighters rigs as the `Sit`/`Talk` clips inside the fighter GLBs (no UAL mesh included) | animation data embedded in `Web/CinematicPoker.Web/wwwroot/models/characters/Fighter1.glb`, `Fighter2.glb` | [quaternius.com](https://quaternius.com/packs/universalanimationlibrary.html) | Quaternius | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Character voice lines — 25 original lines written for this project and synthesised locally with Piper TTS (MIT) using the rhasspy/piper-voices models; no recordings of real people, no third-party dialogue | `Web/CinematicPoker.Web/wwwroot/audio/voices/*.ogg` | Generated for this repository | This project (via [Piper TTS](https://github.com/rhasspy/piper)) | Original works of this repository; Piper engine [MIT](https://github.com/rhasspy/piper/blob/master/LICENSE.md) |

Notes:

- CC0 requires no attribution, but Kenney and Poly Haven are credited here and in the
  README as a courtesy.
- Card faces were renamed from Kenney's `cardHeartsA.png` convention to the engine's
  `hearts_A.png` convention; the card back is Kenney's `cardBack_red2.png`.
- When adding any new asset: verify the licence on the source page, add a row to this
  table in the same commit, and never import content from Rockstar, Disney/Lucasfilm,
  Playboy, Eon/Bond or any other third-party IP.
