# Sprint 57 — Serilog et rotation

## Critères d'acceptation automatisables

1. Serilog intégré.
2. Logs locaux sous `%LOCALAPPDATA%\AtlasDrop\Logs`.
3. Rotation quotidienne.
4. Rotation aussi sur limite de taille.
5. Taille max par fichier : 5 Mo.
6. Conservation : 14 fichiers.
7. Fichiers partageables en lecture.
8. Messages multilignes aplatis.
9. Messages tronqués à 500 caractères.
10. Aucun contenu complet de document n'est journalisé.
11. Aucun mot de passe/token n'est requis ou journalisé.
12. Démarrage, instance secondaire et fermeture journalisés.
13. Tests isolés dans dossier temporaire.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 57 réussis.
