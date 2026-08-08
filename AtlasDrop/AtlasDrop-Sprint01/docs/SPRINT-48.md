# Sprint 48 — Vérification post-déplacement

## Critères d'acceptation automatisables

1. Destination présente après déplacement.
2. Source absente après déplacement.
3. Taille destination identique à la taille source capturée avant déplacement.
4. SHA-256 vérifié pour les fichiers <= 64 Mo.
5. Un écart de taille fait échouer la vérification.
6. Un écart de hash fait échouer la vérification.
7. Une source encore présente fait échouer la vérification.
8. Le service de déplacement utilise systématiquement cette vérification.
9. Aucun succès n'est annoncé sans vérification complète.
10. Rollback automatique en cas d'échec sera ajouté au Sprint 49.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 48 réussis.
