PalworldHelper Default Data Replacement

1. Replace all files part01.txt through part15.txt in:
   src/PalworldHelper/Embedded/BreedingData/

2. Delete:
   src/PalworldHelper/Embedded/BreedingData/part09-tail.txt

3. Do not append any tail after part09.txt.

4. The workflow must expect:
   Base64 length: 215048
   GZIP SHA-256: ae292cc8f8b0db54e5c4462507e39274654f78f13c613c23d9b23c910f439934
   JSON SHA-256: 51ceacd65d7e484738d95f662362591888a30b50c756d7285a660c06ed4ac74f
   Pals: 298
   Results: 44253

Run Verify-DefaultData.ps1 before committing.
