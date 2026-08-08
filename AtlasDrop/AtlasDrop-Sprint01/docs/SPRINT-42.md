# Sprint 42 — Noms Windows invalides

## Critères d'acceptation automatisables

1. Caractères `" * : < > ? / \ |` neutralisés.
2. Noms réservés CON/PRN/AUX/NUL/COM1..9/LPT1..9 neutralisés.
3. Points et espaces finaux supprimés.
4. Extension conservée et nettoyée.
5. Nom vide refusé.
6. Longueur de nom bornée.
7. Un nom déjà valide reste inchangé.
8. Le moteur de renommage passe systématiquement par cette politique.
9. Aucun écrasement ni opération physique sur fichier dans ce sprint.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 42 réussis.
