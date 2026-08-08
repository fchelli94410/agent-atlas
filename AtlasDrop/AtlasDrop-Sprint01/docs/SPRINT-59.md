# Sprint 59 — Installateur / désinstallateur / tests Windows

## Critères d'acceptation

1. Projet `AtlasDrop.Installer` ajouté.
2. Installation par utilisateur dans `%LOCALAPPDATA%\AtlasDrop\App`.
3. Aucun droit administrateur requis.
4. Payload validé avant toute modification.
5. Copie complète de l'application publiée.
6. Menu clic droit enregistré automatiquement après installation.
7. Désinstallation supprime l'application et le menu contextuel.
8. Les logs/données utilisateur ne sont pas supprimés implicitement.
9. Aucun écrasement silencieux hors du dossier Atlas Drop dédié.
10. Publication `win-x64` self-contained.
11. L'installateur final est un EXE.
12. Auto-test du payload sans toucher au registre.
13. Solution `.sln` reconstruite proprement avec tous les projets.
14. Tests d'installation utilisent des doublures et dossiers temporaires.
15. Le Sprint 60 produira les livrables finaux et le rapport complet.
