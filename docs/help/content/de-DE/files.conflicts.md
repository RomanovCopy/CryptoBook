# Namenskonflikte lösen

Ein Konflikt erscheint, wenn Kopieren oder Verschieben einen belegten Zielnamen verwenden würde. Lesen Sie vor der Auswahl Quell- und Zielpfad: Gleiche Namen bedeuten nicht gleichen Inhalt.

1. Prüfen Sie beide Pfade im Fenster **Namenskonflikt**.
2. Wählen Sie den Umgang mit dem Objekt:

![Schritt 2. Quelle, Ziel und Konfliktentscheidungen](images/S20-a.png "Schritt 2. Quelle, Ziel und Konfliktentscheidungen")

| Entscheidung | Ergebnis |
| --- | --- |
| **Ersetzen** | Bereitet die neue lokale Kopie vor und behält das bisherige Ziel bis zum Abschluss des Stapels. Abbruch oder Fehler stellt das bisherige Ziel wieder her. Ein Ordner wird vollständig ersetzt und nicht zusammengeführt. |
| **Überspringen** | Behält das vorhandene Ziel und überträgt diese Quelle nicht. |
| **Beide beibehalten** | Behält das vorhandene Objekt und überträgt die Quelle unter einem freien Namen. Prüfen Sie den tatsächlichen Namen. |
| **Abbrechen** | Beendet die Planung dieses Stapels. Während die Konfliktentscheidungen gesammelt werden, hat noch keine Übertragung dieses Stapels begonnen. |

**Auf alle Konflikte anwenden** wiederholt die Entscheidung für weitere Konflikte dieses Stapels. Lassen Sie das Feld frei, wenn später andere Entscheidungen nötig sind. Das Schließen des Konfliktfensters bedeutet Abbruch.

## Nach der Auswahl

Warten Sie auf das Ende, aktualisieren Sie die Ansicht und vergleichen Sie Ziel und Quelle. Beim Überspringen einer Verschiebung bleibt die Quelle bestehen. Eine Kopie im eigenen Ordner erhält einen freien Namen; eine Verschiebung an den bereits belegten eigenen Speicherort wird übersprungen.

Ein Abbruch im Fortschrittsfenster setzt bereits ausgeführte Aktionen des lokalen Stapels zurück. Das vorherige Ziel bleibt bis zum Abschluss der Übertragung erhalten. Bei blockierenden Sperren oder belegten Namen wählen Sie **Vorgänge wiederherstellen** im Ordner mit dem in der Meldung genannten Protokoll. Nach einem Prozessabbruch öffnen Sie den Zielordner und verwenden diesen Befehl.

## Verwandte Themen

- [Dateien kopieren und verschieben](help:files.transfer)
- [Schreibfehler](help:recovery.write)
