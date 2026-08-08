# Agent Atlas v1.1

Agent Windows autonome de validation, installation et restauration d’Atlas Drop.

## Installation recommandée

Double-cliquer une seule fois sur `INSTALLER-ET-CONNECTER-AGENT-ATLAS.cmd`, puis valider la connexion dans le navigateur.

Les fichiers d'installation et de connexion séparés restent disponibles uniquement pour le diagnostic.

L’agent est ensuite lancé automatiquement à chaque ouverture de session et surveille `%LOCALAPPDATA%\AtlasAgent\Inbox`.

Toutes les cinq minutes, il verifie aussi la branche `main` du depot prive `fchelli94410/agent-atlas`. Une nouvelle version est telechargee, controlee, compilee, testee puis installee avec restauration automatique en cas d'echec.

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

Le code et les contrôles statiques sont livrés. Le comportement Windows réel, la tâche planifiée, la connexion GitHub et le rollback devront être validés sur Windows.
