# Öffnen, umbenennen und löschen

Ein Befehl verwendet die ausgewählte Zeile. Bewahren Sie vor Änderungen wichtiger Dateien eine separate Kopie auf. Lokales Löschen fragt nach einer Bestätigung und verschiebt Dateien und Ordner in den Windows-Papierkorb.

## Öffnen oder Pfad kopieren

Ein Doppelklick öffnet einen Ordner. Wählen Sie für eine Datei **Öffnen**: Unterstützte Dokumente öffnen sich in CryptoBook, andere vorgesehene Aktionen können eine externe Anwendung nutzen. **Öffnen mit** ruft für eine lokale Datei die Windows-Programmauswahl auf. **Im Datei-Explorer anzeigen** zeigt ein lokales Objekt im Windows-Datei-Explorer. Diese Aktionen benötigen ein geeignetes lokales Objekt und Windows-Unterstützung.

**Pfad kopieren** kopiert ausgewählte Pfade zeilenweise in die Windows-Zwischenablage, keine Dateiinhalte. Prüfen Sie Auswahl sowie lokale oder Geräteadresse, bevor Sie den Text weiterverwenden.

## Ein Objekt umbenennen

1. Wählen Sie genau eine Datei oder einen Ordner und **Umbenennen**.
2. Bearbeiten Sie den Namen in der Zeile. Ohne neue Erweiterung bleibt die bisherige automatisch erhalten. Eine ausdrücklich eingegebene Erweiterung ersetzt die alte. Zum gezielten Entfernen verwenden Sie das Mehrfach-Umbenennen mit leerer Erweiterungsmaske.

![Schritt 2. Vollständigen neuen Dateinamen in der Zeile bearbeiten](images/S21-a.png "Schritt 2. Vollständigen neuen Dateinamen in der Zeile bearbeiten")

3. Enter wendet die Änderung an. Esc fragt nach der Bestätigung zum Verwerfen der Bearbeitung; bestätigen Sie den Abbruch, um den alten Namen wiederherzustellen. Bei einem Fehler wird der bisherige Name mit einer Meldung wiederhergestellt.

Aktualisieren Sie anschließend die Liste und prüfen Sie den vollständigen Pfad. Einzelnes Umbenennen aktualisiert den passenden Pfad des aktiven Dokuments und angeheftete sowie zuletzt verwendete Verweise. Eine separate Warnung kann auf fehlgeschlagene Verweisaktualisierung hinweisen. Ordnerbäume und externe Verknüpfungen verfolgen Dokumente nicht allgemein. Für mindestens zwei lokale Dateien gibt es das Mehrfach-Umbenennen.

## Löschen

Markieren Sie lokale Objekte, wählen Sie **Löschen** und bestätigen Sie die Auswahl. **Abbrechen** im Bestätigungsdialog lässt Dateien unverändert. Objekte werden unter ihren ursprünglichen Namen in den Windows-Papierkorb verschoben; daneben werden keine Ordner für gelöschte Dateien angelegt. Ist das Verschieben in den Papierkorb nicht möglich, erscheint ein Fehler. Die App löscht die Objekte dann nicht endgültig.

Öffnen Sie zum Zurückholen den Windows-**Papierkorb**, markieren Sie die Objekte und wählen Sie **Wiederherstellen**. Sie kehren in ihre ursprünglichen Ordner zurück. Bei Abbruch eines laufenden Stapels oder einem Fehler können einige Objekte bereits im Papierkorb liegen, während andere unverändert bleiben. Bereits verschobene Objekte stellen Sie im Papierkorb wieder her. Nach dem Leeren des Papierkorbs oder endgültigem Löschen daraus entfällt diese normale Wiederherstellung.

Für Android und andere entfernte Quellen bleibt die separate Warnung vor endgültigem Löschen bestehen. Bei gemischter Auswahl gelangen lokale Objekte in den Papierkorb; entfernte folgen den Geräteregeln.

## Unterbrochene Vorgänge wiederherstellen

Die Schaltfläche **Vorgänge wiederherstellen** ist nur verfügbar, wenn der geöffnete Ordner ein ausstehendes Wiederherstellungsprotokoll enthält. Während der Wiederherstellung und nach erfolgreichem Abschluss ist sie deaktiviert. Protokolle werden beim Ordnerwechsel, beim Aktualisieren und nach Dateioperationen geprüft. Öffnen Sie für eine unterbrochene Übertragung den Zielordner, für Mehrfach-Umbenennen den Ordner der ersten Quelldatei. Diese Schaltfläche stellt keine Objekte aus dem Windows-Papierkorb wieder her.

Objekte, die eine frühere Version in einem benachbarten Wiederherstellungsordner aufbewahrt hat, lassen sich weiterhin über diese Schaltfläche im Ordner mit dem alten Protokoll zurückholen.

## Verwandte Themen

- [Mehrere Dateien umbenennen](help:files.batch)
- [Möglichkeiten und Grenzen der Quellen](help:devices.limits)
- [Zwei Editoren, Umbenennen und Schließen](help:doc.switch)
