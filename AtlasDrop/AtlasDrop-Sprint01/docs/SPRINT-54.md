# Sprint 54 — Instance unique et transmission fichier

## Critères d'acceptation automatisables

1. Mutex Windows pour une seule instance principale.
2. Deuxième instance détectée.
3. Deuxième instance transmet le fichier à la première.
4. Canal local via named pipe.
5. Aucun réseau ni serveur externe.
6. Chemin transmis normalisé.
7. Première instance écoute les demandes suivantes.
8. Fenêtre restaurée/activée quand un nouveau fichier arrive.
9. Le fichier reçu remplace le fichier courant de l'interface.
10. Deuxième instance se ferme après transmission.
11. Gestion propre de l'arrêt et annulation de l'écoute.
12. Préparation directe du menu clic droit du Sprint 55.

## Preuve attendue

Compilation Release sans erreur, 0 avertissement, puis tous les tests Sprint 1 à 54 réussis.
