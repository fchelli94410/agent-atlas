# Sprint 50 — Historique des opérations

## Critères d'acceptation automatisables

1. Historique structuré des opérations.
2. Source conservée dans l'entrée d'historique.
3. Ancien et nouveau nom enregistrés.
4. Ancienne et nouvelle destination enregistrées.
5. Date UTC enregistrée.
6. Résultat succès/échec enregistré.
7. Message résultat enregistré.
8. SHA-256 enregistré quand disponible.
9. État `Undone` présent pour préparer Sprint 51.
10. Historique récent limité à 10 opérations par défaut.
11. Ordre du plus récent au plus ancien.
12. Les échecs sont eux aussi historisés.
13. Aucun mot de passe, token ou contenu complet de fichier stocké.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 50 réussis.
