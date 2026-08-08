# Sprint 19 — Inspection ZIP sécurisée

## Critères d'acceptation automatisables

1. `.zip` reconnu.
2. Inspection sans extraction.
3. Traversées `../` détectées.
4. Chemins absolus détectés.
5. Nombre maximal d'entrées limité.
6. Taille décompressée totale limitée.
7. Ratio de compression suspect détecté.
8. Annulation respectée.
9. ZIP source jamais modifié.
10. Aucun fichier contenu dans l'archive n'est exécuté ou extrait automatiquement.
11. Tests uniquement sous `%TEMP%`.

## Preuve attendue

Compilation Release sans erreur + tous les tests Sprint 1 à 19 réussis.
