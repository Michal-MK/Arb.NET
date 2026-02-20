@file:Suppress("EXPERIMENTAL_API_USAGE","EXPERIMENTAL_UNSIGNED_LITERALS","PackageDirectoryMismatch","UnusedImport","unused","LocalVariableName","CanBeVal","PropertyName","EnumEntryName","ClassName","ObjectPropertyName","UnnecessaryVariable","SpellCheckingInspection")
package com.jetbrains.rd.ide.model

import com.jetbrains.rd.framework.*
import com.jetbrains.rd.framework.base.*
import com.jetbrains.rd.framework.impl.*

import com.jetbrains.rd.util.lifetime.*
import com.jetbrains.rd.util.reactive.*
import com.jetbrains.rd.util.string.*
import com.jetbrains.rd.util.*
import kotlin.time.Duration
import kotlin.reflect.KClass
import kotlin.jvm.JvmStatic



/**
 * #### Generated from [ArbModel.kt:9]
 */
class ArbModel private constructor(
    private val _getArbData: RdCall<Unit, List<ArbLocaleData>>,
    private val _saveArbEntry: RdCall<ArbEntryUpdate, Boolean>,
    private val _renameArbKey: RdCall<ArbKeyRename, Boolean>,
    private val _addArbKey: RdCall<ArbNewKey, Boolean>
) : RdExtBase() {
    //companion
    
    companion object : ISerializersOwner {
        
        override fun registerSerializersCore(serializers: ISerializers)  {
            val classLoader = javaClass.classLoader
            serializers.register(LazyCompanionMarshaller(RdId(18097297820116), classLoader, "com.jetbrains.rd.ide.model.ArbEntry"))
            serializers.register(LazyCompanionMarshaller(RdId(1601623367371735298), classLoader, "com.jetbrains.rd.ide.model.ArbLocaleData"))
            serializers.register(LazyCompanionMarshaller(RdId(-5695656692253622339), classLoader, "com.jetbrains.rd.ide.model.ArbEntryUpdate"))
            serializers.register(LazyCompanionMarshaller(RdId(-1733504620339216673), classLoader, "com.jetbrains.rd.ide.model.ArbKeyRename"))
            serializers.register(LazyCompanionMarshaller(RdId(561016481825661), classLoader, "com.jetbrains.rd.ide.model.ArbNewKey"))
        }
        
        
        
        
        private val __ArbLocaleDataListSerializer = ArbLocaleData.list()
        
        const val serializationHash = -3393040445077500747L
        
    }
    override val serializersOwner: ISerializersOwner get() = ArbModel
    override val serializationHash: Long get() = ArbModel.serializationHash
    
    //fields
    val getArbData: IRdCall<Unit, List<ArbLocaleData>> get() = _getArbData
    val saveArbEntry: IRdCall<ArbEntryUpdate, Boolean> get() = _saveArbEntry
    val renameArbKey: IRdCall<ArbKeyRename, Boolean> get() = _renameArbKey
    val addArbKey: IRdCall<ArbNewKey, Boolean> get() = _addArbKey
    //methods
    //initializer
    init {
        bindableChildren.add("getArbData" to _getArbData)
        bindableChildren.add("saveArbEntry" to _saveArbEntry)
        bindableChildren.add("renameArbKey" to _renameArbKey)
        bindableChildren.add("addArbKey" to _addArbKey)
    }
    
    //secondary constructor
    internal constructor(
    ) : this(
        RdCall<Unit, List<ArbLocaleData>>(FrameworkMarshallers.Void, __ArbLocaleDataListSerializer),
        RdCall<ArbEntryUpdate, Boolean>(ArbEntryUpdate, FrameworkMarshallers.Bool),
        RdCall<ArbKeyRename, Boolean>(ArbKeyRename, FrameworkMarshallers.Bool),
        RdCall<ArbNewKey, Boolean>(ArbNewKey, FrameworkMarshallers.Bool)
    )
    
    //equals trait
    //hash code trait
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbModel (")
        printer.indent {
            print("getArbData = "); _getArbData.print(printer); println()
            print("saveArbEntry = "); _saveArbEntry.print(printer); println()
            print("renameArbKey = "); _renameArbKey.print(printer); println()
            print("addArbKey = "); _addArbKey.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    override fun deepClone(): ArbModel   {
        return ArbModel(
            _getArbData.deepClonePolymorphic(),
            _saveArbEntry.deepClonePolymorphic(),
            _renameArbKey.deepClonePolymorphic(),
            _addArbKey.deepClonePolymorphic()
        )
    }
    //contexts
    //threading
    override val extThreading: ExtThreadingKind get() = ExtThreadingKind.Default
}
val Solution.arbModel get() = getOrCreateExtension("arbModel", ::ArbModel)



/**
 * #### Generated from [ArbModel.kt:12]
 */
data class ArbEntry (
    val key: String,
    val value: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbEntry> {
        override val _type: KClass<ArbEntry> = ArbEntry::class
        override val id: RdId get() = RdId(18097297820116)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbEntry  {
            val key = buffer.readString()
            val value = buffer.readString()
            return ArbEntry(key, value)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbEntry)  {
            buffer.writeString(value.key)
            buffer.writeString(value.value)
        }
        
        
    }
    //fields
    //methods
    //initializer
    //secondary constructor
    //equals trait
    override fun equals(other: Any?): Boolean  {
        if (this === other) return true
        if (other == null || other::class != this::class) return false
        
        other as ArbEntry
        
        if (key != other.key) return false
        if (value != other.value) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + key.hashCode()
        __r = __r*31 + value.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbEntry (")
        printer.indent {
            print("key = "); key.print(printer); println()
            print("value = "); value.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:28]
 */
data class ArbEntryUpdate (
    val directory: String,
    val locale: String,
    val key: String,
    val value: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbEntryUpdate> {
        override val _type: KClass<ArbEntryUpdate> = ArbEntryUpdate::class
        override val id: RdId get() = RdId(-5695656692253622339)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbEntryUpdate  {
            val directory = buffer.readString()
            val locale = buffer.readString()
            val key = buffer.readString()
            val value = buffer.readString()
            return ArbEntryUpdate(directory, locale, key, value)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbEntryUpdate)  {
            buffer.writeString(value.directory)
            buffer.writeString(value.locale)
            buffer.writeString(value.key)
            buffer.writeString(value.value)
        }
        
        
    }
    //fields
    //methods
    //initializer
    //secondary constructor
    //equals trait
    override fun equals(other: Any?): Boolean  {
        if (this === other) return true
        if (other == null || other::class != this::class) return false
        
        other as ArbEntryUpdate
        
        if (directory != other.directory) return false
        if (locale != other.locale) return false
        if (key != other.key) return false
        if (value != other.value) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + directory.hashCode()
        __r = __r*31 + locale.hashCode()
        __r = __r*31 + key.hashCode()
        __r = __r*31 + value.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbEntryUpdate (")
        printer.indent {
            print("directory = "); directory.print(printer); println()
            print("locale = "); locale.print(printer); println()
            print("key = "); key.print(printer); println()
            print("value = "); value.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:36]
 */
data class ArbKeyRename (
    val directory: String,
    val oldKey: String,
    val newKey: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbKeyRename> {
        override val _type: KClass<ArbKeyRename> = ArbKeyRename::class
        override val id: RdId get() = RdId(-1733504620339216673)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbKeyRename  {
            val directory = buffer.readString()
            val oldKey = buffer.readString()
            val newKey = buffer.readString()
            return ArbKeyRename(directory, oldKey, newKey)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbKeyRename)  {
            buffer.writeString(value.directory)
            buffer.writeString(value.oldKey)
            buffer.writeString(value.newKey)
        }
        
        
    }
    //fields
    //methods
    //initializer
    //secondary constructor
    //equals trait
    override fun equals(other: Any?): Boolean  {
        if (this === other) return true
        if (other == null || other::class != this::class) return false
        
        other as ArbKeyRename
        
        if (directory != other.directory) return false
        if (oldKey != other.oldKey) return false
        if (newKey != other.newKey) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + directory.hashCode()
        __r = __r*31 + oldKey.hashCode()
        __r = __r*31 + newKey.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbKeyRename (")
        printer.indent {
            print("directory = "); directory.print(printer); println()
            print("oldKey = "); oldKey.print(printer); println()
            print("newKey = "); newKey.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:20]
 */
data class ArbLocaleData (
    val locale: String,
    val directory: String,
    val filePath: String,
    val entries: List<ArbEntry>
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbLocaleData> {
        override val _type: KClass<ArbLocaleData> = ArbLocaleData::class
        override val id: RdId get() = RdId(1601623367371735298)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbLocaleData  {
            val locale = buffer.readString()
            val directory = buffer.readString()
            val filePath = buffer.readString()
            val entries = buffer.readList { ArbEntry.read(ctx, buffer) }
            return ArbLocaleData(locale, directory, filePath, entries)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbLocaleData)  {
            buffer.writeString(value.locale)
            buffer.writeString(value.directory)
            buffer.writeString(value.filePath)
            buffer.writeList(value.entries) { v -> ArbEntry.write(ctx, buffer, v) }
        }
        
        
    }
    //fields
    //methods
    //initializer
    //secondary constructor
    //equals trait
    override fun equals(other: Any?): Boolean  {
        if (this === other) return true
        if (other == null || other::class != this::class) return false
        
        other as ArbLocaleData
        
        if (locale != other.locale) return false
        if (directory != other.directory) return false
        if (filePath != other.filePath) return false
        if (entries != other.entries) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + locale.hashCode()
        __r = __r*31 + directory.hashCode()
        __r = __r*31 + filePath.hashCode()
        __r = __r*31 + entries.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbLocaleData (")
        printer.indent {
            print("locale = "); locale.print(printer); println()
            print("directory = "); directory.print(printer); println()
            print("filePath = "); filePath.print(printer); println()
            print("entries = "); entries.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:43]
 */
data class ArbNewKey (
    val directory: String,
    val key: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbNewKey> {
        override val _type: KClass<ArbNewKey> = ArbNewKey::class
        override val id: RdId get() = RdId(561016481825661)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbNewKey  {
            val directory = buffer.readString()
            val key = buffer.readString()
            return ArbNewKey(directory, key)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbNewKey)  {
            buffer.writeString(value.directory)
            buffer.writeString(value.key)
        }
        
        
    }
    //fields
    //methods
    //initializer
    //secondary constructor
    //equals trait
    override fun equals(other: Any?): Boolean  {
        if (this === other) return true
        if (other == null || other::class != this::class) return false
        
        other as ArbNewKey
        
        if (directory != other.directory) return false
        if (key != other.key) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + directory.hashCode()
        __r = __r*31 + key.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbNewKey (")
        printer.indent {
            print("directory = "); directory.print(printer); println()
            print("key = "); key.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}
