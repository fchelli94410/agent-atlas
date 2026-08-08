# Correction V1.0.1 — branchement réel

Cette correction retire le dernier câblage d'aperçu/démonstration de la fenêtre WPF.

## Corrigé

- suppression des dossiers EDF/Courbevoie de démonstration ;
- fichier reçu par le clic droit réellement conservé et analysé ;
- extraction réelle TXT/CSV/DOCX/XLSX/PPTX/PDF ;
- classification réelle ;
- détection réelle entreprise / lieu / date ;
- lecture des vrais dossiers du OneDrive configuré ;
- recherche manuelle sur les vrais dossiers ;
- suggestions issues des vrais dossiers, avec profondeur automatique <= 4 ;
- exclusions appliquées ;
- renommage basé sur l'analyse réelle ;
- bouton Classer relié au `SafeFileMoveService` ;
- création dossier utilise la vraie racine OneDrive.

## Important

Le test fonctionnel doit se faire d'abord avec une COPIE d'un fichier, jamais avec un original important.
