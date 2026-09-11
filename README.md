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
- Bei einer geladenen Textdatei prueft `Text pruefen` jede nicht leere Zeile direkt mit dem ausgewaehlten T5- oder Local-LLM-Dienst. Textdateien erhalten keine Bildrechtecke oder OCR-Marker; die Quelldatei bleibt unveraendert und Vorschlaege werden nur im Korrekturbereich angezeigt.
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
