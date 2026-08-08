# Sprint 16 — OCR images Tesseract

## Critères d'acceptation automatisables

1. JPG/JPEG/PNG/TIF/TIFF reconnus.
2. Service OCR local basé sur Tesseract intégré.
3. Langue française configurable.
4. Mode français + anglais possible.
5. Présence des fichiers `.traineddata` vérifiée avant OCR.
6. Confiance OCR remontée entre 0 et 1.
7. Limite de texte OCR configurable.
8. Annulation respectée.
9. L'image source n'est jamais modifiée.
10. Les tests d'orchestration OCR utilisent un backend simulé et restent sous `%TEMP%`.

## Limite réelle de ce sprint

La compilation valide l'intégration du moteur Tesseract natif, mais les tests automatisés ne chargent pas encore un vrai modèle `fra.traineddata`. Le test OCR réel avec les données linguistiques sera ajouté avec le packaging des ressources OCR et les vérifications Windows correspondantes.

## Preuve attendue

Compilation Release sans erreur + tous les tests Sprint 1 à 16 réussis.
