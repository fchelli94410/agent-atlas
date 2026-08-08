# Sprint 55 — Menu clic droit Windows

## Critères d'acceptation automatisables

1. Libellé exact : `Ranger avec Atlas Drop`.
2. Disponible sur les fichiers (`HKCU\Software\Classes\*\shell`).
3. Installation par utilisateur courant, sans administrateur.
4. Chemin de l'EXE toujours entouré de guillemets.
5. Chemin du fichier sélectionné transmis via `"%1"`.
6. Désinstallation du menu supportée.
7. Aucun changement HKLM.
8. Service d'enregistrement intégré dans Infrastructure.
9. Scripts d'installation/désinstallation fournis.
10. Compatible avec l'instance unique du Sprint 54.
11. L'installateur final automatisera cette étape au Sprint 59.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 55 réussis.
