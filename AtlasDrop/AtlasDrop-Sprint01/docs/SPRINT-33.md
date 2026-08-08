# Sprint 33 — Signaux d'historique utilisateur

## Critères d'acceptation automatisables

1. Un choix précédent renforce un dossier.
2. Un refus affaiblit un dossier.
3. Un undo est un signal négatif plus fort.
4. Une correction de renommage est mémorisable comme signal léger.
5. Un dossier créé par l'utilisateur est un signal positif léger.
6. Les signaux récents pèsent plus que les anciens.
7. Le contexte correspondant renforce davantage le signal.
8. L'ajustement est borné entre -0,30 et +0,30.
9. L'historique d'un autre dossier est ignoré.
10. L'historique ne peut jamais contourner une exclusion.
11. L'historique ne peut jamais contourner la profondeur automatique max 4.
12. Aucun fichier utilisateur modifié.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 33 réussis.
