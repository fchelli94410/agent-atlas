# Sprint 25 — Détection factures et montants

## Critères d'acceptation automatisables

1. Numéros de facture détectés.
2. Montants EUR détectés avec virgule ou point.
3. Montants USD et GBP détectés.
4. Symbole monétaire avant ou après le montant.
5. Espaces de milliers tolérés.
6. Montants identiques dédupliqués.
7. Signaux `facture`, `invoice`, `total TTC`, `net à payer`, TVA, échéance, règlement, IBAN.
8. Un montant seul ne suffit pas à classifier une facture.
9. Numéro de facture + signaux forts augmentent la confiance.
10. Aucun fichier utilisateur modifié.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 25 réussis.
