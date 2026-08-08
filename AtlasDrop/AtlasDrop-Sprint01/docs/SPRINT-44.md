# Sprint 44 — Détection de doublons

## Critères d'acceptation automatisables

1. Détection d'un conflit de nom à destination.
2. Aucun écrasement autorisé.
3. Proposition automatique d'un nom libre `(2)`, `(3)`, etc.
4. Même nom + même taille = signal de doublon renforcé.
5. Source absente : le conflit de nom reste détecté sans crash.
6. Aucun fichier supprimé automatiquement.
7. Aucun fichier remplacé automatiquement.
8. SHA-256 exact réservé au Sprint 45.
9. Aucun déplacement réel effectué dans ce sprint.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 44 réussis.
