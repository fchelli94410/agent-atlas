# Plan de développement — Agent Atlas

## Sprint 1 — Socle et dossiers

Créer l’arborescence, la configuration, les journaux et le verrou d’instance.

Validation : relance idempotente, aucun dossier hors périmètre.

## Sprint 2 — Installation unique

Créer l’installateur utilisateur, la tâche planifiée et la désinstallation.

Validation : installation sans administrateur et démarrage à l’ouverture de session.

## Sprint 3 — Contrat de paquet

Définir le manifeste, les règles de nommage, la taille maximale et les versions.

Validation : manifestes incomplets ou inconnus refusés.

## Sprint 4 — Contrôles d’intégrité

Ajouter SHA-256, stabilité du fichier, protection ZIP Slip et quarantaine.

Validation : paquet altéré ou archive dangereuse ne touche jamais l’application.

## Sprint 5 — Moteur de validation

Autoriser uniquement les étapes connues : restore, build, test et auto-test.

Validation : commande libre refusée ; délai maximal appliqué.

## Sprint 6 — Sauvegarde et installation atomique

Sauvegarder l’application, préparer la nouvelle version puis permuter les dossiers.

Validation : pas d’installation partielle visible.

## Sprint 7 — Santé et rollback

Lancer le test de santé et restaurer automatiquement en cas d’échec.

Validation : panne simulée, ancienne version fonctionnelle restaurée.

## Sprint 8 — Rapports automatiques

Produire un JSON machine et un journal lisible, sans données personnelles.

Validation : chaque traitement possède un identifiant et un résultat explicite.

## Sprint 9 — Robustesse

Gérer redémarrage, verrou abandonné, paquets multiples, nettoyage et rétention.

Validation : reprise après interruption et conservation de trois sauvegardes.

## Sprint 10 — Livraison v1

Créer le paquet d’installation unique, le test autonome et la documentation.

Validation : contrôle statique complet et test Windows final en une seule exécution automatisée.

## Sprint 11 — Canal distant signé (v1.1)

Relier l’Inbox à une source HTTPS/OneDrive autorisée et exiger une signature numérique.

Validation : aucune version distante non signée ne peut être installée.

État : implémenté avec authentification GitHub locale, dépôt privé fixe, branche `main`, contrôle SHA-256 et validations locales. Test Windows final en attente.

## Sprint 12 — Retour de diagnostics (v1.1)

Transmettre uniquement les rapports Atlas vers une destination autorisée.

Validation : aucune donnée utilisateur hors rapports n’est envoyée.
