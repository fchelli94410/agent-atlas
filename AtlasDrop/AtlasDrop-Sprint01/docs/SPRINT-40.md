# Sprint 40 — Renommage proposé

## Critères d'acceptation automatisables

1. Format cible : date - type - lieu - détail.ext.
2. Date exacte -> AAAA-MM-JJ.
3. Précision mois -> AAAA-MM, sans jour inventé.
4. Précision année -> AAAA, sans mois inventé.
5. Extension conservée.
6. Type documentaire traduit en libellé lisible.
7. Lieu intégré si disponible.
8. Détail > entreprise > référence comme dernier segment utile.
9. Caractères Windows interdits nettoyés.
10. Noms parasites remplacés si des métadonnées utiles existent.
11. Sans métadonnées, le nom d'origine est seulement nettoyé.
12. Nom proposé limité en longueur.
13. Le nom proposé alimente le champ de renommage de l'interface.
14. Aucun renommage réel de fichier dans ce sprint.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 40 réussis.
