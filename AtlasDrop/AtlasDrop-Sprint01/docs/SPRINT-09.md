# Sprint 09 — Nettoyage des chemins disparus

## Critères d'acceptation automatisables

1. Un dossier absent du scan de sécurité est marqué inactif.
2. Aucun enregistrement n'est supprimé automatiquement.
3. `StableId` et historique sont conservés.
4. `MissingSinceUtc` mémorise la première disparition.
5. Un dossier qui réapparaît est réactivé.
6. Les chemins sont comparés sans tenir compte de la casse Windows.
7. La base SQLite passe au schéma version 2.
8. L'initialisation reste idempotente.
9. Aucun fichier réel OneDrive n'est modifié dans les tests.

## Règle de sécurité

Le nettoyage concerne uniquement l'index local SQLite. Atlas Drop ne supprime jamais un dossier ou un fichier réel parce qu'il n'est plus présent dans un scan.
