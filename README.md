# EasyRedmineTool

Desktop-Anwendung für Redmine/Easy Redmine: Tickets im Blick behalten, Zeiteinträge erfassen und Auswertungen ansehen.

## Funktionen

- Ticketliste mit Favoriten und letztem Buchungsdatum
- Schnelles Buchen über Favoriten-Tickets
- Quartalsauswertung nach Kalenderwochen
- Verbindungstest und lokale Einstellungen

## Einstellungen

Die Konfiguration wird lokal in einer JSON-Datei gespeichert:

| Plattform | Pfad |
|-----------|------|
| macOS | `~/Library/Application Support/EasyRedmineTool/settings.json` |
| Windows | `%AppData%\EasyRedmineTool\settings.json` |
| Linux | `~/.config/EasyRedmineTool/settings.json` (falls vom System so aufgelöst) |

Enthalten sind Basis-URL, API-Schlüssel, zwischengespeicherte Tickets und Favoriten.

## Sicherheit

- Der **API-Schlüssel wird unverschlüsselt** in `settings.json` gespeichert.
- Die Datei liegt im Benutzerprofil und ist nur für Ihr Konto zugänglich — teilen oder synchronisieren Sie sie nicht (z. B. per Cloud-Ordner).
- Verwenden Sie einen **persönlichen API-Schlüssel** mit den minimal nötigen Rechten in Redmine.
- Nach dem Löschen der App bleibt `settings.json` erhalten, bis Sie sie manuell entfernen.

## Entwicklung

```bash
dotnet build
dotnet test
dotnet run --project src/EasyRedmineTool.Desktop
```
