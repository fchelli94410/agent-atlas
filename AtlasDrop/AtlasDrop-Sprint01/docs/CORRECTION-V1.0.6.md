# Atlas Drop v1.0.6

## Corrections

- Conservation de la précision réelle des dates détectées.
- Une année produit `AAAA`; un mois produit `AAAA-MM`; seul un jour réellement détecté produit `AAAA-MM-JJ`.
- Suppression du repli trompeur sur la date de création du fichier.
- Blocage des faux noms de société contenant une extension de fichier.
- Interdiction de construire une société en traversant un retour à la ligne.
- Nettoyage des lieux qui absorbaient une société en majuscules.

## Tests de non-régression ajoutés

- nom de fichier DOCX suivi d'une SCI sur la ligne suivante ;
- ville suivie d'une entreprise sur la ligne suivante ;
- ville suivie d'une entreprise en majuscules sur la même ligne.
