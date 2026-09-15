# Vergleich der lokalen Textvorhersage

Stand: 15.09.2026. Zehn vor der Aenderung festgelegte Beispiele, dasselbe lokale Qwen2.5-3B-Instruct-Q4_K_M-Modell auf der CPU. Der bisherige Chat-Aufruf wurde mit direkter Fortsetzung eines vorgegebenen Ausgabetexts verglichen. Kein Modellwechsel und keine Cloud-Anfragen.

Die neue Variante verwendet deterministische Wortwahl, entfernt alleinstehende Leerzeichen am Prompt-Ende und erzeugt angefangene Woerter vom Wortanfang neu. Nur wenn der getippte Wortteil uebereinstimmt, wird die fehlende Fortsetzung angeboten. Bereits vorhandener Text hinter dem Cursor wird nicht erneut eingefuegt. Das tatsaechliche Tokenbudget wird vor jeder Anfrage geprueft.

| Text vor dem Cursor | Bisherige Einfuegung | Neue Einfuegung |
|---|---|---|
| Warum sollte ich vertrauen auf deine korrektheit? du hast gerade selber einen Fehler | vergessen sind. | gemacht. |
| Ich habe meinen Schlüssel zu Hause | vergessen und werde ihn morgen bringen. | vergessen, was bedeutet, dass ich nicht in der Lage bin, die Tür zu öffnen. |
| Wenn es morgen regnet, | vergiss es, ich bleibe doch untergebracht. | werde ich trotzdem losgehen. |
| Könntest du mir bitte | prüfen, ob die Daten korrekt sind. | erklären, wie man das Programm richtig einrichtet? |
| Bitte schicken Sie mir die Unterlagen bis | bis ich sie für meine Präsentation benötige. | zum 30. September, damit ich sie in der geplanten Zeit bearbeiten kann. |
| Ich freue mich dar | auf die Gelegenkeine. | auf, diese Arbeit zu beenden und sie an Sie zu senden. |
| Wir treffen uns | um 15 Uhr. | (kein Vorschlag) |
| Thank you for your message. I will | consider your suggestion and get back. | respond promptly. |
| Die Rechnung wurde gestern | gestzt und ich kann sie heute abholen. | eingereicht, und ich hoffe, sie werden sie rechtzeitig begleichen. |
| Das ist kein Problem, weil | noch viel passiert. | wir immer neue Methoden und Technologien einbringen, um die Effizienz zu erhöhen. |

Beim Beispiel „Wir treffen uns “ steht „um 15 Uhr.“ bereits hinter dem Cursor. Das Ausblenden der wiederholten Zeitangabe ist hier das gewuenschte Ergebnis.
Die neun Anfragen nach dem ersten Modellladen dauerten im Mittel 1,43 Sekunden. Der erste Aufruf inklusive Laden dauerte 2,33 Sekunden.

Dies ist ein kleiner qualitativer Vergleich, keine allgemeine Genauigkeitsmessung. Der Ausgangslauf nutzte die bisherige zufallsbasierte Auswahl, die neue Variante greedy decoding. Sprache, Fachwissen und persoenliche Absicht werden weiterhin nicht immer korrekt getroffen. Auch in den besseren Beispielen sind manche Vorschlaege laenger oder spezifischer als notwendig. Keine Saetze aus dem Testkorpus wurden als Beispiele in den Produktionsprompt uebernommen.

Wiederholen: `dotnet run --project tests/PredictionSmoke -- --quality`. Der Test gibt je Beispiel eine JSON-Zeile mit Kontext, Einfuegung, zusammengesetztem Text und Laufzeit aus. Er ist bewusst ein manueller Qualitaetsvergleich; die normale Testsuite deckt die deterministische Kontext- und Einfuegelogik ab.
