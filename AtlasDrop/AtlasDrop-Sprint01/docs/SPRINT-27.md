# Sprint 27 — Indexation des fichiers déjà classés

## Critères d'acceptation automatisables

1. Profil de dossier calculé à partir des fichiers existants.
2. Nombre de fichiers conservé.
3. Répartition des types documentaires calculée.
4. Répartition des extensions calculée sans sensibilité à la casse.
5. Mots-clés fréquents agrégés.
6. Un mot-clé ne compte qu'une fois par fichier.
7. Mots vides et nombres seuls ignorés.
8. Nombre maximal de mots-clés configurable.
9. Type `Unknown` non utilisé comme signal métier.
10. Collection vide gérée proprement.
11. Aucun fichier utilisateur modifié.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 27 réussis.
