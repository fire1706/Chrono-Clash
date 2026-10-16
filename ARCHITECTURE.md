# Chrono Clash — Architecture (Godot 4 · C#)

Ce document est le plan commun de l'équipe. Il décrit comment le jeu est découpé, qui fait quoi dans le code, et comment le multijoueur fonctionne. Le prototype HTML reste la **référence de comportement** : en cas de doute sur une règle (dégâts, seuils d'ère, zone…), on fait comme lui.

---

## 1. Principes

1. **Le cœur du jeu ne dépend pas de Godot.** Toute la logique (carte, armées, combats, ères, zone, objectifs, IA, points) vit dans une bibliothèque C# pure : `ChronoClash.Core`. Godot ne sert qu'à afficher, jouer les sons, gérer les menus et lire le clavier et la souris.
   Conséquences : le même code tourne dans le jeu, sur un futur serveur dédié et dans les tests automatiques. Il pourrait aussi être réutilisé dans un autre moteur.
2. **L'hôte fait autorité.** Une seule machine, l'hôte, fait tourner la vraie simulation. Les autres joueurs envoient des **ordres** (« va là », « charge », « achète un tank »), jamais des positions. L'hôte renvoie l'état du monde.
3. **Le solo est une partie hébergée sans invités.** Jouer hors ligne contre des bots, c'est héberger une partie avec un transport local en mémoire et 9 bots. Il n'y a qu'un seul chemin de code, donc le solo et le multijoueur ne divergent jamais.
4. **Bots et humains sont interchangeables.** Un bot produit exactement les mêmes ordres qu'un joueur. Si un joueur se déconnecte, un bot reprend son clan.
5. **Les données sont séparées du code.** Les troupes, ères, thèmes de carte, personnalités et réglages d'équilibrage sont des fichiers JSON dans `data/`. On peut équilibrer sans recompiler.
6. **L'affichage est remplaçable pièce par pièce.** Chaque visuel a un dessin par défaut, généré par le code dans le style du prototype. Dès qu'une image existe dans `assets/`, elle le remplace.

---

## 2. Organisation du dépôt

```
chrono-clash/
├─ ChronoClash.sln
├─ src/
│  ├─ ChronoClash.Core/            ← bibliothèque C# pure, AUCUNE référence à Godot
│  │  ├─ Simulation/               Match, World, TickClock, SimRandom, MatchSettings
│  │  ├─ Entities/                 Army, Unit, General, Bubble, Projectile, Boss, Crate, Portal, Shrine
│  │  ├─ Systems/                  un fichier par système (voir §4)
│  │  ├─ Map/                      MapGenerator, Terrain, NavGrid, Pathfinder, ThemeDef
│  │  ├─ AI/                       BotController, Personality, Perception, Decision
│  │  ├─ Commands/                 PlayerCommand et ses sous-types
│  │  ├─ Net/                      ITransport, messages, Snapshot, SnapshotWriter/Reader
│  │  ├─ Data/                     UnitDef, EraDef, TuningDef, DataLoader (JSON)
│  │  ├─ Stats/                    MatchStats, Achievements, Records
│  │  └─ Events/                   GameEvent (kill, évolution, largage…) pour l'UI et le son
│  └─ ChronoClash.Server/          ← application console sans affichage (futur serveur dédié)
├─ game/                           ← projet Godot 4 (édition .NET)
│  ├─ project.godot
│  ├─ scripts/
│  │  ├─ Bootstrap/                GameRoot, SceneRouter
│  │  ├─ Net/                      EnetTransport, LocalTransport, NetSession, Lobby
│  │  ├─ Presentation/             MapRenderer, ArmyRenderer, UnitVisualRegistry, FogRenderer, EffectsLayer, Minimap
│  │  ├─ UI/                       Hud, TitleScreen, LobbyScreen, PauseMenu, SettingsMenu, Tutorial, EndScreen, FormationEditor
│  │  ├─ Input/                    InputMapper (raccourcis personnalisables) → PlayerCommand
│  │  ├─ Audio/                    AudioManager, MusicSequencer, Announcer
│  │  └─ Persistence/              SettingsStore, RecordsStore
│  ├─ scenes/                      .tscn (écrans, HUD, overlays)
│  ├─ assets/                      art/units, art/chiefs, art/fx, audio/… (Git LFS)
│  └─ translations/                fr.csv, en.csv (système de traduction de Godot)
├─ data/                           units.json, eras.json, themes.json, personalities.json, tuning.json
├─ tests/
│  └─ ChronoClash.Core.Tests/      tests unitaires + simulations de parties complètes
└─ docs/                           ARCHITECTURE.md, CONTRIBUTING.md, ART_GUIDE.md
```

`game/` référence `src/ChronoClash.Core` comme projet C#. Le dossier `data/` est copié dans le jeu et dans le serveur au moment de la compilation.

---

## 3. Les objets du cœur (`ChronoClash.Core`)

### Simulation

| Classe | Rôle |
|---|---|
| `Match` | Une partie complète. Contient le `World`, la liste des joueurs (humains ou bots), applique les ordres reçus et fait avancer les ticks. Point d'entrée unique pour l'hôte. |
| `World` | L'état du monde à un instant donné : carte, armées, bulles, projectiles, objectifs, zone, temps écoulé. Ne contient **que des données**. |
| `TickClock` | Avance la simulation à **20 ticks par seconde**, à pas fixe, quelle que soit la vitesse d'affichage. |
| `SimRandom` | Générateur aléatoire avec graine. La même graine donne la même carte sur toutes les machines. |
| `MatchSettings` | Mode (classique, rapide, bac à sable, tutoriel), thème, difficulté des bots, brouillard, graine. |

### Entités

| Classe | Rôle |
|---|---|
| `Army` | Un clan : son `General`, ses `Unit`, ses ressources ◆, ses points ★, son ère, ses temps de recharge, sa formation, ses statistiques. |
| `General` | Le chef : position, vie, endurance, ère, statut promu ou non. |
| `Unit` | Une troupe : type (guerrier, distance, tank), ère, vie, chevrons, cible, place dans la formation. **Objet léger**, pas un nœud Godot, pour en gérer des centaines. |
| `Bubble`, `Projectile`, `Crate`, `Portal`, `Shrine`, `Boss`, `Meteor` | Les autres éléments du monde, chacun avec son propre état. |

### Systèmes

Un système est une classe qui fait évoluer une partie du monde à chaque tick. On les exécute toujours **dans le même ordre**.

| Ordre | Système | Rôle |
|---|---|---|
| 1 | `CommandSystem` | Applique les ordres reçus pour ce tick : déplacement, tactique, achat, formation. |
| 2 | `BotSystem` | Fait réfléchir les bots environ une fois par seconde. Ils produisent des ordres, comme un humain. |
| 3 | `GeneralMovementSystem` | Déplace les chefs : chemins, sprint, endurance, terrain, collisions. |
| 4 | `FormationSystem` | Calcule la place de chaque troupe à partir du dessin de formation, avec agrandissement proportionnel et rotation. |
| 5 | `UnitMovementSystem` | Déplace les troupes vers leur place ou leur cible, avec séparation et anti-blocage. |
| 6 | `CombatSystem` | Corps à corps, tirs, charge, cri de guerre, dégâts de zone, vétérans. |
| 7 | `ProjectileSystem` | Fait voler les projectiles : visée anticipée, dégâts de zone. |
| 8 | `DeathSystem` | Retire les morts, gère la succession du chef, l'élimination des clans et les récompenses. |
| 9 | `EvolutionSystem` | Seuils d'ère, évolution progressive sur 20 s, troupe par troupe. |
| 10 | `BubbleSystem` | Apparition et ramassage des bulles et des étoiles. |
| 11 | `ObjectiveSystem` | Largages, boss, portails, sanctuaires, événements (météores, orage, migration). |
| 12 | `ZoneSystem` | Rétrécissement par paliers et dégâts hors zone. |
| 13 | `VisionSystem` | Brouillard de guerre : ce que chaque clan voit. Sert à l'affichage et à l'envoi réseau. |
| 14 | `ScoringSystem` | Points, classement, statistiques de partie, défis. |

### Données (`data/*.json`)

- `UnitDef` : statistiques par type **et par ère** (vie, dégâts, cadence, portée, vitesse, projectile…), soit 3 types × 8 ères.
- `EraDef` : nom, seuil de ★, années affichées, chef de l'ère.
- `ThemeDef` : réglages de génération de carte et couleurs.
- `PersonalityDef` : Prudent, Chasseur, Collectionneur, Tacticien.
- `TuningDef` : tous les réglages d'équilibrage (zone, récompenses, difficulté…).

Les valeurs de départ sont celles du prototype HTML.

### Événements

Le cœur ne joue aucun son et n'affiche rien. Il publie des `GameEvent` (`UnitKilled`, `EraStarted`, `CrateLanded`, `BossSpawned`, `AchievementUnlocked`…). L'UI, l'audio et l'annonceur s'y abonnent côté Godot. En réseau, ces événements voyagent avec les snapshots.

---

## 4. Le multijoueur

### Les trois façons de jouer, un seul code

| Mode | Simulation | Transport | Joueurs |
|---|---|---|---|
| **Solo, hors ligne** | Sur ta machine | `LocalTransport` (en mémoire) | Toi + 9 bots |
| **Héberger** | Sur la machine de l'hôte | `EnetTransport` (UDP) en écoute | Jusqu'à 10 humains, les places vides prises par des bots |
| **Rejoindre** | Chez l'hôte | `EnetTransport` (UDP) client | — |
| Serveur dédié (plus tard) | `ChronoClash.Server`, sans affichage | `EnetTransport` | Jusqu'à 10 humains |

`ITransport` est défini dans le cœur. Godot fournit l'implémentation réseau avec `ENetMultiplayerPeer`, et le serveur dédié en fournira une sans Godot.

### Ce qui circule

**Du client vers l'hôte : des ordres.** Ils sont petits, environ 30 ordres par seconde maximum.

```
PlayerCommand { Tick, PlayerId, Type, ... }
  MoveTo(x, y, queue)    Direction(dx, dy)    Stop    Sprint(on)
  Tactic(Charge|Retreat|Aim(x,y)|Cry)
  Buy(War|Ranged|Tank)
  SelectFormation(index)    SetFormationCells(index, cells)
```

**De l'hôte vers chaque client : des snapshots**, 10 fois par seconde.
- **Filtrés par le brouillard de guerre.** Chaque joueur ne reçoit que ce que son clan voit. Ça économise du réseau et empêche la triche, puisqu'on ne peut pas afficher ce qu'on n'a pas reçu.
- **Compactés.** Une troupe tient en environ 6 octets : position sur 2 × 16 bits, vie sur 8 bits, type et ère sur 8 bits, plus un identifiant.
- **Accompagnés de l'état global** (zone, classement, objectifs annoncés) et des `GameEvent` du moment.

**La carte n'est jamais envoyée.** Seule la graine l'est, et chaque machine génère la même carte localement.

### Fluidité chez les clients

- **Interpolation :** le client affiche le monde avec environ 100 ms de retard et glisse entre deux snapshots. Les mouvements restent fluides même à 10 snapshots par seconde.
- **Prédiction du chef :** le chef du joueur bouge immédiatement à l'appui d'une touche, puis se recale doucement sur la position envoyée par l'hôte.

### Ordres de grandeur réseau

Avec beaucoup de troupes visibles, par exemple 500 × 6 octets × 10 par seconde, cela fait environ 30 Ko/s par client. Pour 9 invités, l'hôte envoie donc jusqu'à environ 270 Ko/s, soit environ 2 Mbit/s. C'est raisonnable pour une connexion fibre ou un réseau local.

Si nécessaire, on ajoutera une **compression par différences**, qui n'envoie que ce qui a changé.

### Héberger depuis chez soi

Pour que des amis rejoignent par internet, l'hôte doit **ouvrir un port UDP** sur sa box (par défaut 7777). En réseau local, rien à faire.

Plus tard, on pourra ajouter un serveur relais ou un service tiers pour éviter cette manipulation.

### Salon d'attente

L'hôte crée la partie et choisit le mode, la carte et la difficulté des bots. Les amis rejoignent avec l'adresse IP et le port. Chacun choisit son nom, sa couleur et son emblème. L'hôte lance la partie, et les places vides sont complétées par des bots.

En cas de déconnexion en cours de partie, un bot prend le relais. Le joueur peut revenir et récupérer son clan.

---

## 5. L'affichage (Godot)

- **`MapRenderer`** génère les textures de terrain par morceaux, comme le prototype.
- **`ArmyRenderer`** dessine les troupes en lots, avec un `MultiMeshInstance2D` par couple (type, ère). Des centaines de soldats ne coûtent que quelques appels de dessin.
- **`UnitVisualRegistry`** est la clé du travail du graphiste. Pour chaque troupe, chef, objet ou effet, il cherche une image dans `assets/` (par exemple `art/units/war_03.png` pour le mousquetaire). S'il la trouve, il l'utilise. Sinon, il garde le dessin par défaut du style actuel. Le graphiste peut remplacer les visuels **un par un**, sans toucher au code.
- **`FogRenderer`**, **`EffectsLayer`** et **`Minimap`** gèrent le brouillard, les effets (particules, explosions, transition d'ère) et la minicarte.
- **Qualité basse :** chaque renderer sait se simplifier pour les petites machines.

Le guide du graphiste (`docs/ART_GUIDE.md`) précisera les tailles, l'orientation (vue de dessus, vers la droite = avant), la palette et la convention de noms.

---

## 6. Tests et équilibrage

- **Tests unitaires** des règles : multiplicateur d'ère, succession du chef, formations, zone…
- **Simulations de parties complètes sans affichage :** 10 bots jouent des parties entières en accéléré. On mesure la durée, les ères atteintes et l'efficacité de chaque type de troupe, comme on l'a fait pour le prototype.
- Ces tests tournent automatiquement sur GitHub à chaque modification, grâce à GitHub Actions.

---

## 7. Travailler à plusieurs avec Git

- **Outils :** GitHub Desktop pour ceux qui débutent, et **Git LFS** pour les images, sons et polices. C'est configuré dans `.gitattributes`.
- **Branches :** `main` est toujours jouable. Chacun travaille sur une branche (`feature/combat-charge`, `art/mammouth`…), puis ouvre une Pull Request, relue et fusionnée par le responsable du dépôt.
- **Qui touche à quoi :**
  - code du cœur : `src/` ;
  - affichage et interface : `game/scripts` et `game/scenes` ;
  - graphiste : `game/assets` et éventuellement les scènes de visuels.
  On évite ainsi la plupart des conflits.
- **Une règle d'or :** `ChronoClash.Core` ne référence jamais Godot. Si une classe du cœur a besoin de quelque chose de Godot, c'est qu'elle n'est pas au bon endroit.

---

## 8. Étapes de réalisation

| Étape | Contenu | Résultat jouable |
|---|---|---|
| **M0** | Dépôt, solution, projet Godot .NET, Git LFS, tests automatiques | Le projet compile chez tout le monde |
| **M1** | Génération de carte, chemins, chef qui se déplace, `LocalTransport`, affichage de la carte | On se promène sur la carte |
| **M2** | Troupes, bulles, achats, formations et éditeur | On lève une armée |
| **M3** | Combat, tactiques, succession, ères et évolution | On se bat et on évolue |
| **M4** | Zone, objectifs, événements, bots et personnalités | **Partie solo complète hors ligne** |
| **M5** | Réseau : héberger, rejoindre, salon, snapshots, interpolation | **Partie multijoueur entre amis** |
| **M6** | Interface complète, audio, annonceur, traductions, réglages, statistiques, défis, tutoriel | Parité avec le prototype |
| **M7** | Équilibrage par simulations, finitions, serveur dédié | Version 1.0 |

---

## 9. Versions à installer

- **Godot 4**, dernière version stable, **édition .NET**. Attention à bien prendre l'édition .NET.
- **SDK .NET** dans la version demandée par cette version de Godot, indiquée sur la page de téléchargement.
- Un éditeur de code : **Visual Studio Code** avec l'extension C#, ou **JetBrains Rider**.
- **GitHub Desktop** et **Git LFS**.

Tout le monde doit utiliser **exactement les mêmes versions**. On les note dans le `README.md` dès l'étape M0.
