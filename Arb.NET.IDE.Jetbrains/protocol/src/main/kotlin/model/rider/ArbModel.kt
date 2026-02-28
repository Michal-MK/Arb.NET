package model.rider

import com.jetbrains.rd.generator.nova.*
import com.jetbrains.rd.generator.nova.PredefinedType.*
import com.jetbrains.rider.model.nova.ide.SolutionModel

// Extends Rider's SolutionModel so the model is scoped to the open solution.
@Suppress("unused")
object ArbModel : Ext(SolutionModel.Solution) {

    // A single localization entry: the message key and its translated value.
    val ArbEntry = structdef {
        field("key", string)
        field("value", string)
    }

    // All entries for one locale (language code, directory, file path, and list of entries).
    // The directory field allows the UI to group locales by folder.
    // The filePath field allows the UI to open the corresponding .arb file on header double-click.
    val ArbLocaleData = structdef {
        field("locale", string)
        field("directory", string)
        field("filePath", string)
        field("entries", immutableList(ArbEntry))
    }

    // Payload for updating a single entry value in one locale.
    val ArbEntryUpdate = structdef {
        field("directory", string)
        field("locale", string)
        field("key", string)
        field("value", string)
    }

    // Payload for renaming a key across all locale files in a directory.
    val ArbKeyRename = structdef {
        field("directory", string)
        field("oldKey", string)
        field("newKey", string)
    }

    // Payload for adding a new (empty) key to all locale files in a directory.
    val ArbNewKey = structdef {
        field("directory", string)
        field("key", string)
    }

    // Payload for removing a key from all locale files in a directory.
    val ArbRemoveKey = structdef {
        field("directory", string)
        field("key", string)
    }

    // Payload for adding a new locale file to a directory.
    val ArbNewLocale = structdef {
        field("directory", string)
        field("locale", string)
    }

    val AzureTranslationSettings = structdef {
        field("endpoint", string)
        field("deploymentName", string)
        field("apiKey", string)
        field("customPrompt", string)
        field("temperature", float)
    }

    val ArbTranslationItem = structdef {
        field("key", string)
        field("sourceText", string)
        field("description", string.nullable)
    }

    val ArbTranslateRequest = structdef {
        field("directory", string)
        field("settings", AzureTranslationSettings)
        field("sourceLocale", string)
        field("targetLocale", string)
        field("items", immutableList(ArbTranslationItem))
        field("provider", string)
    }

    val ArbTranslatedItem = structdef {
        field("key", string)
        field("translatedText", string)
    }

    val ArbTranslateResponse = structdef {
        field("success", bool)
        field("errorMessage", string.nullable)
        field("items", immutableList(ArbTranslatedItem))
    }

    // A single ARB key with its parametric status (parametric = becomes a method, not a property).
    // Also carries the description from the .arb metadata, the path to the template .arb file,
    // and the 0-based line number of the key in that file (for F12 / Go To Source navigation).
    val ArbKeyInfo = structdef {
        field("key", string)
        field("isParametric", bool)
        field("description", string.nullable)  // from @key metadata block in template .arb; null if absent
        field("arbFilePath", string.nullable)  // absolute path to the template .arb file; null if unavailable
        field("lineNumber", int)               // 0-based line index of the key; -1 if unknown
        field("xmlDoc", string.nullable)       // raw inner content of <summary> from generated Dispatcher; null if unavailable
    }

    init {
        // Call: Kotlin asks C# to scan the solution and return all ARB data.
        // Returns one ArbLocaleData per .arb file found.
        call("getArbData", void, immutableList(ArbLocaleData))

        // Call: Kotlin sends an updated entry value to C# to persist to disk.
        // Returns true if the entry was found and saved successfully.
        call("saveArbEntry", ArbEntryUpdate, bool)

        // Call: Kotlin asks C# to rename a key across all locale files.
        // Returns true if at least one file was updated.
        call("renameArbKey", ArbKeyRename, bool)

        // Call: Kotlin asks C# to add a new empty key to all locale files in a directory.
        // Returns true if at least one file was updated.
        call("addArbKey", ArbNewKey, bool)

        // Call: Kotlin asks C# to remove a key from all locale files in a directory.
        // Returns true if at least one file was updated.
        call("removeArbKey", ArbRemoveKey, bool)

        // Call: Kotlin asks C# to create a new locale file in a directory.
        // Returns true if the file was created successfully.
        call("addArbLocale", ArbNewLocale, bool)

        // Call: Kotlin asks C# backend to translate source texts using Azure OpenAI.
        // Returns per-key translated strings or an error message.
        call("translateArbEntries", ArbTranslateRequest, ArbTranslateResponse)

        // Call: Kotlin asks C# backend for all ARB keys from the AppLocale generated class
        // for a given project directory. Falls back to parsing the template .arb file if the
        // class is not yet generated. Returns key names with parametric flag.
        call("getArbKeys", string, immutableList(ArbKeyInfo))
    }
}
