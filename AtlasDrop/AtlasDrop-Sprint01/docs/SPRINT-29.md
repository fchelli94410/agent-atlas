# Sprint 29 — Recherche multi-mots

## Critères d'acceptation automatisables

1. Requête découpée en plusieurs termes normalisés.
2. Termes identiques dédupliqués.
3. Correspondance possible dans le nom.
4. Correspondance possible dans le chemin.
5. Correspondance possible dans le contenu indexé.
6. Les termes peuvent être répartis entre plusieurs champs.
7. Correspondance complète mieux classée qu'une correspondance partielle.
8. Correspondance exacte du nom complet conserve la priorité absolue.
9. Accents, casse et aliases toujours supportés.
10. Limite de résultats respectée.
11. Aucun fichier utilisateur modifié.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 29 réussis.
