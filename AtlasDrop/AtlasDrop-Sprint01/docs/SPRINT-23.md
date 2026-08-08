# Sprint 23 — Détection des lieux

## Critères d'acceptation automatisables

1. Ville avec code postal détectée.
2. Ville dans une adresse détectée.
3. Labels explicites `Ville`, `Lieu`, `Commune`, `Site`, `Agence`.
4. Dictionnaire minimal de lieux fréquents.
5. Variantes avec accents normalisées.
6. `Saint-Maurice`, `Saint Maurice` et variantes équivalentes.
7. Code postal conservé lorsqu'il existe.
8. Doublons fusionnés.
9. Source la plus fiable prioritaire.
10. Aucun fichier utilisateur modifié.

## Preuve attendue

Compilation Release sans erreur + tous les tests Sprint 1 à 23 réussis.
