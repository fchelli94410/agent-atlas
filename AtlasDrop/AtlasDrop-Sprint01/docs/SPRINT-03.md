# Sprint 03 — SQLite et schéma initial

## Critères d'acceptation automatisables

1. SQLite est local via Microsoft.Data.Sqlite.
2. La base peut être créée dans un dossier de test isolé.
3. Le schéma a une version explicite.
4. Table `Folders` présente avec les champs nécessaires à l'indexation initiale.
5. Table `Operations` présente pour l'historique.
6. Table `LearningEvents` présente pour l'apprentissage local.
7. Index principaux créés.
8. Initialisation répétée sans erreur.
9. `FullPath` d'un dossier est unique.
10. Aucun test n'écrit dans le OneDrive réel.

## Preuve attendue

Compilation Release sans erreur + tous les tests Sprint 1 à 3 réussis.
