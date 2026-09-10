# Ki-Lupe: Design der ersten WPF-Version

## Ziel

Ki-Lupe ist eine lokale Windows-Anwendung, mit der Nutzer Bilder und sichtbare Bildschirmbereiche vergroessern und nach auffaelligen Inhalten durchsuchen koennen. Die erste Version soll Katzen bzw. andere Objekte erkennen, Text aus Bildern lesen und erkannte Rechtschreibfehler markieren. Die Verarbeitung erfolgt lokal; es ist keine Cloud-API erforderlich.

Eine spaetere Version kann die Oberflaeche auf WinUI 3 umstellen. Die erste Version bleibt deshalb bewusst bei WPF und .NET 8.

## Nutzerablaeufe

### Normalmodus

- Die Anwendung zeigt eine geoeffnete Bilddatei in einer grossen Arbeitsflaeche.
- Eine kreisfoermige Lupe folgt der Maus innerhalb der Bildflaeche.
- Schwebende Werkzeuge behalten die Bildflaeche im Mittelpunkt.
- Nutzer koennen Bild oeffnen, Objekterkennung starten, OCR/Rechtschreibpruefung starten und die Trefferliste oeffnen.
- Treffer werden im Bild kontextuell markiert. Eine Detailansicht zeigt Trefferart, Text bzw. Objektname und Konfidenz.

### Schwebemodus

- `Strg+Alt+L` schaltet den Modus global ein oder aus.
- Das Hauptfenster wird minimiert.
- Eine kleine, immer sichtbare und klickdurchlaessige Orb-Leiste schwebt standardmaessig unten ueber der Taskleiste.
- Orbs bieten Pause/Fortsetzen, Analysemodus, Trefferliste und Beenden.
- Die Anwendung liest den sichtbaren Bildschirm nur waehrend dieses aktivierten Modus.
- Die Mausposition wird global verfolgt. Eine Region um den Mauszeiger wird aufgenommen und analysiert.
- Die Lupenanalyse reagiert schnell, wird aber zeitlich gedrosselt und nach Mausbewegungen gebuendelt.
- Ein optionaler Vollbildmodus analysiert den sichtbaren Bildschirm langsamer im Hintergrund.
- Treffer werden mit einer kurzen, nicht blockierenden Markierung nahe dem Mausbereich und ueber den Orb-Status angezeigt.
- Der Pause-Orb stoppt sofort Bildschirmaufnahme und Analyse.

## Technischer Aufbau

### Projekt

- WPF-Anwendung auf .NET 8 fuer Windows.
- Das bestehende Projekt wird von einer Konsolenanwendung zu einer WPF-Anwendung umgestellt.
- Der bestehende ONNX-Runtime-DirectML-Verweis bleibt die bevorzugte Beschleunigung.
- DirectML wird optional aktiviert; bei fehlender Hardware oder Provider-Problemen faellt die Anwendung auf CPU zurueck.

### Komponenten

- `MainWindow`: Normalmodus, Bildflaeche, Werkzeuge und Trefferliste.
- `MagnifierAdorner`: kreisfoermige Darstellung der vergroesserten Bildregion.
- `OrbOverlayWindow`: transparenter, immer sichtbarer und klickdurchlaessiger Schwebemodus mit den Steuer-Orbs.
- `GlobalHotkeyService`: Registrierung und Aufhebung von `Strg+Alt+L`.
- `ScreenCaptureService`: Erfassung eines Mausbereichs oder des sichtbaren Bildschirms, ohne die eigene Overlay-Oberflaeche in die Analyse aufzunehmen.
- `ObjectDetectionService`: ONNX-Modell laden, Eingabe normalisieren, Objekte auswerten und Treffer zurueckgeben.
- `TextRecognitionService`: lokale OCR fuer Deutsch und Englisch.
- `SpellingService`: lokale Woerterbuecher fuer Deutsch und Englisch und Markierung unbekannter oder fehlerverdaechtiger Woerter.
- `AnalysisCoordinator`: steuert Modi, Drosselung, Abbruch laufender Analysen und Zusammenfuehrung der Ergebnisse.
- `AnalysisResult`: gemeinsames Ergebnisformat fuer Objekt-, Text- und Rechtschreibtreffer.

Die Erkennungsdienste werden ueber kleine Interfaces angesprochen. Dadurch kann die spaetere WinUI-3-Oberflaeche dieselben Analysekomponenten weiterverwenden.

## Datenfluss

1. Nutzer oeffnet ein Bild oder aktiviert den Schwebemodus.
2. `ScreenCaptureService` bzw. die Bildflaeche liefert ein Bild oder einen Mausbereich.
3. `AnalysisCoordinator` verhindert parallele, veraltete Analysen und begrenzt die Frequenz.
4. Objekterkennung, OCR und Rechtschreibpruefung verarbeiten die Aufnahme lokal.
5. Ergebnisse werden als einheitliche `AnalysisResult`-Liste zurueckgegeben.
6. Normalmodus und Orb-Overlay zeigen nur die aktuellen Ergebnisse an.

Neue Mausbewegungen duerfen eine laufende, veraltete Analyse abbrechen. Ein spaeter eintreffendes Ergebnis wird verworfen, wenn es nicht mehr zur aktuellen Analyse-ID gehoert.

## Modelle und lokale Daten

- Objekterkennung verwendet ein kompatibles YOLO-ONNX-Modell, das als separate Datei im Ausgabeverzeichnis liegt.
- OCR benoetigt lokale deutsche und englische Sprachdaten.
- Rechtschreibpruefung benoetigt lokale deutsche und englische Woerterbuchdaten.
- Fehlende Dateien werden im UI als Einrichtungshinweis angezeigt. Die Anwendung bleibt bedienbar und stuerzt nicht ab.
- Das Programm verspricht keine vollstaendige Rechtschreibkorrektur: Es markiert verdaechtige Woerter und zeigt erkannte Textstellen.

## Performance und Verhalten

- Lupenbereich: bevorzugt wenige Analysen pro Sekunde, mit Debounce nach Mausbewegung.
- Vollbildscan: deutlich niedrigere Frequenz als der Lupenbereich.
- Bildanalyse laeuft nicht im UI-Thread.
- CPU-Fallback bleibt verfuegbar.
- Pause beendet oder unterbricht die laufende Analyse und verhindert neue Aufnahmen.
- Overlay-Fenster bleibt klickdurchlaessig, ausserhalb der Orbs.

## Fehlerbehandlung

- Nicht erreichbare oder ungueltige Modelldateien werden als konkrete UI-Meldung angezeigt.
- Nicht verfuegbare DirectML-Unterstuetzung fuehrt zu CPU-Fallback.
- OCR- oder Woerterbuchdaten koennen unabhaengig fehlen; die jeweils betroffene Funktion wird deaktiviert, die restliche Anwendung bleibt nutzbar.
- Bildschirmaufnahmefehler werden abgefangen und zeigen einen erneuten Aktivierungs- oder Pausenhinweis.
- Die Anwendung nimmt standardmaessig keine dauerhaften Screenshots auf und speichert keine Aufnahmen automatisch.

## Teststrategie

- Build-Test fuer das WPF-Projekt.
- Unit-Tests fuer Ergebnisaggregation, Debounce/Analyse-ID und fehlende Modelldateien.
- Unit-Tests fuer die Umwandlung von OCR-Woertern in Rechtschreibtreffer.
- Manueller Windows-Test fuer Bild oeffnen, Lupe, globalen Hotkey, klickdurchlaessiges Overlay, Pause und CPU-Fallback.
- Manueller Test mit einem bekannten Beispielbild fuer Katzen- und Texterkennung.

## Nicht Bestandteil der ersten Version

- WinUI-3-Migration.
- Cloud-Analyse oder Synchronisierung.
- Automatische Korrektur von Text.
- Dauerhafte Speicherung erfasster Bildschirmbereiche.
- Vollstaendige Bearbeitung oder Annotation von Bildern.
