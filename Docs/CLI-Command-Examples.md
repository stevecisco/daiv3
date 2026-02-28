# DAIv3 CLI Command Examples

> **Auto-Updated**: This file is automatically updated as new CLI commands are implemented.
> **Last Updated**: February 25, 2026

## Overview

The DAIv3 CLI provides command-line access to all system features for testing, automation, and scripting. Commands are organized by functional area.

## Usage

```bash
# Using the run script (Windows)
.\run-cli.bat [command] [options]

# Direct execution
dotnet run --project src/Daiv3.App.Cli/Daiv3.App.Cli.csproj -- [command] [options]

# After build
.\src\Daiv3.App.Cli\bin\Debug\net10.0-windows10.0.26100\Daiv3.App.Cli.exe [command] [options]
```

---

## Database Commands

### Initialize Database
```bash
.\run-cli.bat db init
```
Creates the SQLite database with all required tables and schema.

**Output Example:**
```
Initializing Daiv3 database...
✓ Database initialized successfully
  Path: C:\Users\[user]\AppData\Local\Daiv3\daiv3.db
  Schema Version: 1
```

### Check Database Status
```bash
.\run-cli.bat db status
```
Displays database location, size, and schema version.

**Output Example:**
```
Database Status:
  Path: C:\Users\[user]\AppData\Local\Daiv3\daiv3.db
  Size: 98,304 bytes
  Last Modified: 2/23/2026 2:30:45 PM
  Schema Version: 1
```

---

## Dashboard Commands

### Show System Dashboard
```bash
.\run-cli.bat dashboard
```
Displays system status, hardware detection, and task queue information.

**Output Example:**
```
═══════════════════════════════════════════════════════════
                    DAIV3 DASHBOARD
═══════════════════════════════════════════════════════════

HARDWARE STATUS:
  Overall: System Ready
  NPU: Detection pending (integration pending)
  GPU: Detection pending (integration pending)

TASK QUEUE:
  Queued Tasks: 0
  Completed Tasks: 0
  Current Activity: Ready for tasks

NOTE: Full hardware detection and queue monitoring pending integration.
      Use 'db status' to check database, 'projects list' for projects.
```

---

## Chat Commands

### Interactive Chat
```bash
.\run-cli.bat chat
```
Starts an interactive chat session. Type messages and press Enter. Type `exit` to quit.

**Example Session:**
```
═══════════════════════════════════════════════════════════
                 DAIV3 CHAT INTERFACE
═══════════════════════════════════════════════════════════
Type your message and press Enter. Type 'exit' to quit.

You: Hello, how are you?
AI: Echo: Hello, how are you? (Orchestration integration pending)
You: What can you do?
AI: Echo: What can you do? (Orchestration integration pending)
You: exit
Goodbye!
```

### Single Message Mode
```bash
.\run-cli.bat chat --message "Hello from CLI"
# or
.\run-cli.bat chat -m "Hello from CLI"
```
Sends a single message and exits immediately.

**Output Example:**
```
User: Hello from CLI
AI: Echo: Hello from CLI (Orchestration integration pending)
```

---

## Project Management Commands

### List All Projects
```bash
.\run-cli.bat projects list
```
Displays all projects in the system.

**Output Example:**
```
PROJECTS:
  ID: 7e0f2e8f-4c3e-4b91-93f8-001fd0d9589f
  Name: My Project
  Description: Project description
  Root Paths:
    - C:\repo\src
    - C:\repo\docs
  Status: active
  Created: 2026-02-28 18:42:11 UTC
  Updated: 2026-02-28 18:42:11 UTC
```

### Create New Project
```bash
.\run-cli.bat projects create --name "My Project" --description "Project description"
# or short form
.\run-cli.bat projects create -n "My Project" -d "Project description"
# with explicit root path(s)
.\run-cli.bat projects create --name "My Project" -r "C:\repo\src"
.\run-cli.bat projects create --name "My Project" -r "C:\repo\src" -r "C:\repo\docs"
```
Creates a new project with the specified name and optional description. Root paths default to the current working directory if `--root-path` is not provided.

**Output Example:**
```
✓ Project created successfully
  ID: ccc3d3cd-75e7-4a90-a2ec-c04109ce1e0c
  Name: My Project
  Description: Project description
  Root Paths:
    - C:\repo\src
  Status: active
  Created: 2026-02-28 18:42:11 UTC
```

---

## Settings Commands

### Show Current Settings
```bash
.\run-cli.bat settings show
```
Displays all current system settings and configuration.

**Output Example:**
```
CURRENT SETTINGS:

Directories:
  Data Directory: C:\Users\steve\AppData\Local\Daiv3\Data
  Models Directory: C:\Users\steve\AppData\Local\Daiv3\Models

Hardware Preferences:
  Use NPU: True (default)
  Use GPU: True (default)

Model Execution:
  Allow Online Providers: False (default)
  Token Budget: 8192 (default)

NOTE: Settings persistence integration pending.
```

---

## Embedding Commands

### Test Embedding Generation
```bash
.\run-cli.bat embedding test
# With custom text
.\run-cli.bat embedding test --text "Your text here"
# Short form
.\run-cli.bat embedding test -t "Your text here"
```

Tests embedding generation with the ONNX embedding model. Generates a 768-dimensional normalized vector for the input text and displays statistics.

**Requirements:**
- **Two-tier embedding models** (automatically downloaded on first run):
  - **Tier 1 (Topic/Summary):** all-MiniLM-L6-v2 (~86 MB) - 384 dimensions
    - Path: `%LOCALAPPDATA%\Daiv3\models\embeddings\all-MiniLM-L6-v2\model.onnx`
  - **Tier 2 (Chunk):** nomic-embed-text-v1.5 (~522 MB) - 768 dimensions
    - Path: `%LOCALAPPDATA%\Daiv3\models\embeddings\nomic-embed-text-v1.5\model.onnx`
- Download progress is displayed in the console
- Requires internet connectivity for first-time initialization
- Both models use the same tokenizer (r50k_base encoding)

**Output Example (First Run - Two-Tier Model Download):**
```
Tier 1 embedding model (all-MiniLM-L6-v2) not found.
Downloading from Azure Blob Storage...

Tier 1 Progress: 5.0% (4.31 MB / 86.22 MB)
Tier 1 Progress: 10.0% (8.62 MB / 86.22 MB)
...
Tier 1 Progress: 95.0% (81.91 MB / 86.22 MB)
✓ Tier 1 model download completed successfully

Tier 2 embedding model (nomic-embed-text-v1.5) not found.
Downloading from Azure Blob Storage...

Tier 2 Progress: 5.0% (26.10 MB / 521.96 MB)
Tier 2 Progress: 10.0% (52.20 MB / 521.96 MB)
...
Tier 2 Progress: 95.0% (495.86 MB / 521.96 MB)
✓ Tier 2 model download completed successfully

EMBEDDING TEST
==============
Input text: The quick brown fox jumps over the lazy dog

Generating embedding... ✓ Success!

Embedding dimensions: 768
Vector magnitude: 1.0000
Value range: [-0.166835, 0.298630]

First 10 embedding values:
  [  0] = 0.058221
  [  1] = 0.016115
  [  2] = -0.166835
  [  3] = 0.037738
```

**Output Example (Subsequent Runs):**
```
EMBEDDING TEST
==============
Input text: The quick brown fox jumps over the lazy dog

Generating embedding... ✓ Success!

Embedding dimensions: 768
Vector magnitude: 1.0000
Value range: [-0.166835, 0.298630]

First 10 embedding values:
  [  0] = 0.058221
  [  1] = 0.016115
  [  2] = -0.166835
  [  3] = 0.037738
  [  4] = 0.011134
  [  5] = 0.041877
  [  6] = 0.006807
  [  7] = 0.028149
  [  8] = 0.022657
  [  9] = -0.005705
  ... (758 more values)
```

**Output Example (Model Not Found):**
```
EMBEDDING TEST
==============
Input text: The quick brown fox jumps over the lazy dog

Generating embedding... ✗ Failed to generate embedding: ONNX model file not found.
```

**Validation Points:**
- ✓ Embedding dimensions: 768 (normalized vector)
- ✓ Vector magnitude: ~1.0000 (normalized)
- ✓ Value range: Approximately [-1, 1]
- ✓ No NaN or Infinity values in output
- ✓ Model loaded using DirectML acceleration (or CPU fallback)

---

## Multimodal (CLIP) Commands

### Test CLIP Text Encoding
```bash
.\run-cli.bat multimodal text
# With custom text
.\run-cli.bat multimodal text --text "a person walking in the park"
# Short form
.\run-cli.bat multimodal text -t "a person walking in the park"
```

Tests CLIP multimodal text encoding. Generates a 512-dimensional embedding for image-text similarity matching.

**Requirements:**
- **CLIP Multimodal Models** (automatically downloaded on first run):
  - **Full Precision (NPU/GPU):** xenova/clip-vit-base-patch32
    - Text Encoder: `%LOCALAPPDATA%\Daiv3\models\multimodal\clip-vit-base-patch32\full-precision\model.onnx`
    - Vision Encoder: `%LOCALAPPDATA%\Daiv3\models\multimodal\clip-vit-base-patch32\full-precision\vision_model.onnx`
  - **Quantized (CPU):** uint8 quantized variants
    - Text Encoder (uint8): `%LOCALAPPDATA%\Daiv3\models\multimodal\clip-vit-base-patch32\quantized\model_uint8.onnx`
    - Vision Encoder (int8): `%LOCALAPPDATA%\Daiv3\models\multimodal\clip-vit-base-patch32\quantized\vision_model_int8.onnx`
- Hardware-aware variant selection (NPU/GPU → full precision, CPU → quantized)
- Enables image-text similarity scores and cross-modal retrieval

**Output Example:**
```
CLIP MULTIMODAL TEXT ENCODING TEST
==================================
Input text: a person walking in the park

Status: CLIP text encoder integration pending

Expected capabilities:
  • Text encoding into 512-dimensional vectors
  • Normalized L2 distance for similarity comparison
  • Image-text similarity matching for vision tasks

Model Information:
  • Model: xenova/clip-vit-base-patch32
  • Text Encoder Output Dims: 512
  • Vision Encoder Output Dims: 512
  • Hardware: NPU/GPU (full precision), CPU (quantized)

CLIP text encoding test completed (integration pending)
```

**Validation Points (When Implemented):**
- ✓ Text embeddings: 512 dimensions (L2 normalized)
- ✓ Image embeddings: 512 dimensions (L2 normalized)
- ✓ Similarity scores: Cosine distance between text and image embeddings
- ✓ Hardware variants properly selected based on detected hardware

---

## OCR Commands

### Test OCR Capabilities
```bash
.\run-cli.bat ocr test
```

Tests Optical Character Recognition (OCR) capabilities using TrOCR. Demonstrates document and handwriting text recognition.

**Requirements:**
- **TrOCR Models** (automatically downloaded on first run):
  - **Full Precision (NPU/GPU):** microsoft/trocr-base-printed
    - Encoder (FP16): `%LOCALAPPDATA%\Daiv3\models\ocr\trocr-base-printed\fp16\encoder_model.onnx`
    - Decoder (FP16): `%LOCALAPPDATA%\Daiv3\models\trocr-base-printed\fp16\decoder_model.onnx`
  - **Quantized (CPU):** int8 quantized variants
    - Encoder (int8): `%LOCALAPPDATA%\Daiv3\models\ocr\trocr-base-printed\quantized\encoder_model_int8.onnx`
    - Decoder (int8): `%LOCALAPPDATA%\Daiv3\models\ocr\trocr-base-printed\quantized\decoder_model_int8.onnx`
- Hardware-aware variant selection (NPU/GPU → FP16, CPU → int8)
- Document and handwriting text recognition

**Output Example:**
```
OCR (OPTICAL CHARACTER RECOGNITION) TEST
========================================

Status: TrOCR integration pending

Expected capabilities:
  • Document and handwriting text recognition
  • Support for multiple languages
  • Encoder-decoder architecture for accurate transcription

Model Information:
  • Base Model: microsoft/trocr-base-printed
  • Architecture: Vision Encoder (ViT) + Text Decoder (LSTM)
  • Input: Normalized image patches
  • Output: Text tokens (character sequences)

Hardware Variants:
  • NPU/GPU: FP16 precision for accelerated inference
  • CPU: Quantized (int8) for efficient CPU execution

Usage Example:
  ocr test
    Demonstrates OCR capabilities on sample images
```

**Validation Points (When Implemented):**
- ✓ Document text recognition: Accurate transcription of printed text
- ✓ Handwriting recognition: Support for handwritten documents
- ✓ Multi-language support: Handle various languages and scripts
- ✓ Hardware variants properly selected based on detected hardware
- ✓ Encoder-decoder pipeline coordinates vision understanding with text generation

---

## Help and Documentation

### General Help
```bash
.\run-cli.bat --help
# or
.\run-cli.bat -h
```
Shows all available commands and options.

### Command-Specific Help
```bash
.\run-cli.bat [command] --help
```

**Examples:**
```bash
.\run-cli.bat db --help
.\run-cli.bat chat --help
.\run-cli.bat projects --help
.\run-cli.bat settings --help
.\run-cli.bat embedding --help
.\run-cli.bat multimodal --help
.\run-cli.bat ocr --help
```

---

## Integration Status

| Feature Area | Commands | Status | Notes |
|--------------|----------|--------|-------|
| Database | `db init`, `db status` | ✅ Complete | Fully functional |
| Dashboard | `dashboard` | 🔄 Partial | Hardware detection pending |
| Chat | `chat`, `chat -m` | 🔄 Partial | Orchestration layer pending |
| Projects | `projects list`, `projects create` | ✅ Complete | Persistence-backed project listing/creation with explicit root path support (`--root-path`) |
| Settings | `settings show` | 🔄 Partial | Configuration service pending |

**Legend:**
- ✅ Complete - Fully implemented and integrated
- 🔄 Partial - Command works, integration pending
- ⏳ Planned - Not yet implemented

---

## Future Commands (Planned)

The following commands will be added as features are implemented:

### Projects (Additional)
- `projects delete --id <guid>` - Delete a project
- `projects show --id <guid>` - Show project details
- `projects tasks --id <guid>` - List tasks in a project

### Tasks
- `tasks list [--project-id <guid>]` - List all tasks
- `tasks create --name "Task" --project-id <guid>` - Create a task
- `tasks update --id <guid> --status <status>` - Update task status

### Knowledge Management
- `knowledge index --path <directory>` - Index documents
- `knowledge search --query "search terms"` - Search indexed content
- `knowledge status` - Show indexing status

### Model Management
- `models list` - List available models
- `models download --name <model>` - Download a model
- `models test --name <model>` - Test model inference

### Settings (Additional)
- `settings set --key <key> --value <value>` - Update a setting
- `settings reset` - Reset all settings to defaults
- `settings export --file <path>` - Export settings to file
- `settings import --file <path>` - Import settings from file

---

## Notes for Developers

### Adding New Commands

When implementing new CLI commands:

1. **Add command handler** in `src/Daiv3.App.Cli/Program.cs`
2. **Create unit tests** for the handler logic
3. **Test manually** using `.\run-cli.bat` 
4. **Update this file** with:
   - Command syntax
   - Parameter descriptions
   - Example usage
   - Expected output
   - Integration status

### Command Naming Conventions

- Use lowercase for commands and subcommands
- Use `--long-form` and `-s` short forms for options
- Keep command names concise but descriptive
- Group related commands under a parent command (e.g., `db init`, `db status`)

### Output Formatting

- Use `✓` for success messages
- Use `✗` for error messages  
- Use `═` for section headers
- Include clear status messages for pending integrations
- Provide helpful next steps in output when appropriate

---

**Version**: 1.0  
**Requirement**: ARCH-REQ-002 (Presentation Layer)  
**Last Updated**: February 25, 2026
