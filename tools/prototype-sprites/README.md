# Exporter les dessins du prototype en images

Ce petit outil reprend **exactement** les dessins du prototype HTML (les 24 troupes et les 8 chefs, vus de dessus,
l'avant vers la droite) et les enregistre en PNG, avec pour chacun un calque `_team` (zones à la couleur du clan, en blanc).

```
cd tools/prototype-sprites
npm install
npm run render
```

Les images arrivent dans `out/units/` et `out/chiefs/` : copie-les dans `game/assets/art/units/` et `game/assets/art/chiefs/`.

- Troupes : 96 × 96 px (guerriers et distance), 128 × 128 px (tanks) ; chefs : 128 × 128 px.
- Les zones du clan sont en gris neutre dans l'image principale ; le jeu y pose la couleur du clan grâce au calque `_team`.
- Pas d'ombre au sol (le jeu ajoute la sienne).
- Le graphiste peut remplacer ces images une par une par les siennes, **de la même taille**.
