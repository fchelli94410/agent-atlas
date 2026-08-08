# Atlas Drop V1 — Rapport final

## Fonctionnalités couvertes

- configuration OneDrive ;
- index SQLite ;
- scan initial et incrémental ;
- exclusions ;
- watcher ;
- identité stable des dossiers ;
- nettoyage des chemins absents ;
- analyse TXT/CSV/DOCX/XLSX/PPTX/PDF/images/MSG/ZIP ;
- OCR local ;
- normalisation ;
- extraction dates/entreprises/lieux/personnes/références/factures/montants ;
- classification document ;
- profils fichiers ;
- recherche accent/casse/multi-mots/fuzzy ;
- scoring dossiers ;
- fichiers similaires ;
- historique utilisateur ;
- confiance ;
- explicabilité ;
- interface WPF ;
- clavier ;
- recherche manuelle ;
- création dossier ;
- proposition de renommage ;
- renommage auto haute confiance ;
- noms Windows et chemins longs ;
- détection doublons ;
- SHA-256 ;
- prévalidation déplacement ;
- déplacement sécurisé ;
- vérification ;
- rollback ;
- historique ;
- undo ;
- apprentissage ;
- reset/désactivation apprentissage ;
- instance unique ;
- clic droit Explorer ;
- sons/rappel ;
- logs Serilog ;
- cache/retry/robustesse ;
- installateur/désinstallateur.

## Règles critiques garanties par tests

- aucun écrasement ;
- aucun effacement automatique d’un doublon ;
- destination hors OneDrive refusée ;
- profondeur automatique maximale 4 ;
- exclusions conservées ;
- source préservée en cas d’échec pré-déplacement ;
- rollback en cas d’échec de vérification ;
- undo sécurisé ;
- apprentissage désactivable.

## Validation finale attendue

Le PC Windows cible doit confirmer :
- restauration NuGet ;
- build Release ;
- 0 avertissement ;
- 0 erreur ;
- tous les tests réussis ;
- publication self-contained ;
- installateur généré ;
- auto-test installateur réussi ;
- `VALIDATION COMPLETE`.
