# Sprint 41 — Renommage automatique haute confiance

## Critères d'acceptation automatisables

1. Le renommage automatique est une politique séparée.
2. Il nécessite une confiance élevée.
3. Il nécessite `CanAutoClassify = true`.
4. Il nécessite l'option utilisateur activée.
5. Confiance moyenne : jamais automatique.
6. Confiance faible : jamais automatique.
7. Si le nom proposé est identique, aucun renommage automatique.
8. L'interface affiche l'état d'autorisation.
9. Une case permet d'activer/désactiver l'option.
10. Aucun renommage physique de fichier dans ce sprint : la décision est prête pour les opérations sûres ultérieures.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 41 réussis.
