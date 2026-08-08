# Sprint 31 — Scoring v1 des dossiers

## Critères d'acceptation automatisables

1. Score borné entre 0 et 1.
2. Nom de fichier et contenu peuvent renforcer un dossier.
3. Mots-clés, lieux, entreprises et années pris en compte.
4. Type documentaire déjà présent dans le dossier renforce le score.
5. Choix précédents renforcent le score.
6. Utilisation récente renforce légèrement le score.
7. Qualité du dossier prise en compte.
8. Dossiers génériques pénalisés.
9. Dossiers explicitement exclus : score 0 et jamais proposés.
10. Arborescence indexée entièrement mais profondeur > 4 : score 0 et jamais proposée automatiquement.
11. Niveau 4 reste éligible.
12. Maximum 3 suggestions par défaut.
13. Aucun fichier utilisateur modifié.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 31 réussis.
