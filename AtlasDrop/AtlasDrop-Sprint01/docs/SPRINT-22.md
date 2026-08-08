# Sprint 22 — Détection des entreprises

## Critères d'acceptation automatisables

1. Entreprises avec forme juridique détectées.
2. SAS/SASU/SARL/EURL/SA/SCI/SNC/SELARL/SCP prises en charge.
3. Labels explicites `Société`, `Entreprise`, `Fournisseur`, `Client` détectés.
4. Domaines d'e-mail professionnels utilisables comme signal.
5. Domaines grand public ignorés.
6. Variantes accentuées normalisées.
7. Formes juridiques retirées du nom normalisé.
8. Doublons de la même entreprise fusionnés.
9. Le signal le plus fiable est conservé.
10. Aucun fichier utilisateur modifié.

## Preuve attendue

Compilation Release sans erreur + tous les tests Sprint 1 à 22 réussis.
