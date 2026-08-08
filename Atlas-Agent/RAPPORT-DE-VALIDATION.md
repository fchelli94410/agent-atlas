# Rapport de validation — Agent Atlas v1.1

Date : 8 août 2026

## Réalisé

- Cahier des charges complet.
- Plan de 12 sprints avec critères d’acceptation.
- Installation utilisateur et tâche planifiée.
- Arborescence isolée Atlas Agent.
- Surveillance automatique de l’Inbox.
- Contrat de manifeste fermé.
- Vérification SHA-256.
- Limite de taille.
- Protection contre la traversée de chemin ZIP.
- Liste blanche des étapes de validation.
- Compilation et tests Atlas Drop avant installation.
- Permutation atomique de l’application.
- Test de santé, redémarrage et restauration de l’ancienne version.
- Rapports JSON, journaux, quarantaine et rétention des sauvegardes.
- Créateur de paquets.
- Test automatique Windows sans action pendant l’exécution.
- Connexion GitHub unique par navigateur sans mot de passe dans les scripts.
- Surveillance du dépôt privé et de la branche `main` toutes les cinq minutes.
- Téléchargement limité au dépôt `fchelli94410/agent-atlas`.
- Création locale d’un paquet avec SHA-256 avant validation et installation.

## Contrôles exécutés dans Work

- Présence de tous les livrables : réussie.
- Validité du fichier JSON : réussie.
- GitHub désactivé par défaut avant autorisation : réussi.
- Dépôt et branche limités dans la configuration : réussi.
- Recherche de téléchargement réseau ou d’exécution dynamique : aucun mécanisme trouvé.
- Vérification des chemins destructifs larges : aucun effacement de dossier utilisateur général.
- Archive finale et inventaire : à produire après ce rapport.

## Non validable dans Work

Work fonctionne sous Linux et ne possède ni Windows PowerShell 5.1, ni le Planificateur de tâches Windows, ni le Bureau Windows. La connexion par navigateur et le comportement Windows réel ne peuvent donc pas être certifiés ici.

Le paquet est une version de développement complète, mais il ne doit pas être présenté comme validé sur le PC tant que le test Windows automatique n’a pas produit `TEST AUTOMATIQUE REUSSI` et que la connexion GitHub n’a pas affiché `CONNEXION REUSSIE`.

## Limite du zéro-intervention

Après l’installation initiale et la connexion GitHub unique, l’agent peut récupérer seul les changements de `main`. Le retour automatique des diagnostics vers GitHub reste hors périmètre de la v1.1.
