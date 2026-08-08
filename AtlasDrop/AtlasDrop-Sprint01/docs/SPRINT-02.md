# Sprint 02 — Configuration externe et validation OneDrive

## Critères d'acceptation automatisables

1. La configuration est stockée dans `appsettings.json`.
2. La racine par défaut est `C:\Users\fchelli\OneDrive - ALTEDIS`.
3. `MaxSuggestedDepth` vaut 4.
4. Une racine vide est refusée.
5. Une profondeur hors plage 1..12 est refusée.
6. Une configuration JSON valide est chargée.
7. Un fichier de configuration absent est refusé clairement.
8. Une configuration JSON invalide est refusée.
9. Les tests utilisent uniquement `%TEMP%\AtlasDrop.Tests`.
10. Aucun fichier réel OneDrive n'est modifié.

## Preuve attendue

- restauration NuGet réussie ;
- compilation Release réussie ;
- 0 erreur ;
- tests Sprint 1 + Sprint 2 tous réussis.
