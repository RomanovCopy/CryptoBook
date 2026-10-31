# Android verbinden

Android lässt sich als zusätzliche Dateiquelle verwenden. Normale Verbindungen nutzen die MTP-Dateiübertragung von Windows; Entwicklermodus und ADB sind dafür nicht nötig.

## Vorbereitung

Sie benötigen ein Datenkabel, ein entsperrtes Gerät und Zugriff auf den gemeinsamen Speicher. Sichtbare Ordner hängen von Android und Berechtigungen ab; geschützte App-Verzeichnisse werden nicht automatisch zugänglich.

## Vorgehen

1. Verbinden und entsperren Sie das Gerät und wählen Sie den USB-Modus zur Dateiübertragung. Die Android-Bezeichnungen unterscheiden sich nach Hersteller.
2. Öffnen Sie den CryptoBook-Dateimanager. Wählen Sie im Quellenbaum das Gerät, den verfügbaren gemeinsamen Speicher und einen Ordner.
3. Prüfen Sie Liste und verfügbare Befehle mit einer entbehrlichen Testdatei. Beginnen Sie mit Kopieren und behalten Sie das Original.
4. Beenden Sie nach Abschluss der Vorgänge den Zugriff und trennen Sie das Gerät. Wählen Sie nach erneutem Verbinden Quelle und Ordner neu.

## Ergebnis prüfen

Gerätequelle und erreichbare Ordner erscheinen im Baum. Dies ist eine Dateiquelle, kein lokaler Such-Arbeitsordner. Nicht alle Befehle lokaler Datenträger stehen zur Verfügung.

## Abbrechen und zurückkehren

Brechen Sie über das Fortschrittsfenster ab und warten Sie auf das Ende. Das Herausziehen des Kabels ist kein Abbruchverfahren.

## Bei Problemen

Keine Quelle: Prüfen Sie Entsperrung, USB-Modus, Kabel und Sichtbarkeit in Windows und verbinden Sie erneut. ADB ist ein eigener optionaler Weg mit Debugging-Einrichtung und Computerfreigabe; offline/unauthorized bedeutet, dass der Zugriff noch nicht bereit ist.

## Verwandte Themen

- [Dateien zwischen PC und Android übertragen](help:devices.transfers)
- [Möglichkeiten und Grenzen von Geräten](help:devices.limits)
