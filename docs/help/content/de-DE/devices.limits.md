# Möglichkeiten und Grenzen von Geräten

Befehle hängen von Quelle, Objekt und Rechten ab. Android bietet zusätzlichen Dateispeicher, aber nicht jede Funktion eines lokalen Windows-Datenträgers.

## Vorbereitung

> Löschen auf Android ist aus Sicht von CryptoBook dauerhaft. Lokale Vorgangswiederherstellung und Windows-Papierkorb können gelöschte Geräteobjekte nicht zurückholen. Entschlüsselte Ergebnisse werden ungeschützt gespeichert.

| Aktion | Lokaler Ordner | Android/MTP |
| --- | --- | --- |
| Navigation, Kopieren, Verschieben, Ordner erstellen, Umbenennen | Nach Ordnerrechten | Nach Geräte- und Objektrechten |
| Löschen im Dateimanager | Bestätigter Windows-Papierkorb | Bestätigtes dauerhaftes Löschen |
| Arbeitsordnersuche, Überwachung, Stapelumbenennung | Nach eigenen Bedingungen | Nicht als lokale Befehle unterstützt |
| Extern öffnen, vollständige Eigenschaften und Hashes | Nach eigenen Bedingungen | Zuerst lokal kopieren |
| Text-/Bildvorschau | Unterstützte Dateien | Datenstrom ohne automatische Entschlüsselung |

## Vorgehen

1. Kopieren Sie die Gerätedatei für vollständige Anzeige oder genaue Eigenschaften in einen lokalen Ordner.
2. Zum Entschlüsseln direkt auf dem Gerät geben Sie den passenden Schlüssel ein, wählen eine lesbare geschützte Datei und **Ausgewählte Elemente entschlüsseln**.
3. Wählen Sie unter **Dokument entschlüsseln** ein verfügbares Format und **Geschützte Quelldatei durch die entschlüsselte Datei ersetzen** für die Ausgabe auf dem Gerät. Schreib- und Löschrechte sind nötig; lesen Sie die Warnung vor der Bestätigung.
4. Um das geschützte Original zu behalten, wählen Sie eine entschlüsselte Kopie und prüfen deren Ziel ausdrücklich.

## Ergebnis prüfen

Nach erfolgreichem Ersetzen liegt das Ergebnis im Geräteordner. Name und Erweiterung können abweichen. Das Entschlüsseln lesbarer entfernter Dateien benötigt keine gesonderte Verschlüsselungsfähigkeit.

## Abbrechen und zurückkehren

Schließen Sie die Optionen vor der Bestätigung zum Abbrechen. Verlassen Sie sich nach dem Schreiben nicht auf allgemeines Rückgängig; behalten Sie bei Bedarf eine geschützte Kopie.

## Bei Problemen

Ein gesperrter Befehl bedeutet meist ein ungeeignetes Objekt, einen fehlenden Schlüssel oder Rechte. Beschreibbare Android-Dateien können je nach Rechten Verschlüsselung/geschützte Kopien erlauben; daraus folgt keine rekursive Verschlüsselung entfernter Ordner. Der vollständige Schutzablauf wird separat behandelt.

## Verwandte Themen

- [Ausführliche Eigenschaften und Prüfsummen](help:files.properties)
- [Dateien entschlüsseln](help:security.decrypt)
