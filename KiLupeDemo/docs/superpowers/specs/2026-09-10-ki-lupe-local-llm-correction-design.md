# Ki-Lupe: Lokale LLM-Textkorrektur

## Ziel

Ki-Lupe soll neben dem vorhandenen T5-Rechtschreibmodell ein staerkeres lokales Sprachmodell fuer deutsche Rechtschreib-, Grammatik-, Grossschreibungs- und Zeichensetzungskorrekturen anbieten. Die Funktion bleibt vollstaendig offline und darf den bestehenden OCR-, Hunspell- und T5-Pfad nicht voraussetzen oder stoeren.

## Entscheidung

Die erste LLM-Integration verwendet `LLamaSharp` mit dem CPU-Backend und einem quantisierten Qwen2.5-3B-Instruct-GGUF-Artefakt. Die vorhandene `ITextCorrectionService`-Schnittstelle bleibt die einzige Grenze zur UI. Das LLM wird als eigener Katalogeintrag aktiviert, wenn die lokale GGUF-Datei vorhanden ist; ohne Datei bleibt die Anwendung wie bisher nutzbar.

Das Modell wird lazy geladen, damit das Starten oder Wechseln der UI-Auswahl nicht durch das mehrere-GB-Artefakt blockiert wird. Pro Anfrage wird ein neuer Kontext erzeugt, waehrend die geladenen Gewichte wiederverwendet werden. Der Inferenzpfad laeuft seriell, kann abgebrochen werden und akzeptiert nur nichtleere, bereinigte Textausgaben.

## Modell- und Promptvertrag

Der Eingabetext bleibt eine einzelne OCR-Zeile. Der Systemprompt verlangt ausschliesslich den korrigierten deutschen Text, ohne Erklaerung, Markdown oder Alternativen. Die Korrektur darf keine Fakten, Namen oder Zeilenstruktur erfinden. Die Ausgabe wird gegen den Originaltext verglichen; unveraenderte Ausgaben werden verworfen.

Das Modell wird mit einem begrenzten Kontext und einer begrenzten Maximalzahl neuer Tokens betrieben. Der CPU-Pfad ist der erste kompatible Backend-Pfad; DirectML bleibt fuer YOLO, RT-DETR und T5 unveraendert. Ein spaeteres GPU-Backend kann hinter derselben Servicegrenze ergaenzt werden.

## Benutzeroberflaeche

Der neue Eintrag erscheint im vorhandenen Auswahlfeld fuer Textkorrektur. Die Option wird nur als verfuegbar angezeigt, wenn das GGUF-Artefakt gefunden wird. Der Benutzer kann zwischen keiner Korrektur, dem bestehenden deutschen T5 und dem lokalen LLM waehlen. Der Korrekturbereich im Hauptfenster und das Orb-Popup verwenden weiterhin denselben `CorrectionPresentationState`.

## Fehlerverhalten

- Fehlt das GGUF-Modell, bleibt der LLM-Eintrag sichtbar und meldet den erwarteten Pfad.
- Scheitert das Lazy-Loading oder die native Backend-Initialisierung, wird kein UI-Fehler geworfen; der Dienst liefert einen sichtbaren Status und keine Vorschlaege.
- Abbruch einer veralteten OCR-Anfrage verwirft auch die laufende LLM-Antwort.
- Der T5-Dienst bleibt unabhaengig und kann weiterhin verwendet werden.
- Das Modell und alle Ausgaben bleiben lokal; keine Netzwerkverbindung wird zur Laufzeit benoetigt.

## Validierung

- Unit-Tests pruefen Modellkatalog, fehlendes LLM-Artefakt, Status und Factory-Auswahl.
- Der vorhandene T5- und OCR-Testbestand bleibt gruen.
- Ein optionaler Smoke-Test wird nur ausgefuehrt, wenn das GGUF-Artefakt lokal installiert ist; er prueft einen deutschen Grammatikfehler und misst die Laufzeit.
- README und Downloadskript dokumentieren Quelle, Dateiname, ungefaehre Groesse, Lizenzpruefung und lokalen Installationsort.
