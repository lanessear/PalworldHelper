# PalworldHelper Quickstart v0.2

**THIS PROJECT IS MANAGED BY AI.**

Eine bewusst kleine, native Windows-Desktop-App ohne Browser. Die Anwendung wird als eigenständige Windows-EXE gebaut; auf dem Zielrechner muss weder Python noch das .NET SDK installiert sein.

## Bereits funktionsfähig

- beliebig viele Serverprofile
- Anmeldung per Passwort oder SSH-Key
- SFTP-Verbindung testen
- `Level.sav` automatisch auf dem Palworld-Server suchen
- alternativ einen frei einstellbaren Remote-Pfad verwenden
- `Level.sav` per SFTP herunterladen
- Zugangspasswort mit Windows DPAPI für den aktuellen Windows-Benutzer verschlüsseln
- vorhandene `palworld_breeding_results.json` laden
- Start- und Ziel-Pal über durchsuchbare Dropdown-Menüs auswählen
- kürzeste fortlaufende Zuchtkette per Breitensuche berechnen

## Nächster Entwicklungsschritt

Die heruntergeladene `Level.sav` wird noch nicht direkt in Spieler, vorhandene Pals und passive Fähigkeiten zerlegt. Der Download und die automatische Save-Suche sind bereits eingebaut, damit als Nächstes der Import eines echten Server-Saves umgesetzt und getestet werden kann.

## Windows-EXE herunterladen

Bei jedem Commit auf `main` baut GitHub Actions automatisch eine neue self-contained Windows-Version:

1. Auf GitHub **Actions → Build Windows EXE** öffnen.
2. Den neuesten erfolgreichen Lauf auswählen.
3. Unten das Artefakt `PalworldHelper-win-x64` herunterladen.
4. ZIP entpacken und `PalworldHelper.exe` starten.

Alternativ kann der Workflow über **Run workflow** manuell gestartet werden.

## Zuchtdaten

Beim ersten Start über **Zuchtkette → JSON auswählen** deine Datei `palworld_breeding_results.json` auswählen. Alternativ kann sie direkt neben `PalworldHelper.exe` liegen.

## Lokale Entwicklung

Mit installiertem .NET-8-SDK:

```powershell
.\build-windows.ps1
```

Die fertige EXE liegt anschließend unter `publish\PalworldHelper.exe`.
