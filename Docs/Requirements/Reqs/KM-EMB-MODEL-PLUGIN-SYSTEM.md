# KM-EMB-MODEL-PLUGIN-SYSTEM

Source Spec: 4. Embedding Model Management - Requirements

**Status: BACKLOG (v0.3 - Full Plugin System)**

## Requirement
The system SHALL dynamically load tokenizer implementations as DLL plugins at runtime, enabling extensible tokenizer support for new embedding models without requiring application recompilation or rebuilding.

## Rationale
While v0.2 catalog defines model metadata, the tokenizer implementations remain hardcoded. A plugin-based approach provides:
- **Extensibility**: Add tokenizers without recompiling the app
- **Separation of Concerns**: Tokenizer logic isolated from core app
- **Flexible Updates**: Deploy new tokenizers independently of app updates
- **Community Contribution**: Third parties can create custom tokenizers
- **Version Management**: Different models can require different tokenizer versions

## Specification (v0.3 - Full Implementation)

### 1. Plugin Architecture

**Plugin Components**:

```
Plugin Package Structure:
%LOCALAPPDATA%\Daiv3\models\embeddings\{modelId}\
├── model.onnx                      # Embedding model (ONNX format)
├── config.json                     # Model metadata
├── vocab.txt / sentencepiece.model # Tokenizer vocabulary artifacts
└── plugins/
    └── Daiv3.Tokenizer.*.dll      # Tokenizer plugin DLL

Example:
%LOCALAPPDATA%\Daiv3\models\embeddings\nomic-embed-text-v1.5\
├── model.onnx
├── config.json
├── sentencepiece.model
└── plugins/
    ├── Daiv3.Tokenizer.SentencePiece.dll
    ├── Daiv3.Tokenizer.SentencePiece.deps.json
    └── README.md
```

**Plugin Interface** (well-known contract):

```csharp
// All tokenizer plugins MUST implement this interface
namespace Daiv3.Tokenizer.Abstractions;

public interface IEmbeddingTokenizer
{
    string Name { get; }
    string ModelId { get; }
    int VocabularySize { get; }

    long[] Tokenize(string text);
    bool ValidateTokenIds(long[] tokenIds);
    IReadOnlyDictionary<string, int> GetSpecialTokens();
}

// Plugin discovery interface (required by loader)
public interface ITokenizerPlugin
{
    string PluginName { get; }
    string PluginVersion { get; }
    string[] SupportedTokenizers { get; }
    
    IEmbeddingTokenizer CreateTokenizer(string tokenizerName, string modelDirectory);
}
```

### 2. Tokenizer Plugin DLL Specification

**DLL Naming Convention**:
```
Daiv3.Tokenizer.{TokenizerType}.dll
  Examples: Daiv3.Tokenizer.SentencePiece.dll
            Daiv3.Tokenizer.BertWordPiece.dll
            Daiv3.Tokenizer.Custom.dll
```

**DLL Requirements**:
- Must implement `ITokenizerPlugin` interface
- Must implement `IEmbeddingTokenizer` interface
- Should be self-contained (bundle dependencies via .deps.json)
- Should expose a public class decorated with `[TokenizerPlugin]` attribute
- Should include version info in assembly attributes
- Should log via `ILogger` interface (injected at load time)

**Example Plugin Implementation** (SentencePiece):

```csharp
// File: Daiv3.Tokenizer.SentencePiece/Plugin.cs
[TokenizerPlugin]
public class SentencePiecePlugin : ITokenizerPlugin
{
    public string PluginName => "SentencePiece";
    public string PluginVersion => "1.0.0";
    public string[] SupportedTokenizers => ["sentencepiece"];

    public IEmbeddingTokenizer CreateTokenizer(string tokenizerName, string modelDirectory)
    {
        if (tokenizerName != "sentencepiece")
            throw new ArgumentException($"Unknown tokenizer: {tokenizerName}");
        
        return new SentencePieceTokenizer(modelDirectory);
    }
}

public class SentencePieceTokenizer : IEmbeddingTokenizer
{
    // ... implementation (same as v0.1 hardcoded version)
}
```

### 3. Plugin Catalog Integration

**Updated Catalog Entry** (with plugin DLL URLs):

```json
{
  "id": "nomic-embed-text-v1.5",
  "tier": 2,
  "tokenizer": {
    "type": "plugin",
    "name": "sentencepiece",
    "pluginDllUrl": "https://stdaiv3.blob.core.windows.net/plugins/tokenizer/Daiv3.Tokenizer.SentencePiece/1.0.0/Daiv3.Tokenizer.SentencePiece.dll",
    "pluginDepsUrl": "https://stdaiv3.blob.core.windows.net/plugins/tokenizer/Daiv3.Tokenizer.SentencePiece/1.0.0/Daiv3.Tokenizer.SentencePiece.deps.json",
    "pluginVersion": "1.0.0",
    "minimumVersion": "1.0.0",
    "maximumVersion": "2.0.0",
    "sha256": "abc123...",
    "estimatedSizeBytes": 65536
  },
  "downloads": {
    "model": { ... },
    "config": { ... },
    "vocab": { ... },
    "tokenizer": {
      "url": "https://stdaiv3.blob.core.windows.net/plugins/tokenizer/Daiv3.Tokenizer.SentencePiece/1.0.0/Daiv3.Tokenizer.SentencePiece.dll",
      "format": "dll",
      "size": 65536,
      "sha256": "abc123..."
    }
  }
}
```

### 4. Plugin Loader Service

**Services Required**:

#### `ITokenizerPluginLoader`
Loads and instantiates tokenizer plugins from DLL files

```csharp
public interface ITokenizerPluginLoader
{
    /// Load a plugin assembly from disk
    Task<ITokenizerPlugin> LoadPluginAsync(string dllPath);
    
    /// Unload a plugin (cleanup)
    Task UnloadPluginAsync(string pluginName);
    
    /// Get list of loaded plugins
    IReadOnlyList<string> GetLoadedPlugins();
    
    /// Check if plugin is loaded and compatible
    bool IsPluginLoaded(string pluginName, string minimumVersion);
}
```

**Implementation Details**:
- Use `AssemblyLoadContext` for isolated plugin loading (Windows only: use default context)
- Validate DLL signature/hash before loading (SHA256 check)
- Load DLL into isolated context to allow unloading and reloading
- Resolve `ILogger` dependency for plugin at load time
- Detect `[TokenizerPlugin]` attribute to find plugin class
- Instantiate plugin class and cache instance
- Support version compatibility checking

#### `ITokenizerPluginDownloadService`
Downloads tokenizer plugin DLLs from catalog URLs

```csharp
public interface ITokenizerPluginDownloadService
{
    /// Download plugin DLL for a specific model/tokenizer
    Task DownloadTokenizerPluginAsync(
        string modelId,
        string tokenizerName,
        string downloadUrl,
        string targetDirectory,
        IProgress<DownloadProgress> progress,
        CancellationToken ct = default);
    
    /// Verify plugin DLL integrity (hash check)
    Task<bool> VerifyPluginIntegrityAsync(string dllPath, string expectedSha256);
}
```

**Implementation Details**:
- Download DLL and .deps.json to: `%LOCALAPPDATA%\Daiv3\models\embeddings\{modelId}\plugins\`
- Verify SHA256 hash matches catalog
- Create .sha256 sidecar file for offline verification
- Handle download failures with retry (3 attempts, exponential backoff)
- Show progress to user
- Clean up incomplete downloads on failure

#### `IEmbeddingTokenizerPluginProvider`
Factory for creating tokenizer instances from plugins (replaces v0.1 provider)

```csharp
public interface IEmbeddingTokenizerPluginProvider
{
    /// Get tokenizer for active model, loading plugin if necessary
    Task<IEmbeddingTokenizer> GetTokenizerAsync(string modelId, CancellationToken ct = default);
    
    /// Preload/cache tokenizer for a model
    Task PreloadTokenizerAsync(string modelId, CancellationToken ct = default);
    
    /// Check if tokenizer is available (downloaded and valid)
    Task<bool> IsTokenizerAvailableAsync(string modelId);
    
    /// Get tokenizer metadata (name, version, etc.)
    Task<TokenizerMetadata> GetTokenizerMetadataAsync(string modelId);
}
```

**Implementation Logic**:
1. Query model registry for tokenizer type and version
2. Check if tokenizer plugin DLL exists locally
3. If not exists: Download from catalog
4. Load DLL using `ITokenizerPluginLoader`
5. Instantiate tokenizer via plugin factory
6. Cache instance for reuse
7. Return tokenizer to embedding generator

### 5. Plugin Discovery & Registration

**Plugin Discovery Process** (on startup):

1. **Scan for Plugins**:
   - Scan `%LOCALAPPDATA%\Daiv3\models\embeddings\*/plugins/` for DLLs
   - Scan `%LOCALAPPDATA%\Daiv3\plugins\` for system-wide plugins
   - Scan `<AppDirectory>\plugins\` for bundled plugins

2. **Load Plugins**:
   - Load each DLL into isolated context
   - Validate DLL signature and hash
   - Check for `[TokenizerPlugin]` attribute
   - Log plugin metadata (name, version, supported tokenizers)

3. **Register with Provider**:
   - Update registry with available tokenizers
   - Mark tokenizers as "installed" vs "available for download"
   - Validate tokenizer versions meet model requirements

4. **Error Handling**:
   - Invalid DLL: Log error, skip, try next
   - Version mismatch: Warn user, offer to download correct version
   - Missing dependency: Fail with clear error message

### 6. Plugin Dependency Management

**Plugin Dependencies** (resolution order):

1. **Framework Dependencies** (auto-managed):
   - .NET 10.0 runtime
   - System.* packages
   - Microsoft.Extensions.* packages

2. **Model Dependencies** (required):
   - Microsoft.ML.OnnxRuntime (version from model metadata)
   - Model-specific vocabularies/artifacts in model directory

3. **Plugin Dependencies** (via .deps.json):
   - Custom tokenizer libraries (e.g., official SentencePiece.NetNative for v0.3+)
   - Version-specific dependencies declared in DLL metadata

**Version Compatibility**:
- Each model declares minimum/maximum tokenizer version
- Plugin loader validates version bounds before instantiation
- If version mismatch: Download and load correct version
- Support side-by-side installation of different tokenizer versions

### 7. Plugin Security & Trust

**Security Considerations** (v0.3+):

1. **DLL Signature Verification**:
   - Verify SHA256 hash before loading
   - Store hash in catalog and sidecar files
   - Reject DLLs with mismatched hash
   - Log all hash mismatches with timestamp

2. **Plugin Source Whitelist**:
   - Only load DLLs from trusted locations (Azure, configured mirrors)
   - Reject user-provided DLLs unless explicitly trusted
   - Warn user before loading unsigned plugins

3. **Plugin Sandboxing** (future v0.4):
   - Load plugins in restricted `AssemblyLoadContext`
   - Limit filesystem access to model directory
   - Restrict network access
   - Monitor memory usage

### 8. Configuration & Settings

**Plugin Settings** (in `models.json`):

```json
{
  "plugins": {
    "enabled": true,
    "autoDownload": true,
    "trustUnsignedPlugins": false,
    "pluginSearchPaths": [
      "%LOCALAPPDATA%\\Daiv3\\plugins",
      "%LOCALAPPDATA%\\Daiv3\\models\\embeddings\\*/plugins"
    ],
    "pluginCacheTtlMinutes": 0,
    "maxPluginLoadTimeMs": 5000,
    "verifyPluginSignatures": true
  }
}
```

**Plugin Metadata Cache** (for offline operation):

```
%LOCALAPPDATA%\Daiv3\cache\plugins.json
{
  "lastUpdated": "2026-02-25T12:00:00Z",
  "plugins": {
    "sentencepiece": {
      "version": "1.0.0",
      "modelId": "nomic-embed-text-v1.5",
      "loaded": true,
      "loadTime": "2026-02-25T10:30:15Z"
    }
  }
}
```

## Future Implementation (v0.4+)

- Plugin marketplace (community tokenizers)
- Automatic plugin update checks
- Plugin dependency resolver (transitive dependencies)
- Plugin rollback to previous versions
- Plugin sandboxing with resource limits
- Official tokenizer plugins published as NuGet packages
- Third-party tokenizer contribution process

## Testing Plan (v0.3)

### Unit Tests
- Plugin DLL loading and validation
- Plugin attribute detection
- Version compatibility checking
- SHA256 verification
- Error handling for invalid/corrupted DLLs
- Plugin factory instantiation
- Mock plugin implementations

### Integration Tests
- Download tokenizer plugin from Azure
- Load plugin and create tokenizer instance
- Use tokenizer to generate embeddings
- Load multiple plugins side-by-side
- Plugin unloading and reload
- Plugin version conflicts and resolution
- Network failure handling during plugin download

### Security Tests
- SHA256 mismatch detection
- Unsigned plugin rejection
- Plugin source validation
- Malicious DLL detection (signature validation)

### Performance Tests
- Plugin load time (<200ms per plugin)
- Plugin instantiation time (<10ms)
- Memory overhead per loaded plugin (<50MB)
- Plugin unload cleanup

## Usage and Operational Notes

- Plugin download happens automatically when model is selected (if not cached)
- Plugins are cached locally after download (no re-download unless explicit refresh)
- Plugins are loaded lazily (on-demand when embedding generation starts)
- Plugin errors are logged with actionable remediation steps
- Failed plugin load falls back to built-in tokenizer (if available)
- User can manually trigger plugin refresh/cleanup via Settings

## Acceptance Criteria

- [ ] Plugin interface defined and documented
- [ ] Plugin loader implemented with DLL isolation
- [ ] Plugin downloader integrated with model discovery
- [ ] Plugin validation with SHA256 verification
- [ ] Version compatibility checking
- [ ] Error handling for all failure modes (invalid DLL, version mismatch, etc.)
- [ ] Logging for all plugin operations
- [ ] Integration with v0.2 catalog system
- [ ] Fallback to bundled tokenizer if plugin unavailable
- [ ] Security validation (hash check, source whitelist)
- [ ] Unit and integration tests (90%+ coverage)
- [ ] Documentation for plugin developers
- [ ] Documentation for end users (plugin installation/troubleshooting)

## Dependencies

- KM-EMB-MODEL-CATALOG (v0.2, required)
- KM-EMB-MODEL-001 (Registry, needs update)
- KM-EMB-MODEL-002 (Discovery, needs update)
- KM-EMB-MODEL-003 (Selection, needs update)
- Microsoft.ML.OnnxRuntime (pre-approved dependency)

## Related Requirements

- KLC-REQ-013 (Model management and plugin UI)
- ADD-Plugin-Architecture (Architecture Decision Document)

## Blocking Tasks / Open Questions

- [ ] Finalize plugin interface (v0.2 catalog required first)
- [ ] Determine DLL naming/versioning scheme
- [ ] Establish plugin source and distribution strategy
- [ ] Define plugin developer documentation and templates
- [ ] Decide on cryptographic signing mechanism for plugins
- [ ] Determine plugin marketplace UX/process (v0.4+)
- [ ] Plan for backwards compatibility with v0.1 hardcoded tokenizers
