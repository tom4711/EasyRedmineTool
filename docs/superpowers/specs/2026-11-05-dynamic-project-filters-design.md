# Dynamische Projektfilter im Teamboard

## Ziel

Der Teamleiter soll die Projektliste im Teamboard anhand der Projektangaben filtern können, die Redmine tatsächlich liefert. Der bisher fest eingebaute Schalter für `Geschlossen` wird durch dynamisch erkannte Filter ersetzt. Das Teamboard bleibt eine reine Anzeige; Änderungen in Redmine sind nicht Teil dieses Schritts.

## Ansätze

1. **Filter aus gelieferten Metadaten (gewählt):** Projektfelder werden aus den geladenen Projekten erkannt und als Feld-/Wertfilter angeboten. Das funktioniert mit den Standardangaben und allen Zusatzfeldern, die der Redmine-Zugang in der API-Antwort erhält.
2. **Nur Diagnoseansicht:** Metadaten sichtbar machen, aber weiter manuell konfigurieren. Das hilft beim Eingrenzen, bietet dem Teamleiter aber noch keinen nutzbaren Filter.
3. **Felddefinitionen global abrufen:** Redmines Custom-Fields-Endpunkt verwenden. Erfordert Administratorrechte, die der aktuelle API-Zugang voraussichtlich nicht besitzt.

## Gewähltes Verhalten

- Die Projektabfrage stellt mindestens ID, Name, Projektkennung, Redmine-Status, Sichtbarkeit/Öffentlichkeit, Elternprojekt und vorhandene Projekt-Zusatzfelder bereit.
- Das Teamboard sammelt gelieferte Felder und ihre Werte und bietet sie als Filterauswahl an. Felder ohne nutzbare Werte werden nicht als Filter angeboten.
- Filter wirken auf die angezeigte Projekt-Checkliste. Sie laden keine Tickets und verändern keine Projekt- oder Ticketdaten in Redmine.
- Bereits ausgewählte Projekt-IDs bleiben gespeichert, auch wenn sie durch einen Filter ausgeblendet werden. Beim Entfernen des Filters erscheinen sie wieder ausgewählt.
- Die dynamischen Filtereinstellungen werden lokal gespeichert und beim nächsten Öffnen wiederhergestellt.
- Wenn Redmine keine Zusatzfelder liefert, stehen weiterhin die gelieferten Standardangaben als Filter zur Verfügung; fehlende Zusatzfelder werden nicht vorgetäuscht.

## Grenzen

Die Filter können nur Felder und Werte anbieten, die über die verwendete Redmine-API und den aktuellen API-Zugang sichtbar sind. Tickets werden wie bisher erst für ausgewählte Projekte geladen. Eine Statusänderung oder sonstige Rückschreibung nach Redmine gehört nicht dazu.

## Umsetzungsspielraum

Die UI kann Feld- und Wertauswahl in einer kompakten Filterzeile darstellen. Die konkreten Standardfelder und die Behandlung mehrfacher Filter werden so gewählt, dass sie mit dem bestehenden Avalonia-Layout und den verfügbaren API-Angaben funktionieren.
