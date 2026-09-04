# FldrSrtr — Projectbrief & instructies voor Claude Code

Dit document is het volledige, zelfstandige startpunt voor dit project. Het bevat alles wat eerder is uitgewerkt: de functionele specificatie, de technologiebeslissing (met onderbouwing en overwogen alternatieven), de architectuur, en het gefaseerde implementatieplan. Een nieuwe Claude Code-sessie die dit bestand leest heeft alle context nodig om te starten — er hoeft niets uit een eerdere chat opgezocht te worden.

**Belangrijk:** er is nog geen code geschreven. De technologiekeuze is goedgekeurd (zie hieronder); de implementatie begint bij "Fase 0" onderaan dit document.

---

## Project-identiteit

| | |
|---|---|
| **Naam** | FldrSrtr |
| **Lokaal pad** | `C:\claude_code\FldrSrtr` |
| **Icoon** | `fldrsrtr.png` (in het lokale pad hierboven) |
| **Git remote** | `https://github.com/Vampie/FldrSrtr.git` |
| **Eigenaar** | Axel C. — Trustteam Group |

---

## 1. Doel

Bouw een moderne Windows-only desktopapplicatie, geïnspireerd op Belvedere/Hazel, waarmee gebruikers bestanden automatisch kunnen beheren op basis van configureerbare regels.

De applicatie is **portable en handmatig te bedienen**: geen achtergronddienst, geen filesystem-monitor, geen installer. De gebruiker opent de tool, configureert mappen en regels, en voert scans/regels **handmatig** uit (of via "scan nu"/"run rule"-knoppen).

**Core concept:**
```
Folder → File discovery → Rule evaluation → Conditions → Safety validation → Action execution → Logging
```

---

## 2. Platform

- Windows 10/11
- Desktop GUI
- Lokale applicatie, geen cloud/backend
- Ondersteuning voor lokale drives en UNC/network paths
- **Portable**: geen installer, geen registry-settings, geen settings in userfolders — alle configuratie in de map van de applicatie zelf
- **Geen achtergrondwerking en geen filesystem-monitor** — alles draait on-demand, getriggerd door de gebruiker

---

## 3. Functionaliteit

### 3.1 Folder management
Gebruiker kan:
- folders toevoegen/verwijderen/bewerken
- folders enable/disable
- folder openen in Explorer
- folder onmiddellijk scannen
- regels per folder beheren
- recursive scanning in-/uitschakelen
- exclusions configureren

### 3.2 Rule engine
Elke folder kan meerdere regels hebben. Een rule bevat: naam, enabled/disabled, prioriteit, conditions, condition logic, één of meerdere actions.

Rules moeten kunnen worden: toegevoegd, bewerkt, verwijderd, gedupliceerd, getest, handmatig uitgevoerd, geordend.

**De rule engine moet losstaan van de GUI** (harde architectuureis, zie §5).

### 3.3 Conditions
- **Filename:** equals, contains, starts with, ends with, wildcard, regex, case-sensitive option
- **Extension:** equals, is one of, is not one of
- **Size:** =, !=, <, >, <=, >= — bytes/KB/MB/GB/TB
- **Dates:** creation/modified/accessed date, older/newer than X, before/after/between dates
- **File properties:** file type, hidden, read-only, system, archive, temporary, compressed, encrypted
- **Other:** full path, file existence, duplicate detection, SHA-256 hash, extensionless files

### 3.4 Condition logic
`ALL/AND`, `ANY/OR`, `NOT`, geneste condition groups.

Voorbeeld:
```
ALL
├── Extension = PDF
├── Age > 30 days
└── ANY
    ├── Name contains "invoice"
    └── Name contains "factuur"
```

### 3.5 Actions
Eén rule kan meerdere acties uitvoeren. Minimaal: Move, Copy, Rename, Delete → Recycle Bin, Permanent delete, Open, Open with application, Execute external program/script, Create folder, Add/remove extension, Move naar dynamisch gegenereerde subfolder, ZIP/archive, Windows notification, Logging.

### 3.6 Dynamic variables
In filenames en destination paths: `{FileName}`, `{OriginalName}`, `{Extension}`, `{OriginalExtension}`, `{FullPath}`, `{Directory}`, `{FileSize}`, `{Year}`, `{Month}`, `{Day}`, `{Hour}`, `{Minute}`, `{Second}`, `{Date}`, `{Time}`.

Voorbeeld: `D:\Archive\{Year}\{Month}\{FileName}.{Extension}`

### 3.7 Dry Run / Preview
Elke rule moet getest kunnen worden zonder bestanden te wijzigen. Toon: matching files, waarom ze matchen, huidige locatie, geplande actie, doel locatie, eventuele conflicts. Ook mogelijk voor volledige folders/rulesets.

Voorbeeld:
```
invoice.pdf
MATCH: Extension = PDF, Age > 30 days
ACTION: MOVE
FROM: C:\Downloads\invoice.pdf
TO:   D:\Archive\2026\invoice.pdf
```

### 3.8 Safety
Belangrijk onderdeel van de architectuur. Standaard beschermd:
```
C:\Windows
C:\Program Files
C:\Program Files (x86)
C:\ProgramData
```
Gebruiker kan extra protected folders/extensions configureren. Ondersteun: maximum files per rule run, confirmation threshold, protected extensions, protected folders, Recycle Bin als standaard voor delete, expliciete toestemming voor permanent delete, geen onverwachte overwrites.

### 3.9 Conflict handling
Bij bestaande doelbestanden: Skip, Overwrite, Automatically rename (numeric suffix), Ask user.

Voorbeeld: `document.pdf` → `document (1).pdf` → `document (2).pdf`

### 3.10 Recursive processing
Optioneel, met: include subfolders, maximum recursion depth, excluded folders, excluded patterns.

### 3.11 Exclusions
Voor folders, subfolders, filenames, extensions, wildcard patterns, regex. Voorbeeld: `*.tmp`, `*.part`, `~*`

### 3.12 Activity / logging
Structured logging: timestamp, folder, rule, file, action, result, error, original path, destination path. Statussen: `INFO`, `SUCCESS`, `WARNING`, `ERROR`.

Activity-view in de GUI met: zoeken, filters, datumfilter, rule filter, folder filter, success/error filter.

### 3.13 Statistics
Dashboard met: files processed, moved, copied, renamed, deleted, errors, data moved, actions per rule, actions per folder. Per: today, 7 days, 30 days, all time.

### 3.14 Undo / recovery
Voor Move/Rename/Copy: log original path, new path, rule, action, timestamp. Ondersteun "Undo last action" waar mogelijk. Delete gebruikt bij voorkeur Recycle Bin zodat herstel mogelijk blijft.

### 3.15 Configuration
Persistent en versioned. Bevat: Settings, Folders, Rules, Conditions, Actions, Exclusions. Import/export: volledige configuratie, individuele folder, individuele rule. **JSON-formaat.** Schema/version migration ondersteunen. Automatische configuration backups.

**Portable-eis:** alles in de map van de applicatie zelf — geen registry, geen `%AppData%`.

### 3.16 GUI
Moderne Windows desktop UI. Hoofdonderdelen: Dashboard, Folders, Rules, Activity, Settings.

Rule editor moet visueel zijn (geen code vereist voor normale rules):
```
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

### 3.17 Advanced mode
Voor power users: regex, nested conditions, custom commands, scripts, advanced variables, hashes, advanced attributes. Normale gebruikers moeten deze complexiteit niet nodig hebben.

---

## 4. Niet-functionele eisen

### 4.1 Performance
Grote directories aankunnen (duizenden bestanden, grote bestanden, netwerkshares). Vereisten: responsive GUI, beperkt geheugengebruik, geen onnodige file reads, metadata eerst gebruiken, hashing alleen wanneer nodig, concurrency gecontroleerd uitvoeren.

### 4.2 Reliability
Een fout bij één bestand mag de volledige automation niet stoppen (bv. "100 matched, 97 successful, 2 skipped, 1 failed"). Ondersteun retry, error logging, recovery, duidelijke foutmeldingen. Race conditions en verdwenen/verplaatste bestanden correct afhandelen.

### 4.3 Security
Uitsluitend bestanden wijzigen binnen de expliciet geconfigureerde scope. Geen verborgen uploads, geen cloud dependency, geen telemetrie zonder expliciete toestemming, geen onverwachte code execution, geen automatische systeemwijzigingen. External scripts/commands moeten expliciet door de gebruiker geconfigureerd worden.

### 4.4 Testing
Automated tests voor: condition evaluation, AND/OR/NOT, nested conditions, regex, wildcards, dates, sizes, actions, multiple actions, conflicts, locked files, recursive folders, exclusions, dry run, import/export, configuration migration, concurrent execution, error handling. File operation tests uitsluitend in tijdelijke testdirectories.

### 4.5 Packaging
Normale, **portable** Windows-applicatie:
- executable
- **GEEN installer**
- application icon (`fldrsrtr.png`)
- **GEEN** Start Menu-snelkoppeling
- **GEEN** desktop-snelkoppeling
- **GEEN** uninstall support
- configuration/data separation (alles naast de exe)
- build script
- release build (portable zip)

---

## 5. Architectuurbeslissing (samenvatting — volledige onderbouwing hieronder)

**Gekozen stack: C# op .NET Framework 4.8.1, met WPF.** Status: **Accepted.**

### Waarom
- **.NET Framework 4.8.1 staat al standaard op elke ondersteunde Windows 10/11-installatie** (uitzondering: Windows 11 21H2, waar een handmatige upgrade naar 4.8.1 nodig is — praktisch altijd al aanwezig). Dat betekent: **niets te bundelen, niets te installeren**, en een klein uitvoerbaar bestand — precies wat de portable/dependency-arme eis vraagt.
- Modern .NET (.NET 10, huidige LTS) zou ook "installatievrij" zijn via self-contained single-file publishing, maar bundelt dan de volledige runtime (~100-150 MB voor een WPF-app) — dat is geen "eenvoudig, weinig dependencies"-resultaat meer, ook al hoeft niemand iets te installeren.
- WPF geeft, in vergelijking met C++ (Win32) of Rust (egui/Slint), veruit de snelste en betrouwbaarste weg naar de gevraagde rijke GUI: geneste condition-groups, drag-and-drop rule-herordening, een filterbaar activity-log/datagrid — dit via beproefde, declaratieve databinding in plaats van veel handgeschreven UI-code.
- Python (PySide6/PyInstaller) valt af: grote, trage bundels, veelvuldige antivirus/SmartScreen-waarschuwingen, zwakkere native Windows-integratie.

### Overwogen en bewust afgewezen: AutoIt / AutoHotkey
Deze zijn serieus overwogen (ze zijn zelfs "dependency-vrijer" dan .NET Framework: de interpreter zit al in de gecompileerde exe, geen enkele aanname over de doelmachine nodig). AutoHotkey v2 is daarbij duidelijk sterker dan AutoIt (echte class-syntax/OOP).

Ze zijn afgevallen omdat drie eisen uit dit document expliciet blijven staan en daar structureel tegen aanlopen:
1. **§5-architectuurscheiding** (rule engine mag niet afhankelijk zijn van GUI) — in AutoIt/AHK een codeerafspraak, geen compileertijd-afgedwongen grens zoals in C# (aparte assemblies).
2. **§4.4-testeisen** (uitgebreide automated test-suite) — C# heeft met xUnit een industriestandaard; AutoIt/AHK hebben hooguit een paar kleine, weinig onderhouden hobby-testframeworks (Yunit, AutoHotUnit, assert.ahk).
3. **§3.16-moderne, visuele rule editor** — AutoIt/AHK blijven steken op klassieke Win32-controls; geen equivalent van WPF's databinding/`HierarchicalDataTemplate`/drag-drop-libraries.

**Mocht de prioriteit ooit verschuiven** naar "zo klein en simpel mogelijk, ook als dat een pragmatischere UI en handmatige tests betekent" — dan is AutoHotkey v2 het eerste alternatief om opnieuw te overwegen, niet AutoIt.

### Concrete keuzes per onderdeel
- **GUI:** WPF + `ModernWpf` of `WPF-UI` (lepoco/wpfui) voor een Fluent-achtige, moderne look (.NET Framework heeft geen ingebouwd Fluent-thema — dat kwam pas met .NET 9). MVVM via `CommunityToolkit.Mvvm`. Drag-and-drop herordening via `GongSolutions.WPF.DragDrop`. Notificaties via `System.Windows.Forms.NotifyIcon.ShowBalloonTip` (geen extra dependency, geen tray-icoon, enkel een kortstondige melding na een handmatige run).
- **Persistence:** configuratie (settings/folders/rules/exclusions) als JSON via `Newtonsoft.Json`, in een submap naast de `.exe` (`AppDomain.CurrentDomain.BaseDirectory`, nooit `%AppData%`/registry). `"schemaVersion"`-veld + migratiefuncties. Automatische timestamped backups vóór elke save. Activity-log als append-only **JSON Lines**-bestand (bewust géén SQLite — dat voegt een native dependency toe die niet nodig is zonder 24/7-achtergrondvolume).
- **Testing:** `xUnit` + `FluentAssertions`, `System.IO.Abstractions` voor filesystem-mocking, integratietests uitsluitend in `Path.GetTempPath()`-tijdelijke mappen, `Verify.Xunit` voor dry-run-snapshottests. (Testtooling is build-/devtime, telt niet mee in de "weinig dependencies"-eis van de uitgeleverde app.)
- **Installer:** geen. Eén PowerShell-releasescript: build → verzamelen (`.exe` + `Newtonsoft.Json.dll` + eventuele UI-styling-dll's + `fldrsrtr.png` als icoon) → zippen, met versienummer en SHA-256-checksum.

### Architectuur
```
UI (WPF)
│  Dashboard / Folders / Rules / Activity / Settings
│  Composition root: handmatige DI in App.xaml.cs (geen Generic Host nodig — geen achtergrondproces)
│
├── App.Core                (mag NOOIT verwijzen naar App.UI)
│   ├── Condition evaluator (incl. geneste AND/OR/NOT-groepen)
│   ├── Action planner
│   └── Execution engine    (dry-run en echte executie via hetzelfde pad)
│
├── App.Infrastructure       (mag NOOIT verwijzen naar App.UI)
│   ├── On-demand folder scanner (GEEN filesystem watcher)
│   ├── Safety subsystem (protected paths/extensions)
│   ├── Recycle Bin-interop
│   ├── Configuration persistence (JSON + migraties + backups)
│   └── Activity log (JSON Lines)
```

De Core/Infrastructure ↔ UI-scheiding wordt vanaf dag 1 als **compileertijd-check** afgedwongen (bv. via `NetArchTest`), niet als losse afspraak.

---

## 6. Gefaseerd implementatieplan

Elke fase levert een werkend, zelf te starten portable resultaat op. Testen (§4.4) worden per fase geschreven, niet uitgesteld.

### Fase 0 — Skeleton & portable pipeline (de-risk eerst)
- Solution + `App.Core`, `App.Infrastructure`, `App.UI` (WPF, `net481`), plus `App.Core.Tests`, `App.Infrastructure.Tests` (xUnit).
- Architectuurtest die faalt zodra Core/Infrastructure naar UI verwijst.
- Lege WPF-shell met de vijf hoofdsecties als placeholders.
- Config lezen/schrijven relatief aan de exe-map, `"schemaVersion": 1`.
- **PowerShell-releasescript** bouwen en op een schone map/machine testen (build → verzamelen → zippen, incl. `fldrsrtr.png` als icoon).
- **Definition of done:** portable exe start op een schone Windows-machine, toont een leeg venster, maakt `config.json` naast zichzelf aan.

### Fase 1 — MVP
- **Core:** `Rule`/`Condition`/`Action`-model (nog platte lijst, geen nesting). Evaluator voor filename/extension/size/age. Action-planner + executor voor move/copy/rename/delete→Recycle Bin. Eén evaluatiepad voor dry-run én echte executie.
- **Infrastructure:** on-demand scanner (niet-recursief), `IFileSystem`-implementatie, protected paths hardcoded, config-persistence (folders+rules), JSON Lines activity-log.
- **UI:** Folders-scherm (toevoegen/verwijderen/bewerken/openen in Explorer/scan nu), eenvoudige rule-editor, dry-run-weergave, activity-lijst.
- **Demo:** map toevoegen → regel maken → dry-run → echt uitvoeren → resultaat in activity-log.

### Fase 2 — Uitgebreide regels (V2, zonder watcher/achtergrond)
- **Core:** geneste AND/OR/NOT condition-groups, meerdere acties per rule, exclusions-evaluatie, conflict-resolutielogica, per-bestand locking binnen één run.
- **Infrastructure:** recursieve mapverwerking (max depth), regex/wildcard met `matchTimeout`.
- **UI:** boomstructuur-rule-editor, drag-drop actie-herordening, conflict-dialoog, recursion/exclusion-instellingen.
- **Demo:** geneste regel met meerdere acties, recursief, met exclusions en zichtbare conflicthandling.

### Fase 3 — Geavanceerde functies (V3, zonder tray/startup)
- **Core:** dynamic variables-resolver, duplicate-detection via SHA-256, advanced actions (open/open with/execute external/create folder/add-remove extension/dynamische submap/ZIP).
- **Infrastructure:** ZIP via `System.IO.Compression`, notificatie na handmatige run.
- **UI:** advanced-mode toggle, variabelen-hulp in rule-editor.
- **Demo:** regel archiveert naar `D:\Archief\{Year}\{Month}\`, detecteert duplicaten, toont melding na afloop.

### Fase 4 — Afwerking (V4, zonder installer)
- **Core:** undo-model (original path, new path, rule, timestamp).
- **Infrastructure:** eerste echte config-schema-migratie, automatische config-backups met retentie, import/export, statistics-aggregatie over het activity-log, undo-uitvoering.
- **UI:** Dashboard met statistieken, Activity-scherm met zoeken/filters, undo-knop, import/export-dialogen, geavanceerde instellingen (protected folders/extensions, confirmation threshold, max files per run).
- **Release:** versienummering + SHA-256-checksum in het releasescript; performance-optimalisatie op basis van reële metingen uit fase 1-3.
- **Demo:** volledige workflow inclusief undo, dashboard, import/export, afgewerkte versienummerde release-zip.

### Testtraceability (§4.4 → fase)
| Testcategorie | Fase |
|---|---|
| Condition evaluation (filename/extension/size/age), dates/sizes, dry run | 1 |
| AND/OR/NOT, nested conditions, regex, wildcards, conflicts, recursive folders, exclusions, concurrent execution | 2 |
| — | 3 (geen nieuwe categorie, wel uitbreiding van bestaande dekking) |
| Import/export, configuration migration | 4 |
| Error handling, locked files | doorlopend vanaf fase 1 |

### Risico's om te bewaken (zie ook §5)
- **Portable/schrijfrechten:** de app moet duidelijk falen (met begrijpelijke melding) als de map waarin ze staat niet beschrijfbaar is — test dit al in Fase 0.
- **Recycle Bin op UNC-paden:** werkt niet zoals op lokale schijven — expliciete waarschuwing in de UI bij het configureren van delete op een netwerkmap.
- **Regex-ReDoS:** `matchTimeout` instellen zodra regex geïntroduceerd wordt (Fase 2).
- **.NET Framework is een bevroren platform:** geaccepteerd gevolg van de dependency-arme eis; de Core/Infrastructure/UI-scheiding houdt een latere retargeting naar modern .NET beperkt in omvang mocht dat ooit nodig zijn.

---

## 7. Eerste stappen voor deze Claude Code-sessie

1. Bevestig dat de map `C:\claude_code\FldrSrtr` bestaat en dat `fldrsrtr.png` daar aanwezig is.
2. Initialiseer git in die map en koppel de remote:
   ```
   git init
   git remote add origin https://github.com/Vampie/FldrSrtr.git
   ```
3. Start met **Fase 0** hierboven: solution-structuur, architectuurtest, lege WPF-shell, en — niet uitstellen — het portable releasescript, vóór er functionaliteit bijkomt.
4. Werk daarna fase voor fase verder (§6), telkens met de bijhorende tests, en lever elke fase op als een werkend, zelf te starten portable exe-bestand.
