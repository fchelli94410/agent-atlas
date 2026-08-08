# Sprint 30 — Recherche tolérante aux fautes

## Critères d'acceptation automatisables

1. Distance de Levenshtein locale.
2. Un typo toléré sur mot moyen.
3. Deux typos possibles sur mot plus long.
4. Aucun fuzzy sur mots très courts.
5. Seuil de similarité minimal.
6. Accents/casse/aliases toujours normalisés.
7. Recherche fuzzy dans nom, chemin et contenu indexé.
8. Correspondance exacte toujours prioritaire.
9. Fuzzy utilisable en requête multi-mots.
10. Mots sans rapport rejetés.
11. Aucun fichier utilisateur modifié.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 30 réussis.
