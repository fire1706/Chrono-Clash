# Travailler ensemble sur Chrono Clash

## Le principe

- La branche **`main`** doit toujours fonctionner : on n'y travaille jamais directement.
- Chaque tâche se fait sur **sa propre branche**, puis on propose de l'intégrer avec une **Pull Request**.
- Le responsable du dépôt relit et fusionne.

## Avec GitHub Desktop, pas à pas

1. **Récupérer les dernières modifications** : onglet `main`, bouton **Fetch origin** puis **Pull origin**.
2. **Créer une branche** : *Branch → New branch*, avec un nom clair :
   - `feature/charge-tactique` pour du code,
   - `art/mammouth` pour des visuels,
   - `fix/troupes-bloquees` pour une correction.
3. **Travailler**, puis enregistrer régulièrement : écrire un résumé en bas à gauche (« Ajoute le sprite du mammouth »), puis **Commit**.
4. **Envoyer** sur GitHub : **Push origin**.
5. **Proposer l'intégration** : **Create Pull Request**. Décrire ce qui change et comment le tester.
6. Après la fusion, revenir sur `main` et refaire l'étape 1.

## Qui touche à quoi

| Dossier | Contenu | Qui |
|---|---|---|
| `src/`, `tests/` | Logique du jeu, tests | Développeurs |
| `game/scripts/`, `game/scenes/` | Affichage, interface, son, réseau | Développeurs |
| `game/assets/` | Images, sons, polices | Graphiste (voir `docs/ART_GUIDE.md`) |
| `data/` | Règles et équilibrage | Tout le monde, après discussion |

En restant chacun dans ses dossiers, on évite presque tous les conflits.

## Règles de code

- **`ChronoClash.Core` ne référence jamais Godot.** Si une classe du cœur a besoin de Godot, elle n'est pas au bon endroit.
- Une classe par fichier, et le nom du fichier est celui de la classe.
- Noms en anglais dans le code (`Army`, `Unit`), commentaires en français.
- Une nouvelle règle de jeu s'accompagne d'un test dans `tests/`.
- Avant une Pull Request : le jeu se lance et `dotnet test` passe.

## Images et sons

Les fichiers lourds (`.png`, `.wav`, `.ogg`, `.psd`…) passent automatiquement par **Git LFS**, configuré dans `.gitattributes`. Il n'y a rien à faire de spécial avec GitHub Desktop.

Le stockage LFS gratuit de GitHub est limité : on évite de déposer des fichiers de travail énormes et on préfère les exports finaux.
