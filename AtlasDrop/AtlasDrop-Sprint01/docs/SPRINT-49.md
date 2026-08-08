# Sprint 49 — Rollback en cas d'échec

## Critères d'acceptation automatisables

1. Service de rollback dédié.
2. Si la vérification post-déplacement échoue, tentative immédiate de restauration.
3. Aucun écrasement de la source originale pendant rollback.
4. Destination absente => rollback signalé impossible.
5. Source déjà présente => rollback refusé.
6. Rollback réussi => source restaurée et destination supprimée par déplacement inverse.
7. Rollback vérifié après opération.
8. Le service de déplacement déclenche automatiquement le rollback sur échec de vérification.
9. Les erreurs de rollback sont rapportées sans crash.
10. Aucun succès n'est annoncé si vérification ou rollback échoue.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 49 réussis.
