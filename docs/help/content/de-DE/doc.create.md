# Ein Dokument erstellen und speichern

Erstellen Sie eine leere RTF-Datei, geben Sie eine kurze Zeile ein und speichern Sie die Änderungen. RTF eignet sich für ein gewöhnliches Dokument mit Textformatierung.

## Vor dem Beginn

Verwenden Sie einen lokalen Ordner, in dem Sie Dateien erstellen können, und einen noch nicht vergebenen Dateinamen. In diesem Beispiel entsteht **Reiseplan.rtf**. Speichern Sie vorher die Änderungen an einem bereits geöffneten Dokument. Eine RTF-Datei ist nicht durch ein Passwort geschützt.

## Vorgehen

1. Wählen Sie auf dem Startbildschirm **Neues Dokument**. Wenn bereits ein Dokument geöffnet ist, verwenden Sie stattdessen die Schnellaktion **Erstellen** im Seitenmenü.
2. Wählen Sie unter **Ordner:** über **Durchsuchen...** Ihren Beispielordner aus.
3. Wählen Sie unter **Dateityp:** das **Rich-Text-Format** und prüfen Sie, ob unter **Dateierweiterung:** **.rtf** ausgewählt ist.
4. Geben Sie unter **Dateiname:** **Reiseplan.rtf** ein. Tragen Sie den Namen nach der Wahl des Dateityps ein, da ein Typwechsel den vorgeschlagenen Namen ersetzt.
5. Wählen Sie unter **Dokumentvorlage** die Vorlage **Leeres Dokument**.
6. Stellen Sie **Papierformat** auf **A4** und **Ausrichtung** auf **Hochformat**. Lassen Sie **Schreibgeschützt** und **Versteckt** für dieses Beispiel deaktiviert.

![Dialog für eine neue Datei mit Ordner, RTF-Typ, Dateinamen, leerer Vorlage und Seiteneinstellungen](images/S04-a.png "Schritte 2–6. Einstellungen für Reiseplan.rtf.")

7. Wählen Sie **Erstellen**. Warten Sie, bis die neue Datei im gewöhnlichen Editor geöffnet wird, und prüfen Sie ihren Namen in der oberen Leiste.

![Neu erstellter Reiseplan als leere Seite im Editor](images/S04-b.png "Schritt 7. Die Datei wurde erstellt und geöffnet.")

8. Klicken Sie auf die Seite und geben Sie **Abfahrt um 09:00 Uhr.** ein.
9. Öffnen Sie das Seitenmenü und wählen Sie im Bereich **Datei** den Befehl **Speichern**. Alternativ drücken Sie **Ctrl+S**, während der Editor den Fokus hat.

![Dateibereich des Seitenmenüs mit dem Befehl Speichern](images/S06-a.png "Schritt 9. Änderungen an der geöffneten Datei speichern.")

## Ergebnis prüfen

Öffnen Sie dieselbe RTF-Datei erneut über die Schnellaktion **Öffnen** im Seitenmenü. Die Zeile **Abfahrt um 09:00 Uhr.** sollte weiterhin auf der Seite stehen. Prüfen Sie den tatsächlichen Dokumentnamen, falls die Anwendung wegen einer Namenskollision einen anderen freien Namen gewählt hat.

![Erneut geöffnete Datei Reiseplan.rtf mit der gespeicherten Abfahrtszeile](images/S06-b.png "Ergebnis. Der Text ist nach dem erneuten Öffnen weiterhin vorhanden.")

## Abbrechen und zurückkehren

Bevor Sie **Erstellen** wählen, können Sie den Dialog mit **Abbrechen** verlassen. Nach dem Erstellen löscht das Schließen des Dokuments die Datei nicht. Für einen anderen Namen oder Speicherort verwenden Sie **Datei → Speichern unter**; ein Wechsel des Dateityps kann beeinflussen, welche Formatierung erhalten bleibt.

## Wenn etwas nicht funktioniert

Wenn **Erstellen** nicht verfügbar ist, prüfen Sie die Auswahl von Ordner, Dateityp und Namen sowie die Schreibberechtigung für den Ordner. Lesen Sie eine Fehlermeldung und korrigieren Sie den Namen oder wählen Sie einen anderen Ordner. Bei **Die Datei wurde erstellt, konnte aber nicht geöffnet werden.** prüfen Sie zuerst den Ordner: Die Datei kann bereits vorhanden sein. **Speichern** kann deaktiviert sein, wenn es keine neuen Änderungen gibt oder das gewöhnliche Dokument im Lesemodus geöffnet ist.

## Andere Typen, Vorlagen und Attribute

Dieses Beispiel verwendet eine leere RTF-Datei. Wählen Sie für vorbereiteten Text eine andere **Dokumentvorlage**. Wählen Sie für Markdown dessen Dateityp: Ein eigener Editor öffnet sich, die Papiereinstellungen sind ausgeblendet. Texttypen einschließlich XAML behalten keine Formatierung bei.

**Schreibgeschützt** und **Versteckt** setzen Attribute der neuen lokalen Datei. Sie verschlüsseln sie nicht und schützen sie nicht durch ein Passwort. Lassen Sie beide für normale Arbeit deaktiviert. Andere Speicherquellen können abweichende Möglichkeiten haben.

Ist ein Name belegt, wählt die Anwendung einen freien Namen, der von Ihrer Eingabe abweichen kann. Geben Sie nur den Dateinamen ein und wählen Sie den Ordner gesondert. Prüfen Sie nach der Erstellung immer den Namen oben und die tatsächliche Datei im Ordner.

Wird der ausgewählte Ordner nicht mehr verfügbar, kann der Dialog mit einer Fehlermeldung geöffnet bleiben. Wählen Sie einen verfügbaren Ordner und wiederholen Sie die Erstellung. Prüfen Sie vorher, ob eine Datei aus dem letzten Versuch bereits vorhanden ist. **Abbrechen** löscht keine bereits geschriebene Datei.

![Unterer Bereich des Erstellungsdialogs mit Schreibfehler in einem nicht verfügbaren Ordner](images/S04-d.png "Erstellungsfehler. Prüfen Sie vor einem erneuten Versuch Ordner und bereits erstellte Dateien.")

Schließen Sie zum Prüfen der Speicherung zuerst die gespeicherte Beispieldatei und öffnen Sie sie erneut: Die erneute Auswahl einer bereits aktiven Datei kann lediglich zu ihrem Editor zurückkehren.

## Verwandte Themen

- [Die wichtigsten Bereiche des Fensters](help:start.interface)
- [Speichern und Speichern unter](help:doc.save)
- [Dokumentvorlage auswählen](help:doc.templates)
- [Formate und Kodierungen](help:doc.formats)
