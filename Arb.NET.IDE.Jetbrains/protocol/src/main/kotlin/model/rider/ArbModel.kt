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

    // All entries for one locale (language code + list of entries).
    val ArbLocaleData = structdef {
        field("locale", string)
        field("entries", immutableList(ArbEntry))
    }

    init {
        // Call: Kotlin asks C# to scan the solution and return all ARB data.
        // Returns one ArbLocaleData per .arb file found.
        call("getArbData", void, immutableList(ArbLocaleData))
    }
}
