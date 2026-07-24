# Lanessears Palworld-Zuchtassistent

Die Browseroberfläche verbindet deine eigene Zuchtdaten-JSON mit den Pals aus der `Level.sav`.

## Windows

1. ZIP entpacken.
2. `start_windows.bat` doppelklicken.
3. Beim ersten Start werden Python-Abhängigkeiten installiert.
4. Browser öffnet `http://127.0.0.1:8765`.
5. Deine vorhandene `palworld_breeding_results.json` oben einlesen.
6. SSH-Daten und den vollständigen Pfad zur `Level.sav` eintragen.
7. **Serverdaten abfragen und einlesen** drücken.
8. Beispielsweise eingeben: `Ich will ein Jolthog Cryst mit Artisan`.

Python 3.9+ muss installiert sein. Unter Windows sollte der `py`-Launcher verfügbar sein.

## Linux

```bash
./start_linux.sh
```

## SSH oder lokal

- **SSH:** Das Tool läuft auf deinem PC, lädt die `Level.sav` per SFTP in einen temporären Ordner und verarbeitet sie dort. Passwort wird nicht gespeichert.
- **Lokal:** Das Tool läuft direkt auf dem Palworld-Server oder auf einem Rechner, der den Save-Ordner gemountet hat.

Typischer Linux-Pfad:

```text
/opt/palworld/Pal/Saved/SaveGames/0/<WELT-ID>/Level.sav
```

## Suchlogik

- Startpunkte sind deine vorhandenen Pals, die den gewünschten Passivskill besitzen.
- Der Skill wird entlang der Zuchtkette als vererbbar angenommen.
- Vorhandene Zuchtpartner werden bevorzugt; fehlende Partner werden gelb markiert.
- Die tatsächliche Skill-Vererbung im Spiel ist zufallsabhängig.
- Es werden maximal sieben Zuchtschritte betrachtet.

## Namens- und Skill-Zuordnung

Palworld-Saves können interne IDs statt Anzeigenamen enthalten. Diese Dateien sind editierbar:

- `pal_name_aliases.json`
- `passive_skill_aliases.json`

Für `Artisan` sind bereits die häufigen internen Bezeichnungen `CraftSpeed_up3` und `WorkSpeed_up3` hinterlegt.

## Sicherheit

Der Dienst bindet absichtlich nur an `127.0.0.1`. Öffne Port 8765 nicht in der Firewall und ändere die Bind-Adresse nicht auf `0.0.0.0`, solange SSH-Zugangsdaten über das Formular verwendet werden.

Die Save-Datei wird nur gelesen. Für einen konsistenten Stand sollte eine Backup-Kopie oder ein Snapshot zwischen zwei Autosaves verwendet werden.
