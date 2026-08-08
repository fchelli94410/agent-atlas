# Agent Atlas v1.0

Agent Windows autonome de validation, installation et restauration d’Atlas Drop.

## Installation

Double-cliquer une seule fois sur `INSTALLER-AGENT-ATLAS.cmd`.

L’agent est ensuite lancé automatiquement à chaque ouverture de session et surveille `%LOCALAPPDATA%\AtlasAgent\Inbox`.

## Création d’un paquet

Depuis le dossier du projet Atlas Drop :

```powershell
powershell -ExecutionPolicy Bypass -File .\Creer-Paquet-AtlasDrop.ps1 -SourceFolder "C:\Projet\AtlasDrop" -Version "1.0.9"
```

Copier le ZIP et son manifeste dans l’Inbox. L’agent réalise le reste automatiquement.

## Résultats

- Rapports : `%LOCALAPPDATA%\AtlasAgent\Reports`
- Paquets refusés : `%LOCALAPPDATA%\AtlasAgent\Quarantine`
- Sauvegardes : `%LOCALAPPDATA%\AtlasAgent\Backups`

## État de validation

Le code et les contrôles statiques sont livrés. Le comportement Windows réel, la tâche planifiée et le rollback devront être validés sur Windows ; ce contrôle est automatisé par `TEST-AUTOMATIQUE-AGENT-ATLAS.cmd` et ne demande aucune manipulation pendant son exécution.
