# Journal de décisions

## Architecture
- .NET 8 / WPF.
- SQLite local.
- Application locale-first.
- Tesseract pour OCR local.
- Serilog pour journaux.
- xUnit pour tests.

## Sécurité
- Aucun écrasement.
- Aucun effacement automatique d’un doublon.
- SHA-256 pour les doublons exacts et vérifications utiles.
- Rollback si vérification post-déplacement en échec.
- Undo refusé si le fichier a changé.

## Indexation et suggestions
- Arborescence complète indexable.
- Suggestions automatiques limitées à `MaxSuggestedDepth = 4`.
- Dossiers exclus jamais proposés automatiquement.

## Installation
- Installation par utilisateur.
- Pas de droits administrateur requis.
- Menu contextuel HKCU.
- Une seule instance de l’application.
