# Sprint 45 — SHA-256 et doublon exact

## Critères d'acceptation automatisables

1. Service SHA-256 local.
2. Même contenu = même hash.
3. Contenu différent = hash différent.
4. Hash encodé sur 64 caractères hexadécimaux.
5. Si même nom + même taille, comparaison SHA-256.
6. Hash identique => `ExactDuplicate`.
7. Hash différent => reste `SameSize`.
8. Échec de lecture hash n'empêche pas le signal même taille.
9. Aucun doublon supprimé automatiquement.
10. Aucun écrasement autorisé.
11. Aucun fichier déplacé dans ce sprint.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 45 réussis.
