# Cahier des charges — Agent Atlas v1.0

## 1. Objectif

Installer une seule fois sur Windows 11 un agent autonome limité à Atlas Drop. Après cette installation, l’agent doit réceptionner un paquet de mise à jour, le contrôler, le tester, l’installer et produire un rapport sans intervention de l’utilisateur.

## 2. Périmètre v1

- Fonctionnement local sur Windows 11, sans droits administrateur permanents.
- Surveillance automatique d’un dossier `Inbox` dédié.
- Acceptation exclusive de paquets ZIP accompagnés d’un manifeste JSON.
- Vérification SHA-256 du paquet avant toute exécution.
- Blocage des chemins dangereux et des archives sortant du dossier de travail.
- Sauvegarde de la version installée avant modification.
- Exécution des commandes de validation déclarées dans le manifeste.
- Installation atomique dans `%LOCALAPPDATA%\AtlasDrop\app`.
- Test de santé après installation.
- Retour automatique à la version précédente en cas d’échec.
- Rapport lisible et journal technique dans `Reports`.
- Quarantaine des paquets invalides ou échoués.
- Une seule opération à la fois.

## 3. Hors périmètre

- Accès général ou interactif au PC.
- Lecture, classement ou transfert des documents personnels.
- Exécution arbitraire de commandes reçues d’Internet.
- Contournement des protections Windows.
- Mise à jour silencieuse non authentifiée depuis une source distante.

## 4. Architecture

Répertoire par défaut : `%LOCALAPPDATA%\AtlasAgent`.

- `Agent` : scripts installés et configuration.
- `Inbox` : paquets entrants.
- `Work` : extraction temporaire isolée.
- `Backups` : dernière version saine d’Atlas Drop.
- `Reports` : résultats JSON et journaux texte.
- `Quarantine` : paquets refusés ou échoués.
- `State` : verrou, version et état courant.

L’agent est lancé à l’ouverture de session par une tâche planifiée Windows. Il inspecte `Inbox` toutes les 30 secondes. La source peut être placée dans OneDrive si elle est configurée explicitement, mais l’installation d’un paquet reste soumise aux mêmes contrôles.

## 5. Format d’un paquet

Un paquet comprend :

- `AtlasDrop-<version>.zip`
- `AtlasDrop-<version>.manifest.json`

Le manifeste contient : version, nom du ZIP, SHA-256, exécutable attendu, commandes de validation autorisées et délai maximal. La v1 refuse les commandes autres que `dotnet restore`, `dotnet build`, `dotnet test` et l’auto-test d’un exécutable inclus.

## 6. Cycle automatique

1. Détecter un manifeste stable depuis au moins 10 secondes.
2. Prendre un verrou exclusif.
3. Valider le manifeste, le nom, la version et le SHA-256.
4. Extraire dans un dossier temporaire protégé.
5. Refuser toute traversée de chemin ou lien symbolique dangereux.
6. Exécuter les validations avec délai maximal.
7. Sauvegarder l’installation courante.
8. Arrêter uniquement `AtlasDrop.App`.
9. Basculer atomiquement vers la nouvelle version.
10. Exécuter le test de santé.
11. Confirmer la réussite ou restaurer automatiquement.
12. Produire un rapport et archiver le paquet.

## 7. Sécurité

- Aucun mot de passe dans les fichiers.
- Aucune commande PowerShell libre dans un manifeste.
- Aucun téléchargement HTTP non chiffré.
- Empreinte SHA-256 obligatoire.
- Taille maximale par défaut : 500 Mo.
- Conservation de trois sauvegardes maximum.
- Journaux sans contenu de documents utilisateur.
- Arrêt immédiat en cas d’ambiguïté.

Pour une future source Internet, une signature numérique Authenticode ou une signature Ed25519 sera obligatoire. SHA-256 seul vérifie l’intégrité, pas l’identité de l’expéditeur.

## 8. Critères d’acceptation

- Installation initiale en un double-clic.
- Redémarrage Windows : agent relancé automatiquement.
- Paquet valide : installation et rapport `SUCCESS` sans clic.
- Mauvais SHA-256 : aucune modification, paquet en quarantaine.
- Test en échec : ancienne version restaurée.
- Coupure pendant installation : état récupérable au prochain démarrage.
- Deux paquets simultanés : traitement séquentiel.
- Aucun fichier hors des répertoires Atlas n’est modifié.

## 9. Limite réelle

L’agent rend l’exécution et le diagnostic autonomes sur le PC. Il ne crée pas à lui seul un canal entre ChatGPT Work et Windows. Pour déposer automatiquement les futures versions, il faudra relier `Inbox` à une source synchronisée autorisée (par exemple un dossier OneDrive dédié ou un dépôt de versions signé).
