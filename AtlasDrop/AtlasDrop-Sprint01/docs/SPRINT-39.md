# Sprint 39 — Création sécurisée de dossier

## Critères d'acceptation automatisables

1. Création uniquement sous la racine OneDrive autorisée.
2. Nom vide refusé.
3. Caractères Windows interdits refusés.
4. Noms réservés Windows refusés.
5. Dossiers exclus refusés.
6. Dossier existant refusé.
7. Parent hors racine refusé.
8. Niveau 4 autorisé.
9. Niveau 5 refusé à la création depuis Atlas Drop.
10. Chemins trop longs refusés.
11. Accès refusé / I/O gérés sans crash.
12. Annulation prise en compte.
13. Dossier créé vérifié immédiatement.
14. Dossier créé devient la destination manuelle sélectionnée.
15. Aucun fichier utilisateur déplacé dans ce sprint.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 39 réussis.
