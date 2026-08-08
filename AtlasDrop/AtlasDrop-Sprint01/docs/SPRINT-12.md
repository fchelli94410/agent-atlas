# Sprint 12 — Extraction DOCX

## Critères d'acceptation automatisables

1. `.docx` reconnu.
2. Lecture sans Microsoft Word installé.
3. Extraction des paragraphes.
4. Prise en compte des tabulations et retours à la ligne.
5. Limite maximale de caractères configurable.
6. Document volumineux tronqué proprement.
7. Fichier absent refusé clairement.
8. Mauvaise extension refusée.
9. Annulation respectée.
10. Le DOCX source n'est jamais modifié.
11. Les tests utilisent uniquement `%TEMP%`.

## Preuve attendue

Compilation Release sans erreur + tous les tests Sprint 1 à 12 réussis.
