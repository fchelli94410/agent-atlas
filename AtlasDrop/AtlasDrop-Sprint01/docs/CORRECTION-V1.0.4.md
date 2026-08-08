# Correction V1.0.4 — qualité des suggestions

- suppression du mot générique `Document` dans la recherche automatique ;
- suppression de mots faibles du nom de fichier (`avis`, `document`, `copie`, `scan`, etc.) ;
- priorité aux indices entreprise / lieu / année / type réellement détectés ;
- déduplication par chemin complet ;
- filtrage des suggestions automatiques de score < 30 % ;
- nettoyage d'un lieu si le nom de l'entreprise a été concaténé à sa fin ;
- recherche manuelle dédupliquée.
