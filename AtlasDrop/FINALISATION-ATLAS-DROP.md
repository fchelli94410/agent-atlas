# Finalisation Atlas Drop — cahier des charges figé

Date : 8 août 2026  
Base : Atlas Drop v1.0.8  
Cible : Windows 11, C# / .NET 8 / WPF, OneDrive `C:\Users\fchelli\OneDrive - ALTEDIS`.

## Parcours obligatoire

1. Activation depuis l’Explorateur Windows sur un fichier ou un dossier.
2. Analyse du nom et, lorsque possible, du contenu.
3. Affichage de trois destinations OneDrive uniques et pertinentes avec score de confiance.
4. Proposition de renommage distincte de la destination.
5. Aucune opération tant que l’utilisateur n’a pas explicitement validé.
6. Déplacement sécurisé avec contrôle du résultat et conservation des dates.
7. Question après déplacement : « Le classement est-il conforme ? »
8. Apprentissage uniquement à partir d’une validation humaine confirmée.

## Commandes de l’interface

- **OUI** : valide la destination affichée puis déplace.
- **NON** : ne déplace rien et remonte la proposition d’un niveau.
- **CHOISIR / Choisir un autre dossier** : ouvre la navigation manuelle.
- **DÉPLACER ICI** : valide explicitement le dossier manuel puis déplace.
- **ANNULER** : ferme et réinitialise sans déplacer ni apprendre.
- Aucun déplacement n’est autorisé sans clic explicite.

## Suggestions et profondeur

- Trois suggestions différentes ; aucun doublon.
- Seuil minimal de confiance : 30 %.
- Ne jamais utiliser le terme générique `document` comme suggestion utile.
- Priorités : entreprise, lieu, année, type de document, mots significatifs.
- Parcours OneDrive limité à cinq niveaux dans la v1.0.8.
- Exclure des suggestions automatiques : Bureau, Documents, Images, `99 - Archives`, dossiers système, temporaires, cache, corbeille et exclusions configurées.

## Renommage

- Ne jamais inventer une date précise.
- Année seule connue : `2025 - ...`.
- Année et mois connus : `2025-01 - ...`.
- Date complète uniquement lorsqu’elle est réellement présente.
- Séparer correctement lieu et société, notamment `Charenton le Pont` et `ETHICA GESTION`.
- Le renommage proposé doit rester modifiable et soumis à validation.

## Après déplacement

- Vérifier que la cible existe réellement.
- Afficher « Classement réussi ».
- Demander si le classement est conforme.
- **OUI** : enregistrer l’apprentissage positif.
- **NON** : ne pas apprendre ; proposer une correction sûre avec possibilité de restaurer la source avant un nouveau classement.
- Conserver un historique permettant d’identifier sans ambiguïté le dernier déplacement.

## Sécurité et robustesse

- Précontrôle source, destination, nom Windows et longueur de chemin.
- Gestion prudente des doublons : remplacer, renommer automatiquement ou annuler.
- Confirmation renforcée pour les gros dossiers.
- Sauvegarde / rollback automatique en cas d’échec.
- Sons distincts : lancement, réussite, erreur.
- Journaux techniques sans contenu personnel.
- Une seule opération à la fois.

## Validation finale obligatoire

- Compilation Release : zéro erreur et zéro avertissement.
- Tous les tests automatisés réussis.
- Installation complète via Agent Atlas.
- Test Windows réel : activation Explorateur/Bureau, analyse, trois suggestions, renommage, OUI, NON, CHOISIR, DÉPLACER ICI, ANNULER, confirmation après déplacement, apprentissage, doublon et rollback.
- Aucun point n’est déclaré terminé sans preuve.
