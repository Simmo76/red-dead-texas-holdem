# Asset licence register

Every third-party asset in this repository is listed here **before** being imported,
per the development plan's licensing rules. Only permissively licensed (CC0 /
public-domain equivalent) sources are allowed; no third-party IP.

| Asset | Files in repo | Source | Author | Licence |
|---|---|---|---|---|
| Boardgame Pack v2 — playing card faces, card back, poker chips | `Assets/Resources/Poker/Cards/*.png`, `Assets/Resources/Poker/Chips/*.png`, `Web/CinematicPoker.Web/wwwroot/img/cards/*.png`, `Web/CinematicPoker.Web/wwwroot/img/chip_*.png` | [kenney.nl/assets/boardgame-pack](https://kenney.nl/assets/boardgame-pack) | Kenney Vleugels (kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Casino Audio 1.1 — card shuffle/slide/place/shove, chip stack/handle/collide | `Assets/Resources/Poker/Audio/*.ogg`, `Web/CinematicPoker.Web/wwwroot/audio/*.ogg` | [kenney.nl/assets/casino-audio](https://kenney.nl/assets/casino-audio) | Kenney Vleugels (kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Polar Fleece — fabric texture (diffuse 1K), tinted green at runtime as table felt | `Assets/Resources/Poker/Textures/felt.jpg`, `Web/CinematicPoker.Web/wwwroot/img/felt.jpg` | [polyhaven.com/a/polar_fleece](https://polyhaven.com/a/polar_fleece) | Poly Haven (texture team) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Wood Table 001 — wood texture (diffuse 1K), table rail/base and floor | `Assets/Resources/Poker/Textures/wood.jpg`, `Web/CinematicPoker.Web/wwwroot/img/wood.jpg` | [polyhaven.com/a/wood_table_001](https://polyhaven.com/a/wood_table_001) | Poly Haven (texture team) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |

Notes:

- CC0 requires no attribution, but Kenney and Poly Haven are credited here and in the
  README as a courtesy.
- Card faces were renamed from Kenney's `cardHeartsA.png` convention to the engine's
  `hearts_A.png` convention; the card back is Kenney's `cardBack_red2.png`.
- When adding any new asset: verify the licence on the source page, add a row to this
  table in the same commit, and never import content from Rockstar, Disney/Lucasfilm,
  Playboy, Eon/Bond or any other third-party IP.
