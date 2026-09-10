# Ki-Lupe Configurable Models and Correction Overlay Design

## Goal

Ki-Lupe soll alternative lokale Analysemodelle ueber die UI testbar machen und erkannte Textfehler zusaetzlich mit einem kopierbaren Korrekturvorschlag beantworten.

Die erste Ausbaustufe umfasst:

- YOLOv8n und RT-DETR als auswaehlbare Objektmodelle,
- `Auto`, DirectML und CPU als Ausfuehrungsoptionen, soweit der jeweilige Adapter dies unterstuetzt,
- einen festen Katalog lokaler Textkorrekturmodelle,
- ein breites Korrektur-Popup am Orb im Schwebemodus,
- einen unteren, in der Hoehe veraenderbaren Korrekturbereich innerhalb der linken Bildflaeche im Hauptfenster,
- Kopieren des gesamten Vorschlags oder der aktuellen Textauswahl.

Die bestehende Tesseract-/Hunspell-Pipeline bleibt der stabile OCR- und Wortfehlerpfad.

## Nichtziele

- Kein beliebiges LLM-Format ohne passenden Adapter.
- Keine Cloud-API und kein Upload erkannter Texte.
- Keine Behauptung, dass DirectML automatisch einen NPU ausfuehrt.
- Keine exakten Bildschirmrechtecke fuer Grammatik- oder Satzfehler, die nur aus einem neu erzeugten korrigierten Text abgeleitet werden.
- Keine Ablosung von Tesseract oder Hunspell in der ersten Ausbaustufe.

## Benutzeroberflaeche

### Modelloptionen

Im Hauptfenster gibt es einen kompakten Modellbereich mit drei Auswahlfeldern:

1. **Objektmodell**
   - YOLOv8n
   - RT-DETR
2. **Textkorrektur**
   - Keine Korrektur
   - Deutsch: lokales T5-/ONNX-Korrekturmodell
   - weitere registrierte Katalogeintraege, sobald ein kompatibler Adapter vorhanden ist
3. **Ausfuehrung**
   - Auto
   - DirectML
   - CPU

Jeder Eintrag zeigt seinen Verfuegbarkeitsstatus. Ein fehlendes Modell deaktiviert nicht die restliche Anwendung. Die Statuszeile nennt den konkreten Grund und den verwendeten Provider.

Die Auswahl wird fuer neue Analysen verwendet. Eine laufende Analyse wird abgebrochen, bevor die Dienstgruppe neu erstellt wird.

### Korrektur-Popup im Schwebemodus

Das Popup wird nahe am Orb positioniert und bleibt als eigenes WPF-Fenster oberhalb anderer Fenster sichtbar. Es enthaelt:

- erkannte Originalzeile,
- korrigierten Vorschlag,
- Modell- und Providerstatus,
- eine selektierbare, mehrzeilige Textbox fuer den Vorschlag,
- **Alles kopieren**,
- **Auswahl kopieren**,
- Schliessen oder Ausblenden.

Das Popup wird nur bei einem neuen, nichtleeren Vorschlag aktualisiert. Bei einer Analyse ohne Korrekturmodell wird der Inhalt geleert und der Status am Orb bleibt sichtbar.

### Korrekturcontainer im Hauptfenster

Der Hauptmodus verwendet denselben Korrekturinhalt wie das Popup, aber als Container innerhalb der bestehenden Arbeitsflaeche. Der Container:

- liegt bei Textanalysen unterhalb der Bildflaeche in der linken Arbeitsbereichsspalte,
- kann ueber einen horizontalen `GridSplitter` in der Hoehe veraendert werden,
- verschwindet bei leerem Vorschlag, damit die Bildflaeche wieder den gesamten linken Bereich nutzt,
- verwendet dieselbe selektierbare Textbox und dieselben Kopieraktionen,
- wird bei leerem Vorschlag ausgeblendet.

Die Dockposition ist in dieser Ausbaustufe bewusst auf unten festgelegt. Eine spaetere Erweiterung kann eine seitliche Dockrichtung oder freies Drag-and-Drop ergaenzen, ohne den Analysevertrag zu aendern.

## Architektur

### Modellkataloge

Die Auswahl wird nicht direkt an konkrete UI-Klassen gekoppelt. Zwei kleine Kataloge liefern stabile IDs, Anzeigenamen und Verfuegbarkeitsinformationen:

- `ObjectModelCatalog`: YOLOv8n und RT-DETR,
- `TextCorrectionModelCatalog`: deaktiviert und registrierte lokale Korrekturmodelle.

Ein Katalogeintrag enthaelt mindestens Modell-ID, Anzeigename, erwartete Dateien, Lizenzhinweis, Adaptertyp und Status. Die Kataloge bleiben erweiterbar, ohne die XAML-Controls zu veraendern.

### Analysefabrik und Provider

`AnalysisServiceFactory` wird um eine Auswahlkonfiguration erweitert. Sie erzeugt nur die ausgewaehlten Objekt- und OCR-Dienste. Die OCR-/Hunspell-Komponente bleibt unabhaengig vom Objektmodell.

Die ONNX-Dienste erhalten eine gemeinsame Provider-Auswahl:

- **Auto** versucht den verfuegbaren DirectML-Pfad und faellt bei Initialisierungs- oder Providerfehlern auf CPU zurueck.
- **DirectML** fordert DirectML an und meldet einen kontrollierten Fehler, wenn der Provider nicht geladen werden kann.
- **CPU** verwendet den nativen CPU-Provider.

Der Providername wird im Dienststatus ausgegeben. Ein NPU wird nur als eigener Hardwarepfad angezeigt, wenn ein dafuer registrierter ONNX-/Windows-ML-Provider auf dem System tatsaechlich verfuegbar ist. DirectML wird nicht als NPU-Garantie bezeichnet. Die Providerlogik bleibt hinter einer kleinen Session-Fabrik, damit ein spaeterer NPU-Adapter ergaenzt werden kann.

### Textkorrekturvertrag

Textkorrektur ist kein `IAnalysisService`, weil der Rueckgabewert keine Positionsmarker sind. Der neue Vertrag ist konzeptionell:

```text
ITextCorrectionService
  Name
  ModelId
  IsAvailable
  StatusText
  CorrectAsync(text, cancellationToken)
```

Das Ergebnis enthaelt Originaltext, korrigierten Text, Modellname, Providername und eine Aenderungsinformation. Leere oder unveraenderte Ausgaben werden nicht als Vorschlag angezeigt.

Der erste feste Katalogeintrag zielt auf einen kleinen deutschen T5-/ONNX-Adapter auf Basis des evaluierten deutschen Rechtschreibkorrekturmodells. Das Artefakt wird lokal ueber das vorhandene Downloadskript bezogen, bleibt aus Git ausgeschlossen und wird nur aktiviert, wenn Modell, Tokenizer und Decoder-Generation erfolgreich geladen werden. Die Gewichtsgrenze ist kleiner als 8B Parameter; die Laufzeitentscheidung richtet sich nach dem gemeldeten Provider, nicht nur nach dem Modellnamen.

### OCR- und Korrekturfluss

1. Tesseract liest Woerter mit Rechtecken.
2. Die Textanalyse gruppiert benachbarte OCR-Woerter konservativ nach Zeilen.
3. Hunspell erzeugt weiterhin `Spelling`-Ergebnisse mit den exakten Wortrechtecken.
4. Wenn ein Korrekturmodell aktiv ist, wird jede nicht leere normalisierte OCR-Zeile an den Korrekturdienst uebergeben, damit auch Grossschreibung und Satzkorrektur ohne Hunspell-Treffer moeglich bleiben.
5. Ein geaenderter, nichtleerer Satz wird als `CorrectionSuggestion` an die UI zurueckgegeben.
6. Die UI aktualisiert Popup und Hauptfenstercontainer, ohne aus dem Satz neue Rechtecke zu erfinden.

Die Korrektur wird pro Zeile begrenzt und ueber CancellationToken abgebrochen, wenn der Floating-Frame veraltet ist oder der Benutzer das Modell wechselt. Lange OCR-Ergebnisse werden auf eine kleine Zahl der fehlerhaften Zeilen begrenzt, damit der Schwebemodus responsiv bleibt.

## Fehler- und Verfuegbarkeitsverhalten

- Fehlt ein Modellordner, bleibt der Katalogeintrag sichtbar und wird als nicht verfuegbar markiert.
- Fehlt ein Tokenizer oder ist die Decoder-Ausgabe ungueltig, wird kein halbfertiger Text angezeigt; der Status nennt die Ursache.
- Scheitert DirectML, darf `Auto` auf CPU wechseln; bei explizitem DirectML wird der Fehler sichtbar gemeldet.
- Scheitert die Satzkorrektur, bleiben OCR-, Hunspell- und Objektmarker erhalten.
- Ein neues Floating-Ergebnis ersetzt einen alten Vorschlag atomar; veraltete Ergebnisse duerfen keinen Popup-Inhalt ueberschreiben.
- Kopieraktionen funktionieren nur gegen den aktuell sichtbaren Vorschlag und zeigen bei leerer Auswahl keinen irrefuehrenden Erfolg an.

## Tests

### Unit-Tests

- Kataloge liefern stabile IDs, Reihenfolge und Verfuegbarkeitsstatus.
- Die Auswahlkonfiguration erzeugt den richtigen Objektadapter.
- Provider `Auto`, DirectML und CPU werden ueber eine testbare Session-Fabrik aufgeloest.
- Fehlende Modell- und Tokenizerdateien fuehren zu kontrolliertem `IsAvailable = false`.
- OCR-Woerter werden reproduzierbar zu Zeilen gruppiert.
- Jede nicht leere OCR-Zeile kann an den Korrekturdienst weitergereicht werden; unveraenderte Antworten werden verworfen.
- Unveraenderte oder leere Korrekturausgaben werden verworfen.
- Floating-Analysen verwerfen veraltete Korrekturantworten.
- Der Korrekturbereich bleibt beim Verkleinern der Arbeitsflaeche innerhalb der linken Spalte und der `GridSplitter` veraendert reproduzierbar seine Hoehe.

### Integrationschecks

- Ohne Korrekturmodell startet die Anwendung wie bisher.
- YOLO und RT-DETR koennen nacheinander ueber die UI ausgewaehlt und ausgefuehrt werden.
- Der Korrekturvorschlag ist im Popup und im Hauptfenster selektierbar.
- Gesamter Text und aktuelle Auswahl lassen sich in die Windows-Zwischenablage kopieren.
- CPU-Fallback funktioniert auf einem System ohne DirectML.
- Der vollstaendige bestehende Testbestand und der WPF-Build bleiben erfolgreich.

## Lieferumfang

- Modellkataloge und Auswahlkonfiguration,
- Provider-/Session-Fabrik fuer die vorhandenen ONNX-Dienste,
- Korrekturdienstvertrag und lokaler ONNX-Adapter,
- Erweiterung des Sprachdaten-/Modell-Downloadskripts,
- Popup-Overlay und resizabler Bottom-Dock im Hauptfenster,
- fokussierte Tests fuer Katalog, Korrekturfluss, Copy-Text und Docking,
- README- und Architektur-Dokumentation mit Modell- und Providergrenzen.

Die Implementierung erfolgt in kleinen Schritten: zuerst reine Katalog-/Vertrags-Tests, dann Adapter und Modellverfuegbarkeit, danach UI und Floating-Integration. Gewichte werden nicht in den Quelltext oder Git eingecheckt.