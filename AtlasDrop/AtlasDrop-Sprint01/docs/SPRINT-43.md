# Sprint 43 — Gestion des chemins longs

## Critères d'acceptation automatisables

1. Politique centralisée de longueur de chemin Windows.
2. Normalisation par `Path.GetFullPath`.
3. Limite de sécurité par défaut : 240 caractères.
4. Chemin vide refusé.
5. Limite configurable.
6. Destination évaluée à partir du dossier + nom de fichier.
7. Un chemin trop long est refusé avant toute opération fichier.
8. La création de dossier utilise cette politique.
9. L'interface affiche l'état de longueur du chemin proposé.
10. Aucun déplacement réel n'est effectué dans ce sprint.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 43 réussis.
