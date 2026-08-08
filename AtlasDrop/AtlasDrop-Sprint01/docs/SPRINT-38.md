# Sprint 38 — Recherche manuelle

## Critères d'acceptation automatisables

1. Recherche manuelle branchée à l'interface.
2. Réutilise le moteur des Sprints 28 à 30.
3. Insensible casse/accent.
4. Multi-mots.
5. Tolérance fautes.
6. Résultats limitables.
7. Résultats affichés avec chemin complet.
8. Entrée dans le champ de recherche déclenche la recherche.
9. Sélection d'un résultat définit une destination manuelle.
10. Sélection d'une suggestion automatique efface la destination manuelle.
11. Aucun déplacement de fichier réel dans ce sprint.

## Note sécurité

La recherche manuelle peut afficher des dossiers indexés profonds. La règle profondeur <= 4 reste celle des suggestions automatiques ; les validations de destination réelles restent gérées par les règles de sécurité des sprints suivants.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 38 réussis.
