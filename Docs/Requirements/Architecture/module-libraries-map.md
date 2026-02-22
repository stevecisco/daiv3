# Module And Library Map

## Dependency Philosophy

**External dependencies are minimized by default.** Libraries shown below are either:
- Part of .NET framework (System.*, Microsoft.Extensions.*)
- Microsoft-supported (ONNX Runtime, ML.NET, Azure SDKs)
- Explicitly approved via Architecture Decision Document (ADD)

**All other dependencies require ADD approval.** See `./decisions/` directory for approved decisions.

```mermaid
flowchart LR
    subgraph CustomModules
        UI["UI (MAUI / CLI)"]
        Orchestration["Orchestration Layer"]
        ModelExec["Model Execution Layer"]
        Knowledge["Knowledge Layer"]
        Scheduler["Scheduler Service"]
        WebFetch["Web Fetch + Crawl"]
        McpIntegration["MCP Integration"]
        Persistence["Persistence Layer"]
    end

    subgraph ApprovedLibraries["Pre-Approved Libraries"]
        Onnx["Microsoft.ML.OnnxRuntime.DirectML"]
        Tokenizers["Microsoft.ML.Tokenizers"]
        Tensor["System.Numerics.TensorPrimitives"]
        SqliteLib["Microsoft.Data.Sqlite"]
        ExtAI["Microsoft.Extensions.AI"]
        FoundrySdk["Foundry Local SDK"]
    end
    
    subgraph PendingApproval["Pending ADD Approval"]
        McpSdk["ModelContextProtocol .NET SDK ⚠️"]
        HtmlParser["HTML Parser (TBD) ⚠️"]
        PdfLib["PDF Library (TBD) ⚠️"]
        OpenXml["Open XML SDK ⚠️"]
        Quartz["Quartz.NET ⚠️"]
    end

    subgraph ExternalServices
        FoundryRuntime["Foundry Local Runtime"]
        OnlineProviders["Online Providers\n(OpenAI, Azure OpenAI, Anthropic)"]
        McpServers["MCP Tool Servers"]
        WebSources["Web Sources"]
    end

    ModelExec --> ExtAI
    ModelExec --> FoundrySdk
    ModelExec --> FoundryRuntime
    ModelExec --> OnlineProviders

    Knowledge --> Onnx
    Knowledge --> Tokenizers
    Knowledge --> Tensor
    Knowledge --> SqliteLib

    WebFetch -.-> HtmlParser
    WebFetch --> WebSources

    Orchestration --> ExtAI
    McpIntegration -.-> McpSdk
    McpIntegration --> McpServers

    Scheduler -.-> Quartz
    Persistence --> SqliteLib

    Knowledge -.-> PdfLib
    Knowledge -.-> OpenXml

    UI --> Orchestration
    Orchestration --> ModelExec
    Orchestration --> Knowledge
    Orchestration --> Scheduler
    Orchestration --> WebFetch
    Orchestration --> McpIntegration
    Orchestration --> Persistence
```

**Legend:**
- Solid lines → Approved dependencies
- Dashed lines → Require Architecture Decision Document (ADD)
- ⚠️ → Not yet approved, ADD required before implementation
