# Atlas Drop v1.0.7

## Parcours validé

1. Clic molette sur un fichier ou un dossier sous la souris dans l'Explorateur Windows.
2. Une réaction visuelle apparaît, puis Atlas Drop analyse l'élément.
3. Une nouvelle fenêtre de l'Explorateur ouvre le dossier OneDrive proposé.
4. Aucun déplacement n'est exécuté sans `OUI` ou `DÉPLACER ICI`.
5. `NON` remonte au dossier parent ; `CHOISIR` ouvre la navigation ; `ANNULER` abandonne.

## Règles techniques

- une seule opération à la fois ;
- index local persistant, rafraîchi en arrière-plan, profondeur maximale 5 ;
- fichier : nom et contenu PDF, Word, Excel, PowerPoint ou texte ;
- dossier : nom et noms des éléments directement contenus, sans analyser les sous-dossiers ;
- en cas d'incertitude, proposition du parent sûr ou de la racine OneDrive ;
- apprentissage local durable à partir des validations, refus et choix manuels ;
- conflit : remplacer, renommer automatiquement (choix recommandé par défaut) ou annuler ;
- confirmation si un dossier contient plus de 10 éléments, sous-dossiers compris ;
- conservation des dates d'origine des fichiers ;
- confirmation sonore et visuelle après vérification du déplacement ;
- fermeture après deux secondes pour un fichier, maintien ouvert pour un dossier.

## Validation

Le script unique compile la solution, lance tous les tests, publie l'application autonome, installe la nouvelle version après fermeture de l'ancienne instance, crée le raccourci Bureau et vérifie que le processus reste lancé.

Le clic molette global et le positionnement réel de l'Explorateur nécessitent obligatoirement le test final sous Windows 11.
