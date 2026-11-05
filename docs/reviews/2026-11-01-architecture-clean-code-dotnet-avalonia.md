# Deep Review: Architektur, Clean Code, .NET und Avalonia

Datum: 01.11.2026

## Präzisierung aus der Umsetzung

Beim Laufzeittest zu F11/#57 zeigte sich, dass die .NET-10-Factory bereits eine endliche PooledConnectionLifetime von zwei Minuten setzt. Die ursprüngliche statische Annahme über dauerhaft ungewechselte Verbindungen war für diese Runtime zu weit gefasst. Issue #57 wurde entsprechend korrigiert: umgesetzt wird eine explizite, getestete Fünf-Minuten-Regel für langlebige Desktop-Clients; ein bisheriger DNS-Ausfall ist nicht nachgewiesen. Die ursprünglichen Reviewtexte unten bleiben als Ausgangsbefunde nachvollziehbar.

## Basis und Ergebnis

Geprüfter Stand: [main 1df316bc3879c6348b83e49cfe02b138ab04a091](https://github.com/tom4711/EasyRedmineTool/commit/1df316bc3879c6348b83e49cfe02b138ab04a091), veröffentlicht als v0.12.0. Die Anwendungssourcen im lokalen Review-Worktree entsprechen diesem Stand; `git diff origin/main --stat` war vor dem Bericht leer.

Zwölf eigenständige Findings: **3 × P1**, **9 × P2**. P1 bedeutet vor weiteren größeren Funktionsänderungen beheben, da falsche Zielzuordnung oder Doppelbuchungen möglich sind. P2 bedeutet planbar beheben; jeweils Auslöser und Auswirkung beachten. Keine P0-Feststellung. Die Einstufung berücksichtigt den notwendigen Benutzerablauf und ist keine Aussage, dass der Fehler in jeder Sitzung auftritt.

## Prüfverfahren und Grenzen

- Drei getrennte Lesedurchgänge: Architektur/Composition Root, API/Persistenz sowie Avalonia/MVVM; Findings anschließend mit Aufrufpfaden, Tests und vorhandenen Issues abgeglichen.
- Geprüft: beide Produktprojekte, DI und Start/Shutdown, Services und Interfaces, API/Pagination/Serialisierung, Konfiguration und Caches, Core-/Desktop-ViewModels, AXAML/Code-behind und vorhandene Tests.
- Bestehende Tests ausgeführt: **110 bestanden, 0 fehlgeschlagen, 0 übersprungen**.
- Desktop-Build einschließlich XAML-Kompilierung: **erfolgreich, 0 Fehler**. Bekannte Analyzer-Hinweise (u.a. CA1859, CA1068, CA1861, CA1816) wurden nicht als eigene Architektur-Issues aufgebläht.
- Isolierte Fake-Harnesses unter /private/tmp verwendeten die Originalquellen und ausschließlich Fake-Dienste/HttpMessageHandler. Sie bestätigten F03, F04, F05, F06, F07 und F08. Die übrigen Findings sind statisch über ihre Aufrufketten belegt; Performance- und DNS-Auswirkungen wurden nicht gemessen.
- Keine produktiven API-Schreibaktionen, keine Firmenkonfiguration ausgelesen, kein Live-Test gegen Redmine. Keine vollständige UI-Abnahme oder Profilermessung. Kompilierung ersetzt keine Prüfung aller gerenderten UI-Zustände.
- Baseline-Kommandos:

```sh
dotnet test EasyRedmineTool.slnx --no-restore -p:DisableGitVersionTask=true -p:UseSharedCompilation=false --verbosity minimal
dotnet build src/EasyRedmineTool.Desktop/EasyRedmineTool.Desktop.csproj --no-restore -p:DisableGitVersionTask=true -p:UseSharedCompilation=false --verbosity minimal
```

Der erste Desktop-Build scheiterte an der Sandbox-Schreibsperre für das Avalonia-BuildServices-Log außerhalb des Worktrees. Derselbe Build war mit erlaubtem lokalen Logzugriff erfolgreich; daraus wurde kein Produktfehler abgeleitet. GitVersion wurde für den lokalen Compile-Check deaktiviert; Release-Versionierung war nicht Gegenstand dieses Reviews.

## Architekturbeurteilung

Die vorhandene Aufteilung ist für den Projektumfang grundsätzlich brauchbar: Desktop enthält Avalonia-Ansichten und Composition Root, Core die ViewModels, Anwendungsservices, API und Konfiguration. Nullable, zentrale Parser für Stunden/Datumswerte, kompilierte XAML-Bindings, DI und eine schnelle Fake-basierte Testsuite sind gute Grundlagen.

Die wichtigsten strukturellen Schwächen liegen in den Zustands- und Verantwortungsgrenzen:

1. **Verbindungskontext:** Zugangsdaten und fachliche Daten haben keinen gemeinsamen, überprüfbaren Kontext. Das erzeugt F01.
2. **Buchungsoperation:** Mehrere ViewModels koordinieren API-Mutation, lokalen Speicher und Darstellung selbst. Die Fehlergrenzen und Lebensdauer der Operation sind uneinheitlich (F02–F05). Ein kleiner gemeinsamer Anwendungsablauf und stabile Entwurfsidentitäten sind sinnvoller als weitere verteilte Guards.
3. **Persistenz:** Einstellungen sind gleichzeitig Cache-Repository und synchroner Dateizugriff aus UI-Aktionen (F06/F10). Ein Konfigurationssnapshot plus separates Cache-Repository würde diese Kopplung reduzieren.
4. **Transport und Caches:** HTTP-Lebensdauer, Pagination und Invalidierung brauchen explizite Verträge (F07/F08/F11/F12). Locks allein sichern nicht die zeitliche Gültigkeit asynchron geladener Daten.
5. **UI-Materialisierung:** Der Listenaufbau löst fachliche Ladeoperationen für alle Zeilen aus. Darstellung und Formulardaten sollten bedarfsorientiert zusammenspielen, während Entwürfe außerhalb recycelter Controls leben (F09).

Ein vollständiger Umbau in viele zusätzliche Assemblies oder ein pauschaler Wechsel des MVVM-Frameworks ist durch diese Befunde nicht begründet. Ebenso ist die Dateilänge eines ViewModels allein kein Finding. Browserstarts aus Core, große ViewModels und wiederholte Cancellation-Hilfsmethoden sind sinnvolle spätere Refactoring-Punkte; konkrete Regressionen sollten die jeweils betroffene Extraktion steuern.

## Reihenfolge und Abhängigkeiten

- Zuerst F01–F03: Verbindungsidentität, eindeutiger Commit-Status, Operationen über Navigation hinweg.
- Danach F04/F05 und F06/F07/F08/F12 als gezielte Korrekturen mit den beschriebenen Regressionen.
- F09/F10 gemeinsam entwerfen, aber getrennt verifizieren: Virtualisierung beseitigt weder synchrone Persistenz noch verlorene Entwürfe. Nach Behebung von F03 muss auch Recycling dieselben Zustandszusagen erhalten.
- F11 separat in der Composition Root umsetzen.
- Das bereits offene [Issue #45](https://github.com/tom4711/EasyRedmineTool/issues/45) zu konfigurierbaren Zusatzfelddefinitionen wurde bewusst nicht dupliziert. F02 behandelt die Fehlergrenze nach einer erfolgreichen Buchung und ist davon unabhängig.

## Übersicht

| ID | Priorität | Finding | GitHub |
| --- | --- | --- | --- |
| F01 | P1 | Ticket-Cache und laufende Vorgänge beim Server- oder Kontowechsel isolieren | [#47](https://github.com/tom4711/EasyRedmineTool/issues/47) |
| F02 | P1 | Erfolgreiche Buchungen trotz lokaler Speicherfehler als erfolgreich behandeln | [#48](https://github.com/tom4711/EasyRedmineTool/issues/48) |
| F03 | P1 | Laufende Buchungen und Kartenentwürfe beim Navigieren erhalten | [#49](https://github.com/tom4711/EasyRedmineTool/issues/49) |
| F04 | P2 | Serienvorschau nach erfolgreichem Buchen tatsächlich aktualisieren | [#50](https://github.com/tom4711/EasyRedmineTool/issues/50) |
| F05 | P2 | CanBook bei Änderungen der Busy-Zustände zuverlässig benachrichtigen | [#51](https://github.com/tom4711/EasyRedmineTool/issues/51) |
| F06 | P2 | Beschädigte Einstellungsdateien vor automatischem Überschreiben schützen | [#52](https://github.com/tom4711/EasyRedmineTool/issues/52) |
| F07 | P2 | Serverseitige Seitengröße und Pagination-Limits ohne stille Datenverluste behandeln | [#53](https://github.com/tom4711/EasyRedmineTool/issues/53) |
| F08 | P2 | Datumsparameter der Zeiteintrags-API mit invariantem Kalender formatieren | [#54](https://github.com/tom4711/EasyRedmineTool/issues/54) |
| F09 | P2 | Buchungskarten virtualisieren und Formulardaten bedarfsgesteuert laden | [#55](https://github.com/tom4711/EasyRedmineTool/issues/55) |
| F10 | P2 | Ticket-Cache von Einstellungen trennen und Datei-I/O aus UI-Aktionen entfernen | [#56](https://github.com/tom4711/EasyRedmineTool/issues/56) |
| F11 | P2 | Typed HttpClients nicht dauerhaft in Singleton-Services festhalten | [#57](https://github.com/tom4711/EasyRedmineTool/issues/57) |
| F12 | P2 | Laufende Cache-Ladevorgänge nach Invalidierung nicht wieder veröffentlichen | [#58](https://github.com/tom4711/EasyRedmineTool/issues/58) |

## Detaillierte Findings

### F01 · P1 · Ticket-Cache und laufende Vorgänge beim Server- oder Kontowechsel isolieren

[Issue #47](https://github.com/tom4711/EasyRedmineTool/issues/47)

#### Befund

**Priorität:** P1 · **Bereich:** Architektur · Verbindungs-/Datenkontext

SaveSettings ersetzt URL und API-Key, behält aber CachedTickets, Favoriten, letzte Ticket-IDs und Zusatzfeldvorgaben. ReloadSettings/ReloadFavorites übernehmen diese Daten. Eine alte Ticket-ID wird beim nächsten Buchen mit den neuen Zugangsdaten gesendet. Auf einer zweiten Instanz kann dieselbe ID ein anderes Ticket bezeichnen; außerdem bleiben Daten eines früheren Kontos sichtbar. Laufende Antworten können nach einem Verbindungswechsel ebenfalls alten Zustand publizieren.

#### Codebelege

- [src/EasyRedmineTool.Core/ViewModels/SettingsViewModel.cs:114](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/SettingsViewModel.cs#L114)
- [src/EasyRedmineTool.Core/ViewModels/TicketListViewModel.cs:187](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/TicketListViewModel.cs#L187)
- [src/EasyRedmineTool.Core/ViewModels/FavoriteTimeEntryRowViewModel.cs:243](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/FavoriteTimeEntryRowViewModel.cs#L243)

#### Auslöser / Reproduktion

Instanz A mit Favorit #42 laden. URL/Key auf Instanz B ändern und speichern. Ohne Ticketneuladen den übernommenen Favoriten buchen: Ticket-ID 42 wird an B gesendet.

#### Empfohlene Änderung

Einen expliziten Verbindungskontext mit normalisierter Serveridentität und Kontozuordnung einführen. Persistente und aktive Daten diesem Kontext zuordnen oder beim Wechsel kontrolliert verwerfen; laufende Vorgänge abbrechen und alte Antworten über eine Kontextgeneration abweisen. Einen reinen Key-Rotationsfall bewusst behandeln.

#### Akzeptanzkriterien

- [ ] Nach Wechsel A→B sind Tickets, Favoriten und Zusatzfeldvorgaben von A weder sichtbar noch buchbar.
- [ ] Gleiche Ticket-IDs zweier Instanzen werden getrennt gespeichert; Key-/Kontowechsel hat eine dokumentierte Migrationsregel.
- [ ] Eine verspätete Antwort aus A verändert keine Ansicht und keinen Cache von B.
- [ ] Regression mit zwei Fake-Instanzen und identischer Ticket-ID prüft auch das Ziel jedes Buchungsrequests.

#### Nachweis und Abgrenzung

Statisch über Speicher-, Reload- und Buchungspfad nachgewiesen; keine produktiven API-Schreibaktionen.

Review-ID F01; geprüfter main-Commit: `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0). Baseline: 110 Tests bestanden und Desktop-Build erfolgreich. Diese grünen Baseline-Prüfungen ersetzen die oben beschriebenen Regressionen nicht. Keine produktiven API-Schreibaktionen.

---

### F02 · P1 · Erfolgreiche Buchungen trotz lokaler Speicherfehler als erfolgreich behandeln

[Issue #48](https://github.com/tom4711/EasyRedmineTool/issues/48)

#### Befund

**Priorität:** P1 · **Bereich:** Architektur · Fehlergrenzen nach API-Mutationen

Nach erfolgreichem API-Request werden SaveDefaults und Cache-/Übersichtsaktualisierung im selben Ablauf ausgeführt. Wirft die lokale Speicherung, überschreibt die Favoritenkarte ihre Erfolgsmeldung mit einem Fehler; Eingaben bleiben buchbar. Bei Bearbeitung und Serienbuchung kann die Exception aus dem AsyncRelayCommand entweichen. Ein Benutzer kann eine bereits erstellte Buchung erneut senden.

#### Codebelege

- [src/EasyRedmineTool.Core/ViewModels/FavoriteTimeEntryRowViewModel.cs:270](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/FavoriteTimeEntryRowViewModel.cs#L270)
- [src/EasyRedmineTool.Core/ViewModels/TodayTimeEntryRowViewModel.cs:191](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/TodayTimeEntryRowViewModel.cs#L191)
- [src/EasyRedmineTool.Core/ViewModels/SeriesBookingViewModel.cs:554](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/SeriesBookingViewModel.cs#L554)

#### Auslöser / Reproduktion

CreateTimeEntryAsync liefert Success=true, anschließend wirft IAppSettingsService.Update eine IOException (z.B. schreibgeschützte Datei). Favoritenkarte zeigt einen Fehler statt eines eindeutig abgeschlossenen POSTs.

#### Empfohlene Änderung

Den serverseitigen Commit als endgültigen Buchungsstatus erfassen. Optionale lokale Nacharbeiten getrennt absichern und als zusätzliche Warnung melden. Einen gemeinsamen Anwendungsablauf für Mutation und Nacharbeiten verwenden, damit Einzel-, Änderungs- und Serienbuchung dieselbe Fehlersemantik haben.

#### Akzeptanzkriterien

- [ ] Bei erfolgreichem API-Request bleibt der Buchungserfolg auch dann sichtbar, wenn Defaults oder Cache nicht geschrieben werden können.
- [ ] Lokale Fehler erscheinen als separate Warnung; sie lösen keinen automatischen erneuten POST aus.
- [ ] Einzelbuchung, Bearbeitung und Serie werfen für lokale Nacharbeiten keine unbehandelten Exceptions aus UI-Commands.
- [ ] Fake-Tests mit Success=true plus IOException prüfen Requestanzahl, Erfolgsmeldung und Wiederholungsverhalten.

#### Nachweis und Abgrenzung

Statisch über drei Mutationspfade nachgewiesen. SaveDefaults läuft nach API-Erfolg; die äußere Fehlerbehandlung unterscheidet die beiden Phasen nicht.

Review-ID F02; geprüfter main-Commit: `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0). Baseline: 110 Tests bestanden und Desktop-Build erfolgreich. Diese grünen Baseline-Prüfungen ersetzen die oben beschriebenen Regressionen nicht. Keine produktiven API-Schreibaktionen.

---

### F03 · P1 · Laufende Buchungen und Kartenentwürfe beim Navigieren erhalten

[Issue #49](https://github.com/tom4711/EasyRedmineTool/issues/49)

#### Befund

**Priorität:** P1 · **Bereich:** MVVM · Lebenszyklus / Entwürfe

OpenTimeEntries ruft bei jedem Öffnen ReloadFavorites auf. Dieser Ablauf leert FavoriteRows und erzeugt neue Row-ViewModels. Nur Stunden, Kommentar, Datum und Aktivitäts-ID werden übernommen; IsSubmitting und die laufende Buchungsoperation bleiben an der alten Zeile. Die neue Zeile erlaubt denselben POST erneut. Zusatzfeldentwürfe gehen beim Neuaufbau verloren.

#### Codebelege

- [src/EasyRedmineTool.Desktop/ViewModels/MainWindowViewModel.cs:174](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Desktop/ViewModels/MainWindowViewModel.cs#L174)
- [src/EasyRedmineTool.Core/ViewModels/TimeEntriesViewModel.cs:123](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/TimeEntriesViewModel.cs#L123)
- [src/EasyRedmineTool.Core/ViewModels/TimeEntriesViewModel.cs:139](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/TimeEntriesViewModel.cs#L139)

#### Auslöser / Reproduktion

Einen verzögerten POST auf einer Favoritenkarte starten, zu Tickets wechseln und Zeiteinträge erneut öffnen. Die neue Karte hat IsSubmitting=false, während die alte Karte noch sendet. Erneutes Buchen startet einen zweiten POST.

#### Empfohlene Änderung

Zeilen über eine stabile Ticket-/Verbindungsschlüsselidentität abgleichen und laufende Operationen/Entwürfe erhalten. Alternativ Operationen in einem übergeordneten Buchungskoordinator verwalten und den Zustand in alle Darstellungen projizieren. Zusatzfeldentwürfe beim Abgleich berücksichtigen.

#### Akzeptanzkriterien

- [ ] Navigation während einer verzögerten Buchung erlaubt keinen zweiten POST für denselben laufenden Entwurf.
- [ ] Laufender Status und Ergebnis bleiben nach Rückkehr auf derselben Ticketkarte sichtbar.
- [ ] Stunden, Datum, Kommentar, Aktivität und bearbeitete Zusatzfelder bleiben beim Navigieren erhalten.
- [ ] Deterministischer Test mit TaskCompletionSource: Navigation/Reload während POST; insgesamt genau ein Request.

#### Nachweis und Abgrenzung

Mit isoliertem Fake-Service-Harness reproduziert: alte Zeile IsSubmitting=true, Ersatzzeile=false; erneutes Auslösen erzeugt einen weiteren POST.

Review-ID F03; geprüfter main-Commit: `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0). Baseline: 110 Tests bestanden und Desktop-Build erfolgreich. Diese grünen Baseline-Prüfungen ersetzen die oben beschriebenen Regressionen nicht. Keine produktiven API-Schreibaktionen.

---

### F04 · P2 · Serienvorschau nach erfolgreichem Buchen tatsächlich aktualisieren

[Issue #50](https://github.com/tom4711/EasyRedmineTool/issues/50)

#### Befund

**Priorität:** P2 · **Bereich:** MVVM · Serienbuchungsablauf

ConfirmBookSeriesAsync ruft nach erfolgreicher Serie RefreshPreviewAsync auf, während IsBooking noch true ist. Der Guard am Anfang von RefreshPreviewAsync kehrt deshalb sofort zurück. Die gerade gebuchten Tage bleiben ohne Konflikt als „Neu“ in der Vorschau. Nach erneuter Auswahl können sie ohne Konfliktwarnung nochmals gebucht werden.

#### Codebelege

- [src/EasyRedmineTool.Core/ViewModels/SeriesBookingViewModel.cs:275](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/SeriesBookingViewModel.cs#L275)
- [src/EasyRedmineTool.Core/ViewModels/SeriesBookingViewModel.cs:554](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/SeriesBookingViewModel.cs#L554)

#### Auslöser / Reproduktion

Vorschau mit neuem Tag laden, Serie bestätigen und abschließen lassen. Den gleichen Vorschautag erneut auswählen und erneut bestätigen: keine aktualisierte Konflikterkennung.

#### Empfohlene Änderung

Den Buchungszustand und den nachfolgenden Refresh explizit sequenzieren. Erfolgreiche Tage unmittelbar als gebucht markieren; anschließend aktuelle Serverdaten laden. Teilerfolge müssen ebenfalls korrekt im lokalen Zustand erscheinen.

#### Akzeptanzkriterien

- [ ] Nach erfolgreicher Serie werden bestehende Einträge erneut gelesen oder gleichwertig zuverlässig in die Vorschau übernommen.
- [ ] Ein gebuchter Tag wird als Konflikt/gebucht dargestellt und ist nicht automatisch erneut ausgewählt.
- [ ] Teilerfolge behalten ihren Erfolg; fehlgeschlagene Tage lassen sich gezielt wiederholen.
- [ ] Fake-Test prüft einen zweiten Leseaufruf sowie Konfliktstatus und Warntext nach der Buchung.

#### Nachweis und Abgrenzung

Isolierter Fake-Harness: nach Buchung weiterhin nur ein GET, HasConflict=false und Status „Neu“; Neuauswahl erzeugt Bestätigung ohne Konfliktwarnung.

Review-ID F04; geprüfter main-Commit: `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0). Baseline: 110 Tests bestanden und Desktop-Build erfolgreich. Diese grünen Baseline-Prüfungen ersetzen die oben beschriebenen Regressionen nicht. Keine produktiven API-Schreibaktionen.

---

### F05 · P2 · CanBook bei Änderungen der Busy-Zustände zuverlässig benachrichtigen

[Issue #51](https://github.com/tom4711/EasyRedmineTool/issues/51)

#### Befund

**Priorität:** P2 · **Bereich:** Avalonia · Binding / abgeleiteter Zustand

CanBook hängt von IsLoadingPreview und IsBooking ab. Deren generierte Setter benachrichtigen CanBook nicht. NotifyPreviewChanged wird am Ende des Preview-/Buchungsablaufs noch während des Busy-Zustands ausgelöst; erst danach setzt finally den Zustand zurück. Die Avalonia-Bindung behält dadurch false, obwohl der Getter bereits true ergibt. Der Buchungsbutton bleibt bis zu einer weiteren Auswahländerung deaktiviert.

#### Codebelege

- [src/EasyRedmineTool.Core/ViewModels/SeriesBookingViewModel.cs:79](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/SeriesBookingViewModel.cs#L79)
- [src/EasyRedmineTool.Core/ViewModels/SeriesBookingViewModel.cs:139](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/SeriesBookingViewModel.cs#L139)
- [src/EasyRedmineTool.Core/ViewModels/SeriesBookingViewModel.cs:378](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/SeriesBookingViewModel.cs#L378)
- [src/EasyRedmineTool.Desktop/Views/SeriesBookingView.axaml:284](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Desktop/Views/SeriesBookingView.axaml#L284)

#### Auslöser / Reproduktion

Eine gültige Vorschau mit ausgewähltem Tag laden und auf das Ende warten. Ohne weitere Änderung der Tagesauswahl den Buchungsbutton prüfen.

#### Empfohlene Änderung

Die Abhängigkeiten mit NotifyPropertyChangedFor(nameof(CanBook)) oder zentralen Zustandsübergängen vollständig abbilden. Die Benachrichtigung muss nach jeder relevanten Zustandsänderung den tatsächlichen Wert liefern.

#### Akzeptanzkriterien

- [ ] Nach Abschluss einer gültigen Vorschau wird CanBook=true benachrichtigt und der Button ist ohne weitere Interaktion nutzbar.
- [ ] Start und Ende von Vorschau/Buchung sowie Fehler- und Abbruchpfade aktualisieren CanBook.
- [ ] Ein PropertyChanged-Test prüft die beobachteten Werte, nicht nur den Getter am Methodenende.
- [ ] Ein Avalonia-Binding-Test oder gezielter UI-Durchlauf bestätigt den Buttonzustand.

#### Nachweis und Abgrenzung

Isolierter Harness: nach Vorschau tatsächliches CanBook=True, letzter für die Bindung gemeldeter Wert=False.

Review-ID F05; geprüfter main-Commit: `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0). Baseline: 110 Tests bestanden und Desktop-Build erfolgreich. Diese grünen Baseline-Prüfungen ersetzen die oben beschriebenen Regressionen nicht. Keine produktiven API-Schreibaktionen.

---

### F06 · P2 · Beschädigte Einstellungsdateien vor automatischem Überschreiben schützen

[Issue #52](https://github.com/tom4711/EasyRedmineTool/issues/52)

#### Befund

**Priorität:** P2 · **Bereich:** Persistenz · Wiederherstellung / Datenverlust

ReadFromFile fängt jeden Lesefehler ab und liefert null; Load ersetzt dies durch Defaults. Update kann nicht zwischen fehlender Datei und fehlgeschlagenem Lesen unterscheiden und überschreibt anschließend die bestehende Datei. Schon ein Theme- oder Filterwechsel kann so noch rettbare Zugangsdaten, Favoriten und Cache vollständig entfernen.

#### Codebelege

- [src/EasyRedmineTool.Core/Services/AppSettingsService.cs:42](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Services/AppSettingsService.cs#L42)
- [src/EasyRedmineTool.Core/Services/AppSettingsService.cs:69](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Services/AppSettingsService.cs#L69)
- [src/EasyRedmineTool.Core/Services/AppSettingsService.cs:88](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Services/AppSettingsService.cs#L88)

#### Auslöser / Reproduktion

Eine vorhandene, syntaktisch beschädigte JSON-Datei bereitstellen und Update(s => s.IsDarkMode = true) ausführen. Die Originaldatei wird durch eine Default-Konfiguration ersetzt.

#### Empfohlene Änderung

Fehlende Datei und beschädigte/unlesbare Datei als verschiedene Ergebnisse modellieren. Bei Lesefehlern Original erhalten, Wiederherstellung/Backup anbieten und automatische Updates bis zur bewussten Wiederherstellung verhindern. IO- und Formatfehler gezielt behandeln.

#### Akzeptanzkriterien

- [ ] Eine beschädigte vorhandene Datei bleibt beim folgenden Theme-/Filter-/Save-Update bytegenau erhalten oder wird vor einem expliziten Reset gesichert.
- [ ] Die UI informiert verständlich über Lade-/Wiederherstellungsfehler.
- [ ] Fehlende Dateien erhalten weiterhin normale Defaults; ein erfolgreich gelesenes Dokument bleibt atomar speicherbar.
- [ ] Tests decken kaputtes JSON, ungültiges Schema und einen simulierten Lesefehler ab.

#### Nachweis und Abgrenzung

Isolierter Harness mit Original-Sourcen reproduziert: beschädigtes JSON + Update führt zu leerem API-Key und 0 Favoriten.

Review-ID F06; geprüfter main-Commit: `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0). Baseline: 110 Tests bestanden und Desktop-Build erfolgreich. Diese grünen Baseline-Prüfungen ersetzen die oben beschriebenen Regressionen nicht. Keine produktiven API-Schreibaktionen.

---

### F07 · P2 · Serverseitige Seitengröße und Pagination-Limits ohne stille Datenverluste behandeln

[Issue #53](https://github.com/tom4711/EasyRedmineTool/issues/53)

#### Befund

**Priorität:** P2 · **Bereich:** API · Pagination / Ergebniskorrektheit

Die Pagination beendet bei pageItemCount < dem angefragten Limit 100, selbst wenn total_count weitere Datensätze meldet; der Response-Limitwert wird nicht ausgewertet. Ein Serverlimit von 25 liefert dadurch nur die erste Seite. Außerdem endet jeder Abruf nach 100 Seiten und wird auch bei weiteren Daten als vollständiger Erfolg zurückgegeben. Quartalssummen, Konfliktprüfung und Ticketfilter können unvollständig sein.

#### Codebelege

- [src/EasyRedmineTool.Core/Api/EasyRedmineApiClient.cs:63](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Api/EasyRedmineApiClient.cs#L63)
- [src/EasyRedmineTool.Core/Api/EasyRedmineApiClient.cs:170](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Api/EasyRedmineApiClient.cs#L170)
- [src/EasyRedmineTool.Core/Api/EasyRedmineApiClient.cs:584](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Api/EasyRedmineApiClient.cs#L584)

#### Auslöser / Reproduktion

Fake-API antwortet auf limit=100 mit 25 Einträgen, limit=25 und total_count=80. Der Client gibt 25 Einträge nach einem Request zurück. Separat mehr als 10.000 Einträge bereitstellen.

#### Empfohlene Änderung

Gemeinsamen paginierten Abruf mit Response-Metadaten und Fortschrittsprüfung implementieren. Offset anhand tatsächlich verarbeiteter/serverseitiger Seitengröße fortsetzen. Sicherheitslimits behalten, aber einen expliziten Fehler oder sichtbar unvollständiges Ergebnis liefern.

#### Akzeptanzkriterien

- [ ] limit=25,total_count=80 wird vollständig über mehrere Requests geladen; IDs fehlen nicht und werden nicht doppelt gezählt.
- [ ] Normale 100er-Seiten, fehlender total_count und leere Abschlussseite bleiben unterstützt.
- [ ] Ein erreichtes Sicherheitslimit erzeugt keinen stillen vollständigen Erfolg.
- [ ] Ticketliste, Auswertung und Serienkonfliktprüfung zeigen Pagination-Fehler verständlich an.

#### Nachweis und Abgrenzung

Isolierter Fake-HttpMessageHandler-Harness mit Original-Client: 25/80 Einträge, returned=25 und requests=1. 100-Seiten-Grenze zusätzlich statisch und anhand bestehender Tests bestätigt.

Review-ID F07; geprüfter main-Commit: `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0). Baseline: 110 Tests bestanden und Desktop-Build erfolgreich. Diese grünen Baseline-Prüfungen ersetzen die oben beschriebenen Regressionen nicht. Keine produktiven API-Schreibaktionen.

---

### F08 · P2 · Datumsparameter der Zeiteintrags-API mit invariantem Kalender formatieren

[Issue #54](https://github.com/tom4711/EasyRedmineTool/issues/54)

#### Befund

**Priorität:** P2 · **Bereich:** .NET · Kulturunabhängiger API-Vertrag

GetMyTimeEntriesPageAsync interpoliert from/to mit yyyy-MM-dd ohne InvariantCulture. Das verwendet den Kalender der CurrentCulture. Unter th-TH wird z.B. das Jahr 2026 als 2569 übertragen. Die API fragt damit einen anderen Zeitraum ab; Ergebnisse erscheinen leer oder falsch. Im Projekt existiert bereits RedmineDates.FormatSpentOn für invariant formatierte API-Daten.

#### Codebelege

- [src/EasyRedmineTool.Core/Api/EasyRedmineApiClient.cs:283](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Api/EasyRedmineApiClient.cs#L283)
- [src/EasyRedmineTool.Core/RedmineDates.cs:15](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/RedmineDates.cs#L15)

#### Auslöser / Reproduktion

CurrentCulture auf th-TH setzen und Zeiteinträge für 2026-11-01 abrufen. Den URI eines Fake-Handlers prüfen: from=2569-11-01.

#### Empfohlene Änderung

Die bestehenden invarianten Datumsfunktionen für alle Redmine-Datumsparameter nutzen und die API-Grenze auf weitere kulturabhängige Formatierung prüfen.

#### Akzeptanzkriterien

- [ ] Dieselben DateTime-Werte erzeugen unter de-DE, en-US und th-TH identische Gregorianische API-Datumsparameter.
- [ ] Ein Fake-HTTP-Test prüft from=2026-11-01 und to=2026-11-01 unabhängig von der Kultur.
- [ ] Die Anzeige in der UI darf weiterhin lokalisiert sein; der Wire-Vertrag bleibt yyyy-MM-dd mit invariantem Kalender.

#### Nachweis und Abgrenzung

Isolierter Harness mit Original-Client und Fake-Handler reproduziert: unter th-TH from=2569-11-01&to=2569-11-01.

Review-ID F08; geprüfter main-Commit: `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0). Baseline: 110 Tests bestanden und Desktop-Build erfolgreich. Diese grünen Baseline-Prüfungen ersetzen die oben beschriebenen Regressionen nicht. Keine produktiven API-Schreibaktionen.

---

### F09 · P2 · Buchungskarten virtualisieren und Formulardaten bedarfsgesteuert laden

[Issue #55](https://github.com/tom4711/EasyRedmineTool/issues/55)

#### Befund

**Priorität:** P2 · **Bereich:** Avalonia · Rendering / Anforderungslast

Die Buchungsliste nutzt ItemsControl in einem äußeren ScrollViewer ohne Virtualisierung. ReloadFavorites erzeugt alle Karten-ViewModels und startet für jede Zeile sofort RestoreAndLoadRowAsync, noch bevor die lokale Suche filtert. Pro Ticket werden Aktivitäten und historische Zusatzfeldwerte geladen, letztere ggf. über mehrere Fallback-Requests. Bei „Alle“ mit hunderten oder tausenden Cache-Tickets entstehen sehr viele Controls und parallele HTTP-Requests bei jedem Reload/Tabwechsel.

#### Codebelege

- [src/EasyRedmineTool.Desktop/Views/TimeEntriesView.axaml:336](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Desktop/Views/TimeEntriesView.axaml#L336)
- [src/EasyRedmineTool.Desktop/Views/TimeEntriesView.axaml:376](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Desktop/Views/TimeEntriesView.axaml#L376)
- [src/EasyRedmineTool.Core/ViewModels/TimeEntriesViewModel.cs:139](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/TimeEntriesViewModel.cs#L139)
- [src/EasyRedmineTool.Core/ViewModels/FavoriteTimeEntryRowViewModel.cs:118](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/FavoriteTimeEntryRowViewModel.cs#L118)

#### Auslöser / Reproduktion

500 lokal gespeicherte Tickets bereitstellen, „Alle“ öffnen und anschließend nach einem einzelnen Ticket suchen. Requests werden bereits für alle Tickets gestartet und alle angezeigten Karten materialisiert.

#### Empfohlene Änderung

Eine an den Viewport gebundene virtualisierte Darstellung verwenden. Formulardaten beim tatsächlichen Bedarf laden, wiederverwenden und Parallelität begrenzen. Entwürfe außerhalb recycelter Controls halten; Formulardaten nicht pauschal für unsichtbare Tickets anfordern.

#### Akzeptanzkriterien

- [ ] Bei 500 Tickets skaliert die Zahl erzeugter UI-Container mit dem Viewport und nicht mit der gesamten Liste.
- [ ] Fake-Zähler belegen, dass unsichtbare/unbenutzte Karten keine unnötigen Formulardatenrequests auslösen.
- [ ] Maximale Parallelität ist begrenzt und messbar; Suche/Navigation beendet überholte Arbeit.
- [ ] Scrollen/Recycling verliert keine Entwürfe oder laufenden Buchungszustände; Ausgangs- und Zielmessung werden dokumentiert.

#### Nachweis und Abgrenzung

Statisch über AXAML, Reload-Schleife und API-Fallback-Kette bestätigt. Keine Laufzeit- oder Speichermessung behauptet.

Review-ID F09; geprüfter main-Commit: `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0). Baseline: 110 Tests bestanden und Desktop-Build erfolgreich. Diese grünen Baseline-Prüfungen ersetzen die oben beschriebenen Regressionen nicht. Keine produktiven API-Schreibaktionen.

#### Referenz

- https://docs.avaloniaui.net/docs/app-development/performance

---

### F10 · P2 · Ticket-Cache von Einstellungen trennen und Datei-I/O aus UI-Aktionen entfernen

[Issue #56](https://github.com/tom4711/EasyRedmineTool/issues/56)

#### Befund

**Priorität:** P2 · **Bereich:** Architektur · UI / Persistenzgrenze

AppSettings speichert Zugangsdaten, kleine UI-Einstellungen und die gesamte Ticketliste in einem Dokument. Jeder Filter-/Favoriten-/Themewechsel liest, deserialisiert und schreibt synchron die ganze Datei auf dem UI-Thread. Beim Kartenaufbau liest zudem jede Zeile erneut dieselbe Datei. Die Kosten steigen mit dem gesamten Ticketbestand; Schreibfehler können aus synchronen PropertyChanged-/RelayCommand-Pfaden entweichen.

#### Codebelege

- [src/EasyRedmineTool.Core/Configuration/AppSettings.cs:11](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Configuration/AppSettings.cs#L11)
- [src/EasyRedmineTool.Core/Services/AppSettingsService.cs:52](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Services/AppSettingsService.cs#L52)
- [src/EasyRedmineTool.Core/ViewModels/TicketListViewModel.cs:591](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/TicketListViewModel.cs#L591)
- [src/EasyRedmineTool.Core/ViewModels/FavoriteTimeEntryRowViewModel.cs:118](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/ViewModels/FavoriteTimeEntryRowViewModel.cs#L118)

#### Auslöser / Reproduktion

Mehrere tausend Tickets cachen und Filter/Theme/Favoriten wechseln bzw. „Alle“ öffnen. Jede kleine Änderung verarbeitet die vollständige Cache-Datei; alle Karten lesen sie erneut.

#### Empfohlene Änderung

Einen geladenen Konfigurationssnapshot und ein separates Ticket-Repository/Cache einführen. Persistenz explizit asynchron, serialisiert und mit klarer Fehlerbehandlung ausführen; kleine UI-Änderungen ggf. zusammenfassen. Die API darf UI-Zustand nur auf dem UI-Thread aktualisieren. Atomare Speicherung erhalten.

#### Akzeptanzkriterien

- [ ] Theme-/Filter-/Favoritenwechsel serialisieren nicht mehr den gesamten Ticket-Cache.
- [ ] Kartenaufbau verwendet einen gemeinsamen Snapshot statt einer Dateilesung pro Karte.
- [ ] Dateioperationen blockieren den UI-Thread nicht; simulierte IO-Fehler liefern verständliche Rückmeldung ohne App-Absturz.
- [ ] Ein Test/Benchmark mit 3.000 Tickets zählt Lese-/Schreiboperationen und dokumentiert die UI-Reaktionszeit; konkurrierende Updates gehen nicht verloren.

#### Nachweis und Abgrenzung

Synchronous File.ReadAllText/WriteAllText und direkte UI-Aufrufketten statisch bestätigt; keine gemessene Freeze-Dauer behauptet.

Review-ID F10; geprüfter main-Commit: `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0). Baseline: 110 Tests bestanden und Desktop-Build erfolgreich. Diese grünen Baseline-Prüfungen ersetzen die oben beschriebenen Regressionen nicht. Keine produktiven API-Schreibaktionen.

#### Referenz

- https://learn.microsoft.com/en-us/dotnet/standard/io/asynchronous-file-i-o

---

### F11 · P2 · Typed HttpClients nicht dauerhaft in Singleton-Services festhalten

[Issue #57](https://github.com/tom4711/EasyRedmineTool/issues/57)

#### Befund

**Priorität:** P2 · **Bereich:** .NET · Dependency Injection / HTTP-Lebenszyklus

IEasyRedmineApiClient wird über AddHttpClient als Typed Client registriert, dann aber in IConnectionTestService, ITicketService und ITimeEntryService mit Singleton-Lebensdauer injiziert. Diese halten den ersten Client für die gesamte Desktop-Sitzung. Handler-Rotation durch IHttpClientFactory erreicht diese Instanzen nicht; eine PooledConnectionLifetime ist nicht konfiguriert. Nach DNS-/VPN-/Backendwechseln können langlebige Verbindungen alte Ziele behalten.

#### Codebelege

- [src/EasyRedmineTool.Desktop/DependencyInjection/ServiceCollectionExtensions.cs:24](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Desktop/DependencyInjection/ServiceCollectionExtensions.cs#L24)
- [src/EasyRedmineTool.Core/Services/TicketService.cs:9](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Services/TicketService.cs#L9)
- [src/EasyRedmineTool.Core/Services/TimeEntryService.cs:9](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Services/TimeEntryService.cs#L9)

#### Auslöser / Reproduktion

Die App länger geöffnet lassen und die DNS-Zuordnung des Redmine-Hosts wechseln. Der vom Singleton festgehaltene Client wird nicht allein durch Ablauf der Factory-HandlerLifetime ersetzt.

#### Empfohlene Änderung

Entweder pro Operation einen benannten Client über IHttpClientFactory beziehen oder langlebige Clients bewusst mit SocketsHttpHandler.PooledConnectionLifetime konfigurieren. Die gewählte Lebensdauerstrategie in der Composition Root dokumentieren; keine neuen HttpClients je Request ohne Factory erzeugen.

#### Akzeptanzkriterien

- [ ] Die Composition Root verwendet eine dokumentierte, zu Desktop-Sitzungen passende HTTP-Lebensdauerstrategie.
- [ ] Singletons halten keinen kurzlebig gedachten Typed Client dauerhaft ohne Connection-Lifetime-Regel.
- [ ] Konfigurationstest prüft Client-/Handler-Erzeugung bzw. PooledConnectionLifetime; Timeout und Cancellation bleiben erhalten.
- [ ] Ein manueller DNS-/Reconnect-Prüfweg für lange Sitzungen ist dokumentiert.

#### Nachweis und Abgrenzung

DI-Registrierung und Konstruktoren statisch bestätigt; kein DNS-Ausfall reproduziert. Das Risiko wird ausdrücklich als Lifecycle-Risiko behandelt.

Review-ID F11; geprüfter main-Commit: `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0). Baseline: 110 Tests bestanden und Desktop-Build erfolgreich. Diese grünen Baseline-Prüfungen ersetzen die oben beschriebenen Regressionen nicht. Keine produktiven API-Schreibaktionen.

#### Referenz

- https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory#avoid-typed-clients-in-singleton-services

---

### F12 · P2 · Laufende Cache-Ladevorgänge nach Invalidierung nicht wieder veröffentlichen

[Issue #58](https://github.com/tom4711/EasyRedmineTool/issues/58)

#### Befund

**Priorität:** P2 · **Bereich:** Async · Cache-Konsistenz

Beide Caches löschen bei Invalidate nur bereits gespeicherte Daten. Ein zuvor gestarteter GET kann anschließend zurückkehren und seinen alten Snapshot erneut in den Cache schreiben. TimeEntryFormDataCache behält zusätzlich alte Inflight-Tasks. Damit kann eine erfolgreiche Mutation direkt danach wieder veraltete Daten für fünf Minuten (TicketService) bzw. bis zur nächsten Invalidierung (Aktivitäten) als aktuell anbieten. Ein Lock um einzelne Dictionary-/Feldzugriffe löst dieses zeitliche Problem nicht.

#### Codebelege

- [src/EasyRedmineTool.Core/Services/TicketService.cs:135](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Services/TicketService.cs#L135)
- [src/EasyRedmineTool.Core/Services/TimeEntryFormDataCache.cs:23](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Services/TimeEntryFormDataCache.cs#L23)
- [src/EasyRedmineTool.Core/Services/TimeEntryFormDataCache.cs:39](https://github.com/tom4711/EasyRedmineTool/blob/1df316bc3879c6348b83e49cfe02b138ab04a091/src/EasyRedmineTool.Core/Services/TimeEntryFormDataCache.cs#L39)

#### Auslöser / Reproduktion

GET starten und mit TaskCompletionSource verzögern. Eine Mutation bzw. Invalidate ausführen. Den alten GET freigeben. Anschließend erneut dieselben Daten abrufen: alter Cache-Inhalt kann wiederverwendet werden.

#### Empfohlene Änderung

Eine Cachegeneration vor dem Load erfassen und Publikation nur bei unveränderter Generation zulassen. Nach Invalidierung keine Inflight-Tasks einer früheren Generation mit neuen Lesern teilen; gemeinsames Laden und Abbruchsemantik explizit definieren.

#### Akzeptanzkriterien

- [ ] Ein vor Invalidierung gestarteter Load darf nachträglich keinen gültigen Cacheeintrag der neuen Generation erzeugen.
- [ ] Ein neuer Leser nach Invalidierung verwendet keinen veralteten Inflight-Task.
- [ ] TCS-gesteuerte Tests für beide Caches prüfen, dass der nächste Abruf neue Daten lädt.
- [ ] Die normale gemeinsame Nutzung identischer gleichzeitiger Loads bleibt bestehen; einzelne Abbrüche beschädigen andere Leser nicht.

#### Nachweis und Abgrenzung

Zeitliche Abfolge anhand der await-/Invalidate-/Publikationspfade statisch bestätigt; isolierte Reproduktion wird im Reviewbericht separat ausgewiesen.

Review-ID F12; geprüfter main-Commit: `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0). Baseline: 110 Tests bestanden und Desktop-Build erfolgreich. Diese grünen Baseline-Prüfungen ersetzen die oben beschriebenen Regressionen nicht. Keine produktiven API-Schreibaktionen.

---

## Referenzen für die Empfehlungen

- [.NET: Typed Clients in Singleton-Services vermeiden](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory#avoid-typed-clients-in-singleton-services).
- [.NET: Asynchrones Datei-I/O](https://learn.microsoft.com/en-us/dotnet/standard/io/asynchronous-file-i-o).
- [Avalonia: Performance und Virtualisierung](https://docs.avaloniaui.net/docs/app-development/performance).

Diese Referenzen stützen die Lösungsrichtung. Die konkreten Findings ergeben sich aus den oben verlinkten Sourcen und Reproduktionen der geprüften Codebasis.

