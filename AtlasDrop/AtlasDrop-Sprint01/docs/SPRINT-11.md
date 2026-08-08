# Sprint 11 — Extraction TXT / CSV

## Critères d'acceptation automatisables

1. TXT pris en charge.
2. CSV pris en charge.
3. UTF-8 sans BOM pris en charge.
4. UTF-8 avec BOM pris en charge sans conserver le caractère BOM.
5. UTF-16 pris en charge.
6. Windows-1252 pris en charge pour les anciens CSV Windows.
7. Les gros fichiers sont limités à une taille maximale configurable.
8. Le résultat indique si l'extraction a été tronquée.
9. Une extension non supportée est refusée clairement.
10. Un fichier absent est refusé clairement.
11. L'annulation est respectée.
12. Les tests restent sous `%TEMP%` et ne touchent jamais OneDrive.

## Preuve attendue

Compilation Release sans erreur + tous les tests Sprint 1 à 11 réussis.
