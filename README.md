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
3. Étape M4 : une vraie partie solo contre 9 bots, avec la zone battle royale et la carte vivante :
   - **ZQSD** (AZERTY) / **WASD** (QWERTY) ou flèches : marcher · **Espace** maintenu : sprint · **X** : halte
   - **Clic droit** : aller à un point (maintenu = suivre la souris) · **Maj + clic droit** : ajouter une étape
   - **Molette** : zoom · **Y** : verrouiller/libérer la caméra · **C** : recentrer · **clic molette glissé** : déplacer la vue
   - **1** charge · **2** retrait · **3** viser (le curseur) · **R** cri de guerre
   - **4 / 5 / 6** : acheter un guerrier / une troupe à distance / un tank · **Tab** ou **7 8 9 0** : changer de formation · **F** : éditeur de formation
   - **Minicarte** : clic gauche pour regarder, clic droit pour y aller · **L** : classement
   - **F8** : mode (classique / rapide / bac à sable) · **F9** : difficulté des bots · **F10** : brouillard de guerre
   - Tests : **F4** +20 troupes · **F6** clan ennemi immobile (**Maj + F6** : 2 ères d'avance) · **F7** +50 ★ · **F2** nouvelle carte · **F3** panneau technique

## Lancer les tests et le serveur (optionnel)

Dans un terminal, à la racine du dépôt :

```
dotnet test tests/ChronoClash.Core.Tests
dotnet run --project src/ChronoClash.Server
```

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
