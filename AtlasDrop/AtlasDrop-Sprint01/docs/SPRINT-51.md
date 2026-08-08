# Sprint 51 — Annulation sécurisée

## Critères d'acceptation automatisables

1. Seules les opérations réussies peuvent être annulées.
2. Une opération déjà annulée est refusée.
3. Le fichier doit encore exister à sa destination actuelle.
4. Le chemin d'origine doit être libre.
5. Aucun écrasement pendant l'annulation.
6. Si SHA-256 disponible, le fichier doit être inchangé.
7. Fichier modifié => annulation refusée.
8. Retour physique vers le chemin d'origine.
9. Vérification post-annulation.
10. Historique marqué `Undone = true`.
11. Annulation impossible signalée sans suppression.
12. Annulation annulée via CancellationToken sans corruption.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 51 réussis.
