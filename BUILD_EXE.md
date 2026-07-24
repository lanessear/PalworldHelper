# Portable Windows-EXE erzeugen

Hierfür muss lokal kein Python installiert sein. Der Build läuft kostenlos auf einem Windows-Runner von GitHub Actions.

1. Einen neuen privaten GitHub-Repository erstellen.
2. Den gesamten Inhalt dieses Ordners hochladen.
3. Im Repository oben **Actions** öffnen.
4. Workflow **Build Windows EXE** auswählen.
5. **Run workflow** anklicken.
6. Nach erfolgreichem Lauf unten das Artefakt **PalworldBreedingAssistant-Windows** herunterladen.
7. ZIP entpacken und `PalworldBreedingAssistant.exe` starten.

Die EXE öffnet automatisch `http://127.0.0.1:8765` im Standardbrowser.

## Sicherheit

Der Webserver lauscht ausschließlich auf `127.0.0.1`. SSH-Zugangsdaten werden nicht dauerhaft gespeichert. Da die EXE nicht digital signiert ist, kann Windows SmartScreen beim ersten Start warnen.
