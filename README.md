# PalworldHelper Quickstart v0.1

**THIS PROJECT IS MANAGED BY AI.**

Eine bewusst kleine, native Windows-Desktop-App ohne Browser.

## Bereits funktionsfähig

- beliebig viele Serverprofile
- Passwort oder SSH-Key
- SFTP-Verbindung testen
- `Level.sav` vom frei einstellbaren Serverpfad herunterladen
- Zugangspasswort mit Windows DPAPI für den aktuellen Windows-Benutzer verschlüsseln
- vorhandene `palworld_breeding_results.json` laden
- kürzeste fortlaufende Zuchtkette per Breitensuche berechnen

## Noch nicht enthalten

Die heruntergeladene `Level.sav` wird noch nicht in Spieler und Pals zerlegt. Der Download ist bereits eingebaut, damit als nächster Schritt direkt mit einem echten Save gearbeitet werden kann.

## EXE über GitHub erzeugen

1. Repository-Inhalt hochladen/ersetzen und committen.
2. Auf GitHub **Actions → Build Windows EXE** öffnen.
3. Den automatisch gestarteten Lauf abwarten oder **Run workflow** drücken.
4. Unten das Artefakt `PalworldHelper-win-x64` herunterladen.
5. ZIP entpacken und `PalworldHelper.exe` starten.

Die EXE ist self-contained; .NET muss auf dem Zielrechner nicht installiert sein.

## Zuchtdaten

Beim ersten Start über **Zuchtkette → JSON auswählen** deine Datei `palworld_breeding_results.json` auswählen. Alternativ kann sie direkt neben `PalworldHelper.exe` liegen.

## Lokale Entwicklung

Mit installiertem .NET-8-SDK:

```powershell
.\build-windows.ps1
```
