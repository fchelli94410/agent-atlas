# Sprint 58 — Performance, cache et robustesse

## Critères d'acceptation automatisables

1. Cache mémoire TTL thread-safe.
2. Expiration automatique des entrées.
3. Suppression ciblée et vidage du cache.
4. Politique de retry avec backoff exponentiel.
5. Retry uniquement sur erreurs explicitement autorisées.
6. Respect du CancellationToken.
7. Limiteur de concurrence asynchrone.
8. Aucune modification des règles de suggestion.
9. `MaxSuggestedDepth = 4` conservé.
10. Aucun écrasement de fichier toujours garanti.
11. Aucun retry ne contourne exclusions ou validations.
12. Tous les tests antérieurs restent verts.

## Préparation Sprint 59

Le Sprint 59 ajoutera le vrai packaging installable Windows et l'uninstall propre.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 58 réussis.
