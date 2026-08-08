# Sprint 10 — Progression visible et annulation des scans

## Critères d'acceptation automatisables

1. Un scan démarre avec l'état `Running`.
2. La progression remonte le nombre de dossiers déjà scannés.
3. Le chemin courant est exposé pendant le scan.
4. Un scan réussi termine en `Completed`.
5. Une annulation termine en `Cancelled`.
6. Une erreur termine en `Failed`.
7. Les dates de début et de fin sont conservées.
8. L'erreur est exposée sans faire disparaître l'état précédent.
9. Le scan réel reste asynchrone.
10. Aucun fichier OneDrive réel n'est modifié dans les tests.

## Preuve attendue

Compilation Release sans erreur + tous les tests Sprint 1 à 10 réussis.
