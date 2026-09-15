# Ki-Lupe

Ki-Lupe ist eine lokale .NET-8-WPF-Anwendung fuer Windows. Sie vergroessert Bildbereiche und kann lokal nach Objekten, Text und moeglichen Rechtschreibfehlern suchen. Bildschirmaufnahmen werden nur im ausdruecklich aktivierten Schwebemodus erstellt, im Speicher verarbeitet und nicht gespeichert.

## Voraussetzungen

- Windows mit .NET 8 SDK
- Ein kompatibles YOLO-ONNX-Modell fuer die Objekterkennung
- Optional: RT-DETR-ONNX, das lokale deutsche T5-Korrekturmodell (247 Mio. Parameter, Apache-2.0) und ein lokales Qwen2.5-3B-Instruct-GGUF
- Fuer OCR: `tessdata/deu.traineddata` und `tessdata/eng.traineddata`
- Fuer Rechtschreibpruefung: `dictionaries/de_DE.aff`, `dictionaries/de_DE.dic`, `dictionaries/en_US.aff` und `dictionaries/en_US.dic`

Die Datenordner liegen neben der Anwendung, also normalerweise unter `bin/Debug/net8.0-windows/`. Die ONNX-Datei muss `yolov8n.onnx` heissen und entweder neben der Anwendung oder im Projektordner liegen. Das vorhandene `yolov8n.pt` wird nicht direkt von ONNX Runtime geladen.

Wenn nur `yolov8n.pt` vorhanden ist und Python mit Ultralytics installiert ist, kann die benoetigte Datei lokal erzeugt werden:

```powershell
python -c "from ultralytics import YOLO; YOLO('yolov8n.pt').export(format='onnx', imgsz=640, dynamic=False, opset=12, simplify=False, nms=False)"
```

Der Export erzeugt `yolov8n.onnx` im Projektordner. Beim Build wird die Datei automatisch nach `bin/Debug/net8.0-windows/` kopiert.

Die optionalen RT-DETR- und Korrekturartefakte koennen mit dem Projektskript geladen werden:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Download-KiLupeModels.ps1
```

Das Korrekturmodell wird unter `artifacts/models/german-spelling-correction-onnx/` abgelegt. Es wird nur lokal ausgefuehrt; DirectML ist eine optionale Ausfuehrungsroute und keine Zusage fuer eine bestimmte NPU.

Fuer staerkere deutsche Grammatik- und Satzkorrekturen kann zusaetzlich das quantisierte Qwen2.5-3B-Instruct-Modell geladen werden:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Download-KiLupeLlmModel.ps1
```

Das Skript legt `Qwen2.5-3B-Instruct-Q4_K_M.gguf` unter `artifacts/models/qwen2.5-3b-instruct/` ab. Die Datei benoetigt ungefaehr 2 GB Speicherplatz und zur Laufzeit mehrere GB Arbeitsspeicher; die erste Integration verwendet das LLamaSharp-CPU-Backend und kann auf langsameren Rechnern deutlich laenger rechnen. Der Download ist ausdruecklich opt-in und wird nicht in Git oder in den Build-Output kopiert. Ohne GGUF bleibt der Eintrag sichtbar, meldet aber den fehlenden Pfad. Zur Laufzeit arbeitet das Modell offline. Die kleinere T5-Auswahl bleibt unabhaengig als Rechtschreib-/Grossschreibungs-Fallback verfuegbar.

## Starten

```powershell
dotnet restore --ignore-failed-sources
dotnet build KiLupeDemo.csproj
dotnet run --project KiLupeDemo.csproj
```

Falls die private NuGet-Quelle nicht erreichbar ist, erlaubt `--ignore-failed-sources` die Aufloesung der oeffentlichen Pakete. Die verbleibende `NU1900`-Warnung betrifft nur die Sicherheitsmetadaten dieser Quelle.

## Bedienung

Im Normalmodus:

- `Bild oeffnen` laedt ein PNG-, JPEG- oder BMP-Bild.
- `Textdatei laden` laedt lokale `.txt`, `.md`, `.log`, `.csv`, `.json` oder `.xml` Dateien. Der Inhalt wird schreibgeschuetzt angezeigt; UTF-8 sowie UTF-16 mit BOM werden erkannt.
- Die kreisfoermige Lupe folgt der Maus ueber dem Bild.
- `Objekte suchen` verwendet YOLO mit DirectML und faellt bei Bedarf auf CPU zurueck.
- `Text pruefen` verwendet lokales Tesseract sowie deutsche und englische Hunspell-Woerterbuecher.
- Die Auswahlfelder erlauben YOLOv8n oder RT-DETR, Auto/DirectML/CPU sowie keine Korrektur, das deutsche T5-Modell oder das lokale Qwen-LLM. Nicht vorhandene Artefakte bleiben sichtbar, werden aber als nicht verfuegbar gemeldet.
- Bei aktiver Textkorrektur werden erkannte OCR-Zeilen lokal geprueft; nur geaenderte Vorschlaege erscheinen unterhalb der Bildflaeche im linken Arbeitsbereich. Das vorhandene T5-Modell ist vor allem fuer Rechtschreibung sowie Gross-/Zeichensetzung geeignet und ersetzt noch keine allgemeine Grammatikpruefung. Die Hoehe kann per Trennlinie angepasst werden; der Text kann vollstaendig oder markiert kopiert werden.
- Bei einer geladenen Textdatei prueft `Text pruefen` die Rechtschreibung und ergaenzt Vorschlaege des ausgewaehlten Modells. Alle gefundenen Fehlerstellen werden gleichzeitig gelb im Text markiert, auch ohne Auswahl in der Trefferliste. Scrollen und Zeilenumbruch werden beruecksichtigt; beim Bearbeiten verschwinden veraltete Markierungen. Die Markierungen werden nicht in der Datei gespeichert.
- Fehlende Modelle oder Sprachdaten werden als Status angezeigt; die Lupe bleibt nutzbar.

Mit `Strg+Alt+L` oder `Schwebemodus` wird das Hauptfenster ausgeblendet und die Orb-Leiste ueber der Taskleiste aktiviert:

- `II` pausiert bzw. setzt Aufnahme und Analyse fort.
- `C` loescht die aktuellen visuellen Treffer-Markierungen, ohne den Schwebemodus zu beenden.
- `M` wechselt zwischen Objekt-/Textanalyse und Cursor-/Bildschirmbereich.
- `R` beendet den Schwebemodus und zeigt die Treffer im Hauptfenster.
- `X` beendet den Schwebemodus.
- Bei einer Textanalyse mit aktivem Korrekturmodell erscheint der Vorschlag als Popup am Orb; auch dort sind Auswahl- und Gesamtkopie moeglich.
- Objekte werden als gruene Kreise, erkannter Text cyan und moegliche Rechtschreibfehler orange-rot direkt an ihrer Bildschirmposition markiert.
- Markierungen ersetzen die vorherige Analyse, blenden nach etwa zwei Sekunden aus und liegen auf einer klickdurchlaessigen Ebene. Sie werden nur im Speicher gezeichnet und nie gespeichert.

Cursoranalysen laufen gedrosselt. Bildschirmanalysen laufen langsamer. Die Orb-Leiste bleibt waehrend der Aufnahme sichtbar und klickbar.

PDF, DOCX, RTF und ODT sowie semantische Bearbeitung von CSV-, JSON- oder XML-Strukturen sind derzeit nicht enthalten. Ki-Lupe speichert Korrekturen nicht automatisch.

## Tests

```powershell
dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore
```

Die Tests pruefen Ergebnisaggregation, Abbruch veralteter Analysen, fehlende Modelldaten, OCR-Status und die Begrenzung von Bildschirmaufnahmebereichen.

## Modellauswahl beim Programmstart

Die Datei `kilupe.config.json` legt die Startauswahl fest:

```json
{
  "objectModel": "YoloV8N",
  "textCorrectionModel": "None",
  "provider": "Auto"
}
```

| Einstellung | Erlaubte Werte |
| --- | --- |
| `objectModel` | `YoloV8N`, `RtDetr` |
| `textCorrectionModel` | `None` (aus), `GermanSpelling`, `LocalLlm` (Qwen 3B) |
| `provider` | `Auto`, `DirectMl`, `Cpu` |

Beim Entwickeln die Datei im Projektordner bearbeiten; der Build kopiert sie neben die EXE. Bei einer verteilten App die Datei neben `KiLupeDemo.exe` bearbeiten. Die App liest die Datei bei jedem Start aus ihrem Programmverzeichnis, unabhaengig vom Arbeitsverzeichnis.

Fehlende Felder oder eine fehlende Datei verwenden die bisherigen Standardwerte. Bei ungueltigem JSON, unbekannten Einstellungen oder ungueltigen Werten verwendet die App die Standardwerte und zeigt einen Hinweis im Statusbereich. Modellnamen waehlen vorhandene Modelle aus; fehlende Modelldateien werden weiterhin in der Modellauswahl gemeldet. Aenderungen an den Dropdowns gelten fuer die laufende Sitzung und werden nicht in die Datei zurueckgeschrieben.

## Vorfuehren im Video-Call

Im Hauptfenster unten rechts **Video-Call** aktivieren, dann den Schwebemodus starten (oder den Schalter waehrenddessen umlegen). Im Konferenzprogramm den **ganzen Bildschirm** freigeben: Die Markierungen liegen in einem eigenen transparenten Fenster und gehoeren nicht zum Fenster des darunterliegenden Programms.

Der Schalter erlaubt Bildschirmaufnahmen der Markierungen und ihrer Hover-Vorschlaege. Fuer die kurzen eigenen Analyseaufnahmen schliesst KiLupe sie voruebergehend wieder aus, damit keine eigenen Markierungen oder Labels in die OCR gelangen. Im Stream kann dadurch ein kurzes Aussetzen sichtbar sein. Fuer eine ruhige Erklaerung den Mauszeiger im Textbereich belassen und den Bildschirm-Inhalt unveraendert lassen. Ohne Video-Call-Modus bleiben die Markierungen von Aufnahmen ausgeschlossen. Der Schalter gilt fuer die laufende Sitzung.

## Kontextabhaengige Textkorrektur

Unter **Textkorrektur** das **lokale Qwen-LLM** waehlen und
**Kontextabhängig korrigieren** aktivieren. Der Schalter gilt fuer die laufende
Sitzung. Ohne ihn bleibt die bisherige vollstaendige Korrektur aktiv.
Das T5-/ONNX-Modell unterstuetzt diesen Modus nicht; bei dieser Auswahl ist der
Schalter deaktiviert.

- Woerterbuchbekannte Einzelwoerter (z. B. Ansicht, Datei) werden ohne LLM-Aenderung uebernommen.
- Einzelwoerter, Menueeintraege, Beschriftungen und unklare/abgeschnittene
  Textfragmente: nur eindeutige Tippfehler.
- Klarer Satzkontext: auch Grammatik und Zeichensetzung.
- Das LLM bestimmt den Kontext aus dem erkannten Text. Bei Fragmenten verhindert
  ein zusaetzlicher Filter neue Satzzeichen, hinzugefuegte/entfernte Woerter und
  Aenderungen an Woertern, die im deutschen oder englischen Woerterbuch stehen.
- Die Einstellung gilt fuer OCR aus geladenen Bildern und Bildschirmaufnahmen
  ebenso wie fuer geladene Textdateien. Es wird keine Schnittstelle der
  abgebildeten Anwendung und kein visuelles Modell benoetigt.

Die Erkennung ist modellabhaengig und kann sich irren. OCR-Zeilen werden einzeln
beurteilt; insbesondere bei abgeschnittenen Saetzen kann dadurch eine echte
Grammatikkorrektur ausbleiben. Ohne lesbare Woerterbuecher werden keine
Fragmentkorrekturen zugelassen. OCR- und Rechtschreibtreffer erscheinen weiterhin
direkt als gelbe Fehlermarker, auch im Schwebemodus. Die Suche wartet nicht
auf eine Modellbestaetigung und blendet keine ganzen OCR-Zeilen wegen einer
Konfidenzschwelle aus. Die korrigierte Woerterbuchpruefung akzeptiert deutsche
Substantive wie Ansicht und Datei in ihrer originalen Grossschreibung.
Die Prozentangabe beschreibt die OCR-Erkennung, nicht die Sicherheit der Korrektur.
Die optionale kontextabhaengige LLM-Pruefung betrifft die Korrekturvorschlaege.
Die einfache Absicherung bekannter Einzelwoerter ist deterministisch. Das kleine
LLM erkennt dagegen nicht jeden Fehler in stark fehlerhaften Saetzen und ist
kein zuverlaessiger Erkenner beliebiger UI-Elemente oder unbekannter Eigennamen.
Fuer den Start mit aktiviertem Modus in `kilupe.config.json` setzen:

```json
{
  "objectModel": "RtDetr",
  "textCorrectionModel": "LocalLlm",
  "provider": "Auto",
  "contextAwareCorrection": true
}
```

Die automatisierten Schutzregeltests laufen mit der normalen Testsuite.
Ein opt-in Qualitaetstest prueft zehn kurze Beispiele gegen das echte lokale Modell. Einzelne Satzkorrekturen sind weiterhin nicht zuverlaessig:

```powershell
$env:KILUPE_TEST_MODEL = (Resolve-Path 'artifacts/models/qwen2.5-3b-instruct/Qwen2.5-3B-Instruct-Q4_K_M.gguf').Path
dotnet test tests/KiLupeDemo.Tests --filter FullyQualifiedName~ContextualCorrectionModelTests
```

### Textdateien pruefen und Fehlermarker

`Text pruefen` prueft geladene Textdateien zuerst mit den lokalen deutschen und
englischen Woerterbuechern. Diese Treffer bleiben auch ohne Korrekturmodell oder
bei einer leeren Modellantwort erhalten. Ein gewaehltes Modell ergaenzt danach
seine Vorschlaege. Ein Klick auf einen Treffer waehlt die Textstelle im Editor aus.

Rechtschreibfehler in geladenen Bildern erscheinen als transparente gelbe
Markerflaechen in Wortgroesse, wie im Schwebemodus. Rohe OCR-Texttreffer werden
nicht mehr als gruene Kaestchen im Bild gezeichnet. Objektmarkierungen bleiben Kaestchen.
## Vorhersagemodus

Im rechten Bereich **Vorhersagemodus ...** oeffnen und **Vorhersagemodus aktivieren** einschalten. Anschliessend in einem anderen Programm schreiben. Nach standardmaessig 700 ms Schreibpause berechnet das lokale Qwen-Modell eine kurze Ergaenzung; beim ersten Aufruf kommt die Ladezeit hinzu.

- Der Vorschlag ist ein separates, nicht fokussierendes Overlay und veraendert den Text nicht.
- Am Zeilenende erscheint Ghost Text, sofern ausreichend Platz vorhanden ist; mitten im Text oder bei Platzmangel eine kleine Flaeche unter dem Cursor.
- **Einfuegen per Tastenkuerzel erlauben** ist standardmaessig ausgeschaltet. Mit dieser Erlaubnis uebernimmt **Strg+Alt+Leertaste** den aktuellen Vorschlag. Tasten loslassen, damit keine gedrueckten Modifikatoren die Eingabe beeinflussen.
- Pause (300–5000 ms), Shortcut und ausgeschlossene Programme lassen sich im Dialog einstellen. Programmnamen mit oder ohne `.exe`, getrennt durch Komma oder Semikolon. Strg+Alt+L bleibt fuer die Lupe reserviert.
- Einstellungen werden unter `%LOCALAPPDATA%\KiLupe\prediction.json` gespeichert. Textkontext wird weder dort gespeichert noch an einen Cloud-Dienst gesendet.
- Cursor-, Text- und Fensterwechsel verwerfen alte Vorschlaege. Vor der Uebernahme wird das aktive Feld erneut gelesen; unsichere oder veraltete Vorschlaege werden nicht eingefuegt. Die Zwischenablage wird nicht verwendet.

Voraussetzung ist das vorhandene lokale Modell `Qwen2.5-3B-Instruct-Q4_K_M.gguf`. Der Vorhersagemodus arbeitet unabhaengig von der ausgewaehlten Textkorrektur. Das Modell wird bei Bedarf geladen; die Berechnung erfolgt auf der CPU.

### Grenzen und Tests

Unterstuetzt werden beschreibbare Windows-Textfelder, die Text, eine kollabierte Auswahl und sichtbare Cursorgeometrie ueber UI Automation bzw. den Windows-Textcursor bereitstellen. Passwortfelder, markierter Text, eigene KiLupe-Fenster, ausgeschlossene Programme und Textfelder mit mehr als 20.000 Zeichen werden uebersprungen. Programme ohne diese Schnittstellen (z. B. manche Spezialeditoren oder Remote-Oberflaechen) werden nicht per OCR erraten. Eingaben in Programme mit hoeheren Berechtigungen koennen durch Windows blockiert werden. Nach unbestaetigter Eingabe erfolgt kein automatischer Wiederholungsversuch.

Die erneute Kontextpruefung und die Betriebssystemeingabe sind keine atomare Transaktion: ein Fokuswechsel genau dazwischen laesst sich systemweit nicht vollstaendig ausschliessen. Vorschlagsqualitaet und Reaktionszeit haengen vom lokalen Modell und Rechner ab.

Automatisierte Tests: `dotnet test tests/KiLupeDemo.Tests`.
Freiwilliger Desktop-Smoke-Test mit eigenem Textfenster: `dotnet run --project tests/PredictionSmoke`.
Lokaler Modell-Smoke-Test: `dotnet run --project tests/PredictionSmoke -- --model`.
Der Desktop-Test benoetigt eine interaktive Sitzung mit erlaubtem Vordergrundfokus und tippt ausschliesslich in sein eigenes Testfenster.

Die Vorhersage setzt jetzt den vorgegebenen Dokumenttext direkt fort, statt eine Chat-Antwort auf den Text zu erzeugen. Angebrochene Woerter werden an einer Wortgrenze erzeugt und gegen den bereits getippten Teil geprueft. Wiederholter Folgetext wird entfernt; bei fehlender passender Ergaenzung bleibt das Overlay leer. Das lokale Modell bleibt unveraendert. Den dokumentierten Vorher-/Nachher-Vergleich und den wiederholbaren Testlauf beschreibt [docs/prediction-quality.md](docs/prediction-quality.md).