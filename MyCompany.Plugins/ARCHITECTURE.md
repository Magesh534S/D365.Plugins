# MyCompany.Plugins – Architecture & Flow

A Dataverse / Dynamics 365 C# plugin library (.NET Framework class library) that bundles several account-related features behind a single plugin entry point.

> GitHub renders the Mermaid diagrams below automatically.

---

## 1. Project Structure

```mermaid
flowchart LR
    ROOT["📦 MyCompany.Plugins"]

    ROOT --> REF["References"]
    ROOT --> CORE["📁 Core"]
    ROOT --> FEAT["📁 Features"]
    ROOT --> ENTRY["AccountPlugin.cs<br/>(entry point)"]
    ROOT --> CFG["app.config / packages.config"]
    ROOT --> SNK["Account.snk<br/>(assembly signing key)"]

    FEAT --> F1["AccountNumberFeature.cs"]
    FEAT --> F2["AccountSourceTagPlugin.cs"]
    FEAT --> F3["AccountValidationFeature.cs"]
    FEAT --> F4["ChangeAuditFeature.cs"]
    FEAT --> F5["DeactivationCascadeFeature.cs"]
    FEAT --> F6["DeleteGuardFeature.cs"]
    FEAT --> F7["DuplicateAccountFeature.cs"]
    FEAT --> F8["FollowUpTaskFeature.cs"]
    FEAT --> F9["NormalizationFeature.cs"]

    style ROOT fill:#1f6feb,color:#fff,stroke:#1f6feb
    style ENTRY fill:#238636,color:#fff,stroke:#238636
    style FEAT fill:#8957e5,color:#fff,stroke:#8957e5
    style CORE fill:#bf8700,color:#fff,stroke:#bf8700
```

---

## 2. High-Level Execution Flow

One registered plugin class (`AccountPlugin`) receives the Dataverse event and dispatches to the individual feature classes.

```mermaid
flowchart TD
    A(["Dataverse event on Account<br/>Create / Update / Delete / SetState"]) --> B["AccountPlugin.Execute()<br/>IServiceProvider"]
    B --> C["Resolve services<br/>IPluginExecutionContext<br/>ITracingService<br/>IOrganizationService"]
    C --> D{"Message & Stage?"}

    D --> E["Validation &<br/>Normalization"]
    D --> G["Source tagging<br/>(shared variables)"]
    D --> H["Account number &<br/>duplicate check"]
    D --> I["Delete guard &<br/>deactivation cascade"]
    D --> J["Audit &<br/>follow-up task"]

    E --> E1["AccountValidationFeature"]
    E --> E2["NormalizationFeature"]
    G --> G1["AccountSourceTagPlugin"]
    H --> H1["AccountNumberFeature"]
    H --> H2["DuplicateAccountFeature"]
    I --> I1["DeleteGuardFeature"]
    I --> I2["DeactivationCascadeFeature"]
    J --> J1["ChangeAuditFeature"]
    J --> J2["FollowUpTaskFeature"]

    style A fill:#1f6feb,color:#fff
    style B fill:#238636,color:#fff
    style D fill:#bf8700,color:#fff
```

> ⚠️ The message/stage grouping above is a logical overview. Adjust it to match your actual plugin step registrations.

---

## 3. Cross-Step Communication (Shared Variables)

`AccountSourceTagPlugin` is split into two cooperating steps. **Plugin A** records information in `context.SharedVariables`; **Plugin B** reads it later in the same pipeline and decides what to do.

```mermaid
sequenceDiagram
    autonumber
    participant DV as Dataverse Pipeline
    participant A as Plugin A<br/>SetContextFromTag (PreOperation)
    participant SV as context.SharedVariables
    participant B as Plugin B<br/>ActOnContext
    participant F as FollowUpTaskFeature

    DV->>A: Execute (PreOperation)
    A->>A: Read tag from record
    A->>SV: SharedVariables[SourceKey] = tag
    alt tag is "Migration" or "Batch"
        A->>SV: SharedVariables[SkipKey] = true
        A->>A: trace "Follow-up note will be skipped"
    end

    DV->>B: Execute (later stage)
    B->>SV: TryGetShared(SourceKey)
    alt source not found
        B->>B: trace "No source recorded; PreOperation step may not be registered."
        B-->>DV: return
    else source found
        B->>SV: TryGetShared(SkipKey)
        alt skip == true
            B->>B: trace "Skipping note. Source = {source}"
            B-->>DV: return
        else skip not set
            B->>F: Create follow-up note/task
        end
    end
```

### Decision logic (flowchart view)

```mermaid
flowchart TD
    S(["Plugin B: ActOnContext"]) --> Q1{"SourceKey in<br/>SharedVariables?"}
    Q1 -- No --> R1["Trace: No source recorded<br/>PreOperation step may not be registered"]
    R1 --> END1(["return"])
    Q1 -- Yes --> Q2{"SkipKey = true?"}
    Q2 -- Yes --> R2["Trace: Skipping note<br/>Source = {source}"]
    R2 --> END2(["return"])
    Q2 -- No --> R3["Run follow-up logic"]
    R3 --> END3(["done"])

    style S fill:#1f6feb,color:#fff
    style END1 fill:#6e7681,color:#fff
    style END2 fill:#6e7681,color:#fff
    style END3 fill:#238636,color:#fff
```

---

## 4. Feature Reference

| Feature class | Purpose |
|---|---|
| `AccountNumberFeature` | Account number generation / handling |
| `AccountSourceTagPlugin` | Tags the request source (e.g. Migration, Batch) and shares it across pipeline steps |
| `AccountValidationFeature` | Field / business-rule validation |
| `ChangeAuditFeature` | Tracks changes made to the account |
| `DeactivationCascadeFeature` | Cascades actions when an account is deactivated |
| `DeleteGuardFeature` | Prevents deletion under defined conditions |
| `DuplicateAccountFeature` | Duplicate account detection |
| `FollowUpTaskFeature` | Creates follow-up note/task (skipped for Migration / Batch sources) |
| `NormalizationFeature` | Cleans / standardizes field values |

---

## 5. Build & Deploy

```mermaid
flowchart LR
    A["Build in Visual Studio 2022<br/>(Release)"] --> B["Sign assembly<br/>Account.snk"]
    B --> C["Plugin Registration Tool<br/>Register assembly"]
    C --> D["Register steps<br/>Message / Stage / Entity"]
    D --> E["Add to Solution"]
    E --> F["Export & deploy to environment"]

    style A fill:#1f6feb,color:#fff
    style F fill:#238636,color:#fff
```

---

## Notes

- Keep PreOperation and later-stage steps registered together, otherwise Plugin B logs "No source recorded."
- Always use `ITracingService` so behavior can be diagnosed from the Plugin Trace Log.
- Do not commit secrets, connection strings, or `bin/` / `obj/` folders (add them to `.gitignore`).
