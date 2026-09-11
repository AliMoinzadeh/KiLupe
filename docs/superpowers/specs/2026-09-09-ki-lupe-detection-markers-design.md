# Ki-Lupe: Visuelle Treffer-Markierungen im Schwebemodus

## Ziel

Der Schwebemodus soll nicht nur eine Trefferzahl in der Orb-Leiste anzeigen. Nutzer sollen direkt am Bildschirm sehen, wo die aktive Analyse ein Objekt, Text oder einen moeglichen Rechtschreibfehler gefunden hat.

## Bestaetigte Auswahl

- Variante 1: separate, transparente Bildschirm-Markierungsebene.
- Markierungen werden pro Analyse ersetzt, nicht dauerhaft aufaddiert.
- Markierungen bleiben ungefaehr zwei Sekunden sichtbar und blenden sanft aus.
- Pause, Moduswechsel, Beenden und ein expliziter Loeschbefehl entfernen alle Markierungen sofort.
- Die Markierungsebene bleibt klickdurchlaessig; Orb-Schaltflaechen bleiben bedienbar.

## Visuelles Verhalten

- Objektergebnisse erhalten einen farbigen Kreis um ihren Trefferbereich.
- Erkannte Textstellen erhalten eine kompakte farbige Rechteckmarkierung.
- Rechtschreibtreffer erhalten eine rote, deutlich erkennbare Markierung.
- Die Farben unterscheiden Ergebnisarten, ohne die darunterliegende Anwendung zu verdecken.
- Treffer werden nahe ihrer tatsaechlichen Bildschirmposition gezeichnet, nicht neben der Orb-Leiste.
- Die Orb-Leiste zeigt weiterhin Anzahl und kurze Labels der aktuellen Treffer.

## Architektur

### ScreenCaptureService

Die Aufnahme liefert neben dem `BitmapSource` auch den Bildschirmursprung und die Groesse des aufgenommenen Bereichs. Fuer Cursoraufnahmen ist das der geklemmte Cursorbereich; fuer Vollbildaufnahmen der virtuelle Bildschirm.

### DetectionOverlayWindow

Ein eigenes WPF-Fenster deckt den virtuellen Bildschirm transparent ab. Es verwendet einen `Canvas` fuer die Markierungen und Win32-Stile fuer `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW` und Klickdurchlaessigkeit. Es wird nicht waehrend jeder Aufnahme ein- und ausgeblendet, damit kein Flackern entsteht.

Das Fenster bietet mindestens:

- `ShowResults(IReadOnlyList<AnalysisResult>, CaptureRegion, TimeSpan)`
- `ClearResults()`
- `SetVisible(bool)`

Die Koordinaten werden von Bildkoordinaten in Bildschirmkoordinaten umgerechnet. Markierungen werden mit einem DispatcherTimer oder einer Storyboard-Animation ausgeblendet; die Overlay-Ebene bleibt bestehen.

### MainWindow / Floating Loop

Nach einer erfolgreichen Analyse uebergibt die Floating-Schleife Ergebnisse und Capture-Region an das Detection-Overlay. Bei leerem Ergebnis werden alte Markierungen geloescht. Bei Pause, Moduswechsel, Stop und Fensterende wird `ClearResults()` ausgefuehrt.

Der neue Clear-Orb loescht nur visuelle Treffer und laesst den Analysemodus unveraendert. Das bestehende Results-Orb beendet weiterhin den Schwebemodus und zeigt die Treffer im Hauptfenster.

## Fehlerbehandlung

- Fehlende Bildschirmkoordinaten oder ungueltige Regionen erzeugen keine Markierung und keine UI-Ausnahme.
- Wenn die Overlay-Erstellung fehlschlaegt, bleibt die Analyse aktiv; der Orb-Status zeigt den Fehler.
- Das Overlay speichert keine Screenshots und enthaelt nur die kurzlebigen WPF-Markierungen.

## Tests

- Pure Koordinatenumrechnung fuer Cursor- und Vollbildregionen.
- Ergebnisart wird auf die erwartete Markierungsform/Farbe abgebildet.
- Leere Ergebnisse und `ClearResults()` entfernen alle Markierungen.
- Fade-Dauer bzw. Ablauf entfernt alte Markierungen ohne neue Analyse.
- Bestehende Analyse-, Capture- und Buildtests bleiben gruen.

## Nicht Bestandteil

- Permanente Annotationen.
- Bearbeitung oder Speicherung der aufgenommenen Bilder.
- Treffer-Markierungen im Normalmodus ausserhalb der bestehenden Ergebnis-Overlays.
