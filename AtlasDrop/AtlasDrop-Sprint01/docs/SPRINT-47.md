# Sprint 47 — Déplacement sécurisé

1. Prévalidation Sprint 46 obligatoire.
2. Aucun écrasement (`overwrite: false`).
3. Conflit de nom => `(2)`, `(3)`, etc.
4. Doublon exact SHA-256 => aucun déplacement.
5. Échec de prévalidation => source intacte.
6. Annulation avant déplacement => source intacte.
7. Destination vérifiée après déplacement.
8. Destination hors OneDrive refusée.
9. Accès refusé / I/O gérés sans crash.
10. Nouveau projet AtlasDrop.FileOperations.
11. Aucune suppression automatique de doublon.
12. Vérification renforcée au Sprint 48.
