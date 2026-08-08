# Sprint 05 — Exclusions complètes

## Critères d'acceptation automatisables

1. `Bureau`, `Documents`, `Images`, `99 - Archives`, `pour voir` sont exclus.
2. Tout sous-dossier `99 - Archives` est exclu quelle que soit sa profondeur.
3. Les dossiers commençant par `.` ou `$` sont exclus.
4. Les dossiers temporaires/provisoires sont exclus.
5. Les caches sont exclus.
6. Les corbeilles sont exclues.
7. Les dossiers système/techniques connus sont exclus.
8. Les exclusions ajoutées par l'utilisateur sont respectées.
9. Un chemin hors racine OneDrive n'est jamais proposé.
10. Une destination normale reste autorisée.
11. La limite de suggestion à 4 niveaux reste appliquée.

## Preuve attendue

Compilation Release sans erreur + tous les tests Sprint 1 à 5 réussis.
