# Sprint 17 — OCR PDF scannés

## Critères d'acceptation automatisables

1. OCR déclenché si aucun texte PDF exploitable.
2. OCR déclenché si le texte natif est insuffisant.
3. OCR non déclenché si le texte PDF natif est suffisant.
4. Nombre maximal de pages OCR configurable.
5. Texte OCR concaténé avec numéro de page.
6. Limite de caractères configurable.
7. Annulation respectée.
8. PDF source jamais modifié.
9. Images temporaires supprimées après traitement.
10. Tests isolés sous `%TEMP%`.

## Limite réelle

Le rendu réel PDF -> image dépendra du moteur de rendu retenu/packagé sur Windows.
Ce sprint valide l'orchestration et les règles de déclenchement OCR sans prétendre valider ce rendu natif final.

## Preuve attendue

Compilation Release sans erreur + tous les tests Sprint 1 à 17 réussis.
