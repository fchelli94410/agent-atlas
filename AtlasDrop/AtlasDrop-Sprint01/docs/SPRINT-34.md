# Sprint 34 — Niveaux de confiance

## Critères d'acceptation automatisables

1. Score converti en confiance faible / moyenne / élevée.
2. Seuil faible par défaut : < 0,45.
3. Seuil moyen : 0,45 à < 0,75.
4. Seuil élevé : >= 0,75.
5. Confiance faible : jamais de classement automatique.
6. Confiance moyenne : validation utilisateur obligatoire.
7. Confiance élevée : peut autoriser l'automatisation.
8. Score borné entre 0 et 1.
9. Seuils configurables.
10. Les dossiers exclus restent en confiance faible.
11. Les niveaux > 4 restent en confiance faible et non automatisables.
12. Le résultat du scoring embarque son niveau de confiance.
13. Aucun fichier utilisateur modifié.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 34 réussis.
