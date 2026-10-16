# Guide du graphiste

Le jeu dessine pour l'instant tout par le code, dans le style du prototype. Chaque image que tu ajoutes **remplace un dessin par défaut**, sans casser le reste. Tu peux donc avancer un visuel à la fois.

## Comment ça marche

1. Tu exportes ton image en **PNG avec fond transparent**.
2. Tu la nommes selon la convention ci-dessous et tu la poses dans le bon dossier de `game/assets/art/`.
3. Au lancement, le jeu la trouve et l'utilise. Si l'image n'existe pas, il garde le dessin par défaut.

## Convention des noms

Les ères sont numérotées : 0 Préhistoire, 1 Rome, 2 Moyen Âge, 3 Renaissance, 4 1914-18, 5 1939-45, 6 Moderne, 7 Futur.

| Élément | Dossier | Nom | Exemple |
|---|---|---|---|
| Guerrier | `art/units/` | `war_<ère>.png` | `war_3.png` = mousquetaire |
| Troupe à distance | `art/units/` | `rng_<ère>.png` | `rng_1.png` = archer romain |
| Tank | `art/units/` | `tank_<ère>.png` | `tank_0.png` = mammouth |
| Chef | `art/chiefs/` | `chief_<ère>.png` | `chief_2.png` = chevalier |
| Bulles, objets | `art/objects/` | nom de l'objet | `bubble_war.png`, `crate.png`, `portal.png` |
| Emblèmes | `art/emblems/` | nom de l'emblème | `sun.png`, `skull.png` |

## Règles de dessin

- **Vue de dessus.** L'avant de la troupe pointe **vers la droite** de l'image : le jeu la fait tourner selon sa direction.
- **Couleur du clan :** les zones qui doivent prendre la couleur du joueur (écharpe, bouclier, caparaçon…) sont à peindre en **blanc pur** sur un calque séparé, exporté en `<nom>_team.png`. Le jeu les teinte automatiquement.
- **Taille conseillée :** 64 × 64 pixels pour les troupes, 128 × 128 pour les tanks et les chefs à cheval. Le jeu réduit à l'affichage.
- Garde la silhouette lisible en tout petit : à l'écran, une troupe fait souvent moins de 20 pixels.

Ce guide sera complété au fil des étapes, avec les animations, les effets et l'interface.
