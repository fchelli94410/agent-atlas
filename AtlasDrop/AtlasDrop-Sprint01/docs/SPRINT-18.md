# Sprint 18 — Analyse MSG Outlook

## Critères d'acceptation automatisables

1. `.msg` reconnu.
2. Lecture locale sans automatiser Outlook.
3. Sujet extrait.
4. Expéditeur extrait.
5. Destinataires To/Cc extraits lorsque disponibles.
6. Date d'envoi extraite lorsque disponible.
7. Corps texte utilisé en priorité.
8. HTML converti en texte si nécessaire.
9. Taille du corps limitée.
10. Annulation respectée.
11. MSG source jamais modifié.
12. Aucune pièce jointe n'est ouverte, exécutée ou extraite.
13. Tests isolés sous `%TEMP%`.

## Limite réelle

Les tests automatisés valident l'orchestration avec un backend simulé.
La compilation Windows valide la présence de MsgReader. Un test d'intégration avec un vrai fichier `.msg`
sera ajouté aux validations de fichiers réels contrôlés, sans utiliser les messages personnels de l'utilisateur.

## Preuve attendue

Compilation Release sans erreur + tous les tests Sprint 1 à 18 réussis.
