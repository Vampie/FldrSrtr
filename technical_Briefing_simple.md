# Technical Project Brief — Windows Automated File Manager

## Doel

Bouw een moderne Windows-only desktopapplicatie, geïnspireerd op Belvedere/Hazel, waarmee gebruikers bestanden automatisch kunnen beheren op basis van configureerbare regels.

De applicatie monitort één of meerdere mappen en voert acties uit wanneer bestanden aan bepaalde voorwaarden voldoen.

**Core concept:**

```text
Folder
  → File discovery
  → Rule evaluation
  → Conditions
  → Safety validation
  → Action execution
  → Logging
```

---

# Platform

* Windows 10/11
* Desktop GUI
* Lokale applicatie
* Geen cloud/backend
* Ondersteuning voor lokale drives en UNC/network paths

---

# Functionaliteit

## 1. Folder management

Gebruiker kan:

* folders toevoegen/verwijderen/bewerken
* folders enable/disable
* folder openen in Explorer
* folder onmiddellijk scannen
* regels per folder beheren
* recursive scanning in-/uitschakelen
* exclusions configureren

---

## 2. Rule engine

Elke folder kan meerdere regels hebben.

Een rule bevat:

* naam
* enabled/disabled
* prioriteit
* conditions
* condition logic
* één of meerdere actions

Rules moeten kunnen worden:

* toegevoegd
* bewerkt
* verwijderd
* gedupliceerd
* getest
* handmatig uitgevoerd
* geordend

De rule engine moet losstaan van de GUI.

---

# 3. Conditions

Ondersteun:

### Filename

* equals
* contains
* starts with
* ends with
* wildcard
* regex
* case-sensitive option

### Extension

* equals
* is one of
* is not one of

### Size

* =, !=, <, >, <=, >=
* Bytes / KB / MB / GB / TB

### Dates

* creation date
* modified date
* accessed date
* older/newer than X
* before/after/between dates

### File properties

* file type
* hidden
* read-only
* system
* archive
* temporary
* compressed
* encrypted

### Other

* full path
* file existence
* duplicate detection
* SHA-256 hash
* extensionless files

---

# 4. Condition logic

Ondersteun:

```text
ALL / AND
ANY / OR
NOT
Nested condition groups
```

Voorbeeld:

```text
ALL
├── Extension = PDF
├── Age > 30 days
└── ANY
    ├── Name contains "invoice"
    └── Name contains "factuur"
```

---

# 5. Actions

Eén rule kan meerdere acties uitvoeren.

Minimaal:

* Move
* Copy
* Rename
* Delete → Recycle Bin
* Permanent delete
* Open
* Open with application
* Execute external program/script
* Create folder
* Add/remove extension
* Move to dynamically generated subfolder
* ZIP/archive
* Windows notification
* Logging

---

# 6. Dynamic variables

Ondersteun variables in filenames en destination paths:

```text
{FileName}
{OriginalName}
{Extension}
{OriginalExtension}
{FullPath}
{Directory}
{FileSize}
{Year}
{Month}
{Day}
{Hour}
{Minute}
{Second}
{Date}
{Time}
```

Voorbeeld:

```text
D:\Archive\{Year}\{Month}\{FileName}.{Extension}
```

---


# 8. Dry Run / Preview

Elke rule moet kunnen worden getest zonder bestanden te wijzigen.

Toon:

* matching files
* waarom ze matchen
* huidige locatie
* geplande actie
* doel locatie
* eventuele conflicts

Voorbeeld:

```text
invoice.pdf

MATCH:
Extension = PDF
Age > 30 days

ACTION:
MOVE

FROM:
C:\Downloads\invoice.pdf

TO:
D:\Archive\2026\invoice.pdf
```

Dry-run moet ook voor volledige folders/rulesets mogelijk zijn.

---

# 9. Safety

Dit is een belangrijk onderdeel van de architectuur.

Bescherm standaard:

```text
C:\Windows
C:\Program Files
C:\Program Files (x86)
C:\ProgramData
```

Gebruiker kan extra protected folders/extensions configureren.

Ondersteun:

* maximum files per rule run
* confirmation threshold
* protected extensions
* protected folders
* Recycle Bin als standaard voor delete
* expliciete toestemming voor permanent delete
* geen onverwachte overwrites

---

# 10. Conflict handling

Bij bestaande doelbestanden:

* Skip
* Overwrite
* Automatically rename
* Numeric suffix
* Ask user

Voorbeeld:

```text
document.pdf
document (1).pdf
document (2).pdf
```

---

# 11. Recursive processing

Optioneel verwerken van subdirectories.

Ondersteun:

* include subfolders
* maximum recursion depth
* excluded folders
* excluded patterns

---

# 12. Exclusions

Ondersteun exclusions voor:

* folders
* subfolders
* filenames
* extensions
* wildcard patterns
* regex

Voorbeeld:

```text
*.tmp
*.part
~*
```

---

# 13. Activity / logging

Structured logging.

Log:

* timestamp
* folder
* rule
* file
* action
* result
* error
* original path
* destination path

Statussen:

```text
INFO
SUCCESS
WARNING
ERROR
```

GUI bevat Activity view met:

* zoeken
* filters
* datumfilter
* rule filter
* folder filter
* success/error filter

---

# 14. Statistics

Dashboard met:

* files processed
* moved
* copied
* renamed
* deleted
* errors
* data moved
* actions per rule
* actions per folder

Per:

* today
* 7 days
* 30 days
* all time

---

# 16. Undo / recovery

Voor Move/Rename/Copy:

log:

* original path
* new path
* rule
* action
* timestamp

Ondersteun waar mogelijk:

```text
Undo last action
```

Delete gebruikt bij voorkeur Windows Recycle Bin zodat herstel mogelijk blijft.

---

# 17. Configuration

Configuratie moet persistent en versioned zijn.

Moet bevatten:

```text
Settings
Folders
Rules
Conditions
Actions
Exclusions
```

Import/export:

* volledige configuratie
* individuele folder
* individuele rule

Voorkeur voor JSON of vergelijkbaar transparant formaat.

Ondersteun schema/version migration.

Automatische configuration backups.

---

# 18. GUI

Moderne Windows desktop UI.

Hoofdonderdelen:

```text
Dashboard
Folders
Rules
Activity
Settings
```

Rule editor moet visueel zijn:

```text
Rule name: [ Move old PDFs ]

Conditions:

[ ALL ]

[ Extension ] [ is ] [ .pdf ]
[ Age       ] [ >  ] [ 30 days ]

[ + Add condition ]

Actions:

[ Move ] [ D:\Archive\PDF\ ]

[ + Add action ]

[ Test Rule ] [ Save ]
```

Geen code vereist voor normale rules.

---

# 20. Architecture requirements

Voorkeur voor duidelijke scheiding tussen:

```text
UI
│
├── Application layer
│
├── Rule Engine
│   ├── Condition evaluator
│   ├── Action planner
│   └── Execution engine
│
├── File monitoring
│
├── Safety subsystem
│
├── Configuration
│
├── Logging
│
└── Statistics
```

De core rule engine mag niet afhankelijk zijn van de GUI.

Dry-run en echte execution moeten dezelfde rule evaluation gebruiken.

File operations moeten via een gecontroleerde action queue lopen.

Voorkom dat twee rules hetzelfde bestand tegelijkertijd wijzigen.

---

# 21. Performance

De applicatie moet grote directories aankunnen:

* duizenden bestanden
* grote bestanden
* netwerkshares

Vereisten:

* responsive GUI
* beperkte geheugengebruik
* geen onnodige file reads
* metadata eerst gebruiken
* hashing alleen wanneer nodig
* concurrency gecontroleerd uitvoeren

---

# 22. Reliability

Een fout bij één bestand mag de volledige automation niet stoppen.

Voorbeeld:

```text
100 files matched

97 successful
2 skipped
1 failed
```

Ondersteun:

* retry
* error logging
* recovery
* duidelijke foutmeldingen

Race conditions en verdwenen/verplaatste bestanden moeten correct afgehandeld worden.

---

# 23. Security

De applicatie mag uitsluitend bestanden wijzigen binnen de expliciet geconfigureerde scope.

Geen:

* verborgen uploads
* cloud dependency
* telemetrie zonder expliciete toestemming
* onverwachte code execution
* automatische systeemwijzigingen

External scripts/commands moeten expliciet door de gebruiker geconfigureerd worden.

---

# 24. Testing

Automated tests voor:

* condition evaluation
* AND/OR/NOT
* nested conditions
* regex
* wildcards
* dates
* sizes
* actions
* multiple actions
* conflicts
* locked files
* recursive folders
* exclusions
* dry run
* import/export
* configuration migration
* watcher
* concurrent execution
* error handling

File operation tests moeten uitsluitend tijdelijke testdirectories gebruiken.

---

# 25. Packaging

Het eindresultaat moet kunnen worden geleverd als normale PORABLE Windows-applicatie.
Dus er mogen geen registersettings of settings in userfoldes gebruikt worden.
Alle configuratie in de folder van de applicatie zelf.

Benodigd:

* executable
* GEEN installer
* application icon
* GEEN Start Menu shortcut
* GEEN optional desktop shortcut
* GEEN uninstall support
* configuration/data separation
* build script
* release build

---

# 26. Development strategy

Werk gefaseerd.

### MVP

* Windows GUI
* folder management
* rule engine
* filename/extension/size/age conditions
* move/copy/rename/delete
* dry run
* logging

### V2

* multiple conditions
* nested AND/OR/NOT
* multiple actions
* recursive folders
* exclusions
* conflicts
* filesystem watcher
* background operation

### V3

* dynamic variables
* regex
* duplicate detection
* advanced actions
* tray
* notifications
* startup

### V4

* import/export
* backups
* statistics
* undo/recovery
* advanced settings
* performance optimization
* installer

---

# 27. Belangrijk: kies eerst de technologie

**Voordat je code schrijft, analyseer de technische vereisten en vergelijk geschikte technologieën.**

Vergelijk minimaal:

* C# / .NET + WinUI 3
* C# / .NET + WPF
* C++ + WinUI/Windows API
* Rust + geschikte Windows GUI framework
* Python + Windows GUI framework
* eventueel andere geschikte technologie

Beoordeel iedere optie op:

| Eigenschap                 |    Belang |
| -------------------------- | --------: |
| Windows filesystem API's   | Zeer hoog |
| Windows notifications      |      Hoog |
| Performance                |      Hoog |
| Reliability                | Zeer hoog |
| Async/concurrency          |      Hoog |
| Moderne Windows UI         |      Hoog |
| Long-term maintainability  | Zeer hoog |
| Development speed          |    Medium |
| Memory usage               |      Hoog |
| Native Windows integration | Zeer hoog |

Geef vervolgens:

1. Een vergelijking van de beste opties.
2. Een duidelijke aanbeveling.
3. De reden waarom die technologie het beste past.
4. Welke GUI framework/library gebruikt moet worden.
5. Welke persistence/configuration technologie gebruikt moet worden.
6. Welke testing framework gebruikt moet worden.
7. Welke installer technologie gebruikt moet worden.
8. Een voorgestelde projectstructuur.
9. Eventuele belangrijke technische risico's.

**Begin nog niet met implementeren.**

Wacht eerst tot de technische stack is bepaald.

Na goedkeuring van de stack kan de implementatie gefaseerd beginnen.
