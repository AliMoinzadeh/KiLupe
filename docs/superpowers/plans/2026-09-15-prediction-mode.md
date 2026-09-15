# Vorhersagemodus

Freigegebenes Verhalten: lokale kurze Textvorschlaege nach einer Schreibpause; Overlay am Cursor, separates Feld mitten im Text; Einfuegen nur nach Opt-in und Shortcut; erneute Kontextpruefung; Passwortfelder und konfigurierte Programme ausschliessen.

## Umsetzung
- [x] PredictionSession: stabile Momentaufnahme, Verzoegerung, Generationen gegen verspaetete Antworten, einmalige Uebernahme. Tests fuer Fokus-/Cursorwechsel und deaktivierte Uebernahme.
- [x] CaretContextReader: UI Automation auf einem Hintergrundthread, nur beschreibbare Felder mit kollabierter Auswahl und bekannter Cursorposition. Kontext begrenzen, keine OCR-Schaetzungen fuer Einfuegen.
- [x] Lokales Qwen ueber vorhandene LLamaSharp-Integration, eigener Fortsetzungsprompt und kurze Ausgabe; keine Netzwerkuebertragung.
- [x] Nicht aktivierendes Overlay, Timer/Abbruch, Unicode-Eingabe nach Loslassen des Shortcuts und erneuter Pruefung.
- [x] Einstellungsdialog: Modus, Einfuegeerlaubnis, Pause, Shortcut, Programmausschluesse. In MainWindow integrieren und beim Beenden aufraeumen.
- [x] Gezielte Tests, gesamter Testlauf, Build und Bedienungsdokumentation.

Grenzen: Anwendungen ohne zugreifbare Textauswahl/Cursorgeometrie werden uebersprungen; maximal 20.000 Zeichen pro zugreifbarem Textfeld. Keine garantierte Unterstuetzung erhoehter Programme. Zwischen letzter Pruefung und Betriebssystemeingabe gibt es keine anwendungsuebergreifende atomare Transaktion.

## Verifikation

145 Tests bestanden, ein bestehender Test uebersprungen. Modell-Smoke-Test lieferte eine lokale Fortsetzung in rund drei Sekunden inklusive Laden. Desktop-Smoke-Test vorbereitet, hier aber ohne bestaetigten Durchlauf: Windows verweigert SetForegroundWindow fuer das eigene Testfenster. Cursor, Overlay und Einfuegen muessen deshalb noch einmal in einer interaktiven Benutzersitzung geprueft werden.
