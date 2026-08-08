# Sprint 46 — Prévalidation déplacement

## Critères d'acceptation automatisables

1. Source normalisée et existence vérifiée.
2. Destination normalisée et existence vérifiée.
3. Destination obligatoirement sous OneDrive autorisé.
4. Nom final validé par la politique Windows.
5. Longueur du chemin final vérifiée.
6. Lisibilité de la source testée.
7. Écriture dans le dossier destination testée avec un fichier sonde temporaire.
8. Doublon de nom signalé.
9. Même taille signalée.
10. Doublon exact SHA-256 signalé.
11. Les conflits sont des avertissements : le Sprint 47 choisira un nom libre et n'écrasera jamais.
12. Aucun déplacement réel effectué dans ce sprint.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 46 réussis.
