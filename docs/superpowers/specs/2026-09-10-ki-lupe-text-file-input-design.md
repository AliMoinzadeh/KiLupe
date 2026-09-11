# Ki-Lupe Textdatei-Eingabe

> Stand: 2026-09-10

## Ziel

Ki-Lupe soll neben Bildern lokale Textdateien laden und deren Zeilen ueber den bestehenden T5- oder Local-LLM-Korrekturvertrag pruefen koennen. Der Bildpfad mit OCR, Objektanalyse und Bildschirmmarkern bleibt unveraendert.

## Umfang

Unterstuetzte Endungen der ersten Stufe:

- `.txt`
- `.md`
- `.log`
- `.csv`
- `.json`
- `.xml`

Die Dateien werden als Text behandelt. Es gibt keine semantische JSON-/XML- oder Tabellenbearbeitung. PDF, DOCX und andere Office-Formate sind nicht Teil dieser Stufe.

## Benutzeroberflaeche

- Neben `Bild oeffnen` erscheint `Textdatei laden`.
- Der Textdatei-Dialog filtert die oben genannten Endungen und bietet weiterhin `Alle Dateien` an.
- Eine geladene Textdatei wird in einer schreibgeschuetzten, umbrechenden Textansicht innerhalb der linken Arbeitsflaeche angezeigt.
- Der Korrekturbereich bleibt unterhalb der Textansicht angedockt und verwendet dieselbe Darstellung wie bei OCR-Korrekturen.
- `Objekte suchen` und der Schwebemodus bleiben fuer Textdateien deaktiviert bzw. melden, dass eine Bildquelle benoetigt wird.
- `Text pruefen` verwendet bei Textdateien direkte Zeilen und startet keine OCR.
- Beim Laden eines neuen Dokuments werden alte Treffer und Korrekturvorschlaege request-id-sicher verworfen.

## Architektur

### Eingabezustand

`MainWindow` fuehrt zusaetzlich zum bisherigen `BitmapSource` einen Dokumentzustand:

- geladener Dateipfad,
- vollstaendiger Text,
- Dokumentmodus Bild oder Text.

Der Dokumentzustand entscheidet, welche Aktion `Text pruefen` ausfuehrt. Die Bildanalyse bleibt an `currentImage` und `AnalysisCoordinator` gebunden.

### Textdatei-Lader

Ein kleiner lokaler Lader liest den Text synchron in einem Hintergrundtask oder vor der UI-Aktualisierung mit begrenzter Dateigroesse. Er verwendet `StreamReader` mit BOM-Erkennung:

- UTF-8 mit oder ohne BOM,
- UTF-16 Little Endian/Big Endian mit BOM.

Bei nicht lesbaren Bytes oder IO-Fehlern wird ein konkreter Status angezeigt. Der Lader veraendert die Datei nicht und schreibt keine Korrekturen zurueck.

### Korrekturvertrag

`CorrectionCoordinator` erhaelt einen zweiten, textbasierten Einstieg fuer `IEnumerable<string>` bzw. Zeilen. Er bildet keine kuenstlichen `AnalysisResult`-Objekte und benoetigt keine Rechtecke. Der bestehende OCR-Einstieg bleibt fuer `CorrectionLineGrouper` erhalten.

Beide Einstiege teilen:

- Verfuegbarkeitspruefung,
- Abbruch ueber `CancellationToken`,
- Filter fuer leere und unveraenderte Ergebnisse,
- Rueckgabe von `CorrectionSuggestion`.

Die vorhandene `CorrectionPresentationState` kann die zeilenweisen Vorschlaege unveraendert zusammenfassen.

## Status und Fehler

- Fehlende Korrekturmodelle verhalten sich wie bisher: kein Absturz, kein Vorschlag, sichtbarer Modellstatus.
- Eine leere Datei zeigt einen geladenen, aber nicht pruefbaren Textzustand.
- Ein IO-/Encoding-Fehler bleibt im Statusfeld und loescht nicht ungefragt die zuletzt sichtbare Datei.
- Die Datei wird nicht automatisch gespeichert. Kopieren aus dem bestehenden Korrekturbereich bleibt moeglich.
- Bildaktionen bei aktivem Textmodus werden klar als nicht verfuegbar gemeldet.

## Tests

- Textdatei-Lader liest UTF-8, UTF-8-BOM und UTF-16-BOM korrekt.
- Unterstuetzte Endungen werden akzeptiert; nicht vorgesehene Endungen werden im Dialog nicht als Texttyp beworben.
- Leere Dateien und IO-Fehler liefern kontrollierte Ergebnisse.
- Textbasierter `CorrectionCoordinator` korrigiert jede nicht leere Zeile, verwirft leere/unveraenderte Antworten und reicht Abbruch weiter.
- Bildbasierter OCR-Korrekturpfad bleibt durch die bestehenden Tests abgedeckt.
- WPF-Build und vollstaendige Test-Suite bleiben gruen.

## Nicht-Ziele

- PDF-, DOCX-, RTF- oder ODT-Parsing.
- Positionsmarker innerhalb des Textdokuments.
- Automatisches Ueberschreiben oder Speichern unter.
- Semantische Verarbeitung von CSV, JSON oder XML.
- Netzwerkzugriff.
