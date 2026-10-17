# Chrono Clash

Battle royale de stratégie en temps réel : dix clans, huit ères, de la préhistoire au futur, un seul survivant.
Version Godot 4 en C#, avec un mode solo contre des bots et un mode multijoueur jusqu'à 10 joueurs (un joueur héberge la partie).

L'architecture est décrite dans [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). La façon de travailler à plusieurs est dans [CONTRIBUTING.md](CONTRIBUTING.md).

## Versions utilisées par toute l'équipe

| Outil | Version |
|---|---|
| Godot | **4.7.2, édition .NET** (fichier `Godot_v4.7.2-stable_mono_win64`) |
| SDK .NET | **10** (version 10.0.402 ou plus récente) |
| Git LFS | dernière version |

Attention : l'édition standard de Godot (sans « mono » dans le nom) **ne sait pas exécuter le C#**.

## Installation (à faire une seule fois)

1. Installer le **SDK .NET 10** depuis le site officiel de Microsoft (dotnet.microsoft.com), puis redémarrer l'ordinateur.
2. Télécharger **Godot 4.7.2 édition .NET** depuis godotengine.org : il faut choisir le bouton « .NET ». Le dossier s'appelle `Godot_v4.7.2-stable_mono_win64`.
3. Installer **GitHub Desktop** (desktop.github.com). Git LFS est inclus.
4. Cloner le dépôt avec GitHub Desktop : *File → Clone repository*.

## Lancer le jeu

1. Ouvrir Godot .NET. Dans la **liste des projets**, cliquer sur **Importer** et choisir le fichier `game/project.godot` (et **pas** la racine du dépôt).
2. Cliquer sur **Build** (marteau en haut à droite), puis sur **Lancer** (▶).
3. L'écran titre s'ouvre : **Tutoriel** pour apprendre, **Jouer** pour une partie (solo, héberger ou rejoindre, voir « Jouer à plusieurs »), **Réglages** pour la langue, le son et les touches. En jeu (touches par défaut, modifiables dans Réglages) :
   - **ZQSD** (AZERTY) / **WASD** (QWERTY) ou flèches : marcher · **Espace** maintenu : sprint · **X** : halte
   - **Clic droit** : aller à un point (maintenu = suivre la souris) · **Maj + clic droit** : ajouter une étape
   - **Molette** : zoom · **Y** : verrouiller/libérer la caméra · **C** : recentrer · **clic molette glissé** : déplacer la vue
   - **1** charge · **2** retrait · **3** viser (le curseur) · **R** cri de guerre
   - **4 / 5 / 6** : acheter un guerrier / une troupe à distance / un tank · **Tab** ou **7 8 9 0** : changer de formation · **F** : éditeur de formation
   - **Minicarte** : clic gauche pour regarder, clic droit pour y aller · **L** : classement · **Échap** : pause
   - **F8** : mode (classique / rapide / bac à sable) · **F9** : difficulté des bots · **F10** : brouillard de guerre
   - Tests : **F4** +20 troupes · **F6** clan ennemi immobile (**Maj + F6** : 2 ères d'avance) · **F7** +50 ★ · **F2** nouvelle carte · **F3** panneau technique

## Jouer à plusieurs

1. **L'hôte** clique sur **Jouer**, puis **Héberger une partie**. L'écran affiche son adresse IP locale (par exemple `192.168.1.20`).
2. **Les amis** cliquent sur **Jouer** et tapent cette adresse dans le champ à côté de **Rejoindre**, avec le même port (7777 par défaut), puis cliquent sur **Rejoindre**.
3. Quand tout le monde est dans le salon, l'hôte clique sur **Lancer la partie**. Les places libres (jusqu'à 10 clans) sont prises par des bots.

- **Sur le même réseau (même box)** : rien d'autre à faire.
- **Par internet** : l'hôte doit ouvrir le **port UDP 7777** sur sa box (« redirection de port » vers son ordinateur) et donner aux amis son **adresse IP publique** (visible sur un site comme « quel est mon IP »). Le pare-feu de Windows peut aussi demander l'autorisation la première fois : accepter pour les réseaux privés.
- Pour tester seul sur un ordinateur : lancer le jeu deux fois (depuis Godot : *Debug → Run Multiple Instances → 2*), héberger dans l'un et rejoindre `127.0.0.1` dans l'autre.
- Tout le monde doit avoir **exactement la même version** du jeu (le salon refuse sinon).

## Serveur dédié et équilibrage

Voir [docs/M7.md](docs/M7.md) : lancer le jeu sans affichage comme serveur (`--headless -- --server`), et simuler des dizaines de parties entre bots pour équilibrer (`dotnet run --project src/ChronoClash.Server -c Release -- simulate`).

## Lancer les tests et le serveur (optionnel)

Dans un terminal, à la racine du dépôt :

```
dotnet test tests/ChronoClash.Core.Tests
dotnet run --project src/ChronoClash.Server -c Release -- simulate --matches 10 --mode quick
```

Le test le plus long (une partie rapide complète entre bots) peut être ignoré avec `dotnet test --filter "Category!=Long"`.

Les tests tournent aussi automatiquement sur GitHub à chaque envoi (onglet **Actions**).

## Structure

```
src/ChronoClash.Core     cœur du jeu en C# pur (aucune dépendance à Godot)
src/ChronoClash.Server   futur serveur dédié, sans affichage
tests/                   tests automatiques du cœur
game/                    projet Godot : affichage, son, menus, contrôles, réseau
data/                    règles du jeu en JSON (troupes, ères, équilibrage)
docs/                    documentation de l'équipe
```
