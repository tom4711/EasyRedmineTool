# Zeitbuchungsfilter: Nur gebuchte Tickets

## Ziel

Wenn der Zeitbuchungsfilter aktiviert ist, soll die Ticketliste ausschließlich Tickets enthalten, auf denen der aktuelle Benutzer im gewählten Zeitraum Zeit gebucht hat. Damit werden nicht mehr alle Treffer des normalen Zuweisungs- und Statusfilters mit angezeigt.

## Verhalten und Umfang

- Die Option wird als „Nur Tickets mit eigenen Zeitbuchungen anzeigen“ beschriftet.
- Der Zeitraum von 3, 6, 9 oder 12 Monaten begrenzt die gefundenen Zeitbuchungen.
- Bei aktivierter Option werden die Zuweisungs- und Statusauswahl deaktiviert und ausgegraut. Die lokale Textsuche bleibt nutzbar.
- Bei deaktivierter Option gelten wie bisher Zuweisungs- und Statusfilter; Zeitbuchungen werden nicht abgefragt.
- Die Statusmeldung nennt die Zahl der geladenen Tickets und den ausgewählten Zeitraum.

## Ansätze

1. Den Zeitbuchungsfilter zum ausschließlichen Modus machen und inkompatible Filter deaktivieren. **Ausgewählt:** entspricht dem gewünschten Verhalten und macht den aktiven Modus sichtbar.
2. Eine zusätzliche Auswahl zwischen „Zusätzlich“ und „Nur“ einführen. Das bewahrt beide Modi, lässt aber mehr Bedienoptionen und Komplexität bestehen.

## Grenzen und Umsetzungsspielraum

Es werden nur Tickets berücksichtigt, die aus den eigenen Zeitbuchungen des aktuellen Benutzers hervorgehen. Manuell hinzugefügte Tickets per ID bleiben wie bisher verfügbar. Die bestehende Zeitbuchungs-Cache- und Zeitraumlogik bleibt bestehen. Die Umsetzung wird im Core-Service, ViewModel und Ticketlisten-UI vorgenommen.
