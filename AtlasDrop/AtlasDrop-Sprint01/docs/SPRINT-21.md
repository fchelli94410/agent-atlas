# Sprint 21 — Détection des dates

## Critères d'acceptation automatisables

1. Dates ISO `AAAA-MM-JJ` détectées.
2. Dates françaises `JJ/MM/AAAA` détectées.
3. Dates longues françaises détectées.
4. Mois + année détectés sans inventer de jour.
5. Année seule détectée sans inventer de mois/jour.
6. Dates calendaires impossibles rejetées.
7. Une date complète ne génère pas de doublons mois/année.
8. Plusieurs dates indépendantes peuvent être conservées.
9. Confiance plus forte pour une date précise.
10. Aucun fichier utilisateur modifié.

## Preuve attendue

Compilation Release sans erreur + tous les tests Sprint 1 à 21 réussis.
