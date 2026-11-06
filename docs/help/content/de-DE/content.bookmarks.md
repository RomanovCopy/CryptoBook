# Lesezeichen, Notizen und interne Links

Ein Lesezeichen fügt eigenen sichtbaren Text aus dem eingegebenen Namen in ein normales Dokument ein. Speichern Sie als XamlPackage oder als geschütztes CryptoBook-Dokument, damit Text, Positionen, Namen, Notizen und interne Links beim erneuten Öffnen erhalten bleiben. Lesezeichen, die beim Speichern in früheren Versionen verloren gingen, werden nicht automatisch wiederhergestellt.

## Schritte

1. Heben Sie die Textauswahl auf, setzen Sie die Einfügemarke und öffnen Sie **Lesezeichen** über dem Dokument. Geben Sie einen Namen ein und klicken Sie **An Cursorposition hinzufügen**: Der Name wird als eigener Text des neuen Lesezeichens eingefügt. Bei aktiver Textauswahl ist das Erstellen nicht verfügbar; aus ausgewähltem Text kann kein Lesezeichen erstellt werden.
2. Wählen Sie einen Eintrag. **Zum ausgewählten Lesezeichen wechseln**, **Vorheriges Lesezeichen** und **Nächstes Lesezeichen** zentrieren die Lesezeichenzeile vertikal im sichtbaren Editorbereich und heben sie kurz hervor. Die Dokumentränder begrenzen das Scrollen. Die Nachbarnavigation läuft am Dokumentende wieder zum Anfang; interne Links zentrieren und markieren ihr Lesezeichenziel ebenfalls.
3. Geben Sie einen neuen Namen ein und klicken Sie **Ausgewähltes Lesezeichen umbenennen**. Für einen Link heben Sie die Textauswahl auf, setzen die Einfügemarke anderswo, geben den eigenen Text des Links im Linktextfeld ein und klicken **Link zum ausgewählten Lesezeichen einfügen**. Der Linktext wird als neuer Text in das Dokument eingefügt. Bei aktiver Textauswahl oder leerem Linktext ist das Einfügen nicht verfügbar; aus ausgewähltem Text kann kein Lesezeichenlink erstellt werden.
4. Öffnen Sie **Lesezeichen-Manager öffnen**, wählen Sie einen Eintrag und ändern Sie seine Notiz. **Index aus Dokument aktualisieren** liest die Anker des aktuellen Dokuments neu ein.

In der Seitenvorschau öffnen diese Befehle und internen Links die Seite mit dem Lesezeichen, ohne den Vorschaumodus zu verlassen, und heben die Zielzeile für 2,5 Sekunden hervor. Die Hervorhebung ändert die Dokumentformatierung nicht. Lesezeichen und Links können nur im Editor eingefügt werden, auch im separaten Manager; Name und Linktext bleiben beim Moduswechsel erhalten. Interne Links und die Navigation zwischen benachbarten Lesezeichen aktualisieren in beiden Modi den ausgewählten Eintrag im Menü „Lesezeichen“.

![Lesezeichenbereich mit Namen, Navigation und Linkbefehlen](images/S15-a.png "Lesezeichenbereich mit Namen, Navigation und Linkbefehlen.")

![Lesezeichen-Manager mit bearbeitbarer Übungsnotiz](images/S15-b.png "Lesezeichen-Manager mit bearbeitbarer Übungsnotiz.")

## Prüfung, Rückgängigmachen und Grenzen

Prüfen Sie Name und Ziel. **Ausgewähltes Lesezeichen löschen** löscht das gesamte Lesezeichen mit seinem Text und allen Links darauf einschließlich der Linktexte. Dies gilt auch für Lesezeichen in älteren Dokumenten, die aus ausgewähltem Text erstellt wurden. Lesezeichen innerhalb des gelöschten Textes und Links darauf werden ebenfalls gelöscht; andere Lesezeichen und externe Links außerhalb des gelöschten Textes bleiben erhalten. Strg+Z macht den gesamten Löschvorgang in einem Schritt rückgängig. Korrigieren Sie leere oder doppelte Namen gemäß Panelmeldung. Das Schließen des Managers verwirft vorgenommene Änderungen nicht; speichern Sie ausdrücklich. Aktualisieren Sie den Index, wenn Textverschiebungen oder Löschungen ein Lesezeichen entfernen. TXT, XAML und RTF garantieren den Erhalt von Ankern und Notizen nicht.

## Verwandte Themen

- [Weblinks und sichtbarer Text](help:edit.links)
- [Der Dokumentstrukturbaum](help:content.structure)
