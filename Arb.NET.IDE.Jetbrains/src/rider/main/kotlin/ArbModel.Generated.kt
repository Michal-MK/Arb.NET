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
    private val _getArbData: RdCall<String, List<ArbLocaleData>>,
    private val _saveArbEntry: RdCall<ArbEntryUpdate, Boolean>,
    private val _renameArbKey: RdCall<ArbKeyRename, Boolean>,
    private val _renameArbPlaceholder: RdCall<ArbPlaceholderRename, Boolean>,
    private val _addArbKey: RdCall<ArbNewKey, Boolean>,
    private val _removeArbKey: RdCall<ArbRemoveKey, Boolean>,
    private val _addArbLocale: RdCall<ArbNewLocale, Boolean>,
    private val _translateArbEntries: RdCall<ArbTranslateRequest, ArbTranslateResponse>,
    private val _previewArbCsvImport: RdCall<ArbCsvPreviewRequest, ArbCsvPreviewResponse>,
    private val _applyArbCsvImport: RdCall<ArbCsvImportRequest, ArbCsvImportResponse>,
    private val _exportArbCsv: RdCall<String, ArbCsvExportResponse>,
    private val _getArbKeys: RdCall<String, List<ArbKeyInfo>>,
    private val _openArbEditor: RdSignal<ArbOpenEditor>,
    private val _arbKeysChanged: RdSignal<String>
) : RdExtBase() {
    //companion
    
    companion object : ISerializersOwner {
        
        override fun registerSerializersCore(serializers: ISerializers)  {
            val classLoader = javaClass.classLoader
            serializers.register(LazyCompanionMarshaller(RdId(18097297820116), classLoader, "com.jetbrains.rd.ide.model.ArbEntry"))
            serializers.register(LazyCompanionMarshaller(RdId(1601623367371735298), classLoader, "com.jetbrains.rd.ide.model.ArbLocaleData"))
            serializers.register(LazyCompanionMarshaller(RdId(-5695656692253622339), classLoader, "com.jetbrains.rd.ide.model.ArbEntryUpdate"))
            serializers.register(LazyCompanionMarshaller(RdId(-1733504620339216673), classLoader, "com.jetbrains.rd.ide.model.ArbKeyRename"))
            serializers.register(LazyCompanionMarshaller(RdId(8903959258467108083), classLoader, "com.jetbrains.rd.ide.model.ArbPlaceholderRename"))
            serializers.register(LazyCompanionMarshaller(RdId(561016481825661), classLoader, "com.jetbrains.rd.ide.model.ArbNewKey"))
            serializers.register(LazyCompanionMarshaller(RdId(-1733498659906343011), classLoader, "com.jetbrains.rd.ide.model.ArbRemoveKey"))
            serializers.register(LazyCompanionMarshaller(RdId(-1733502063603979140), classLoader, "com.jetbrains.rd.ide.model.ArbNewLocale"))
            serializers.register(LazyCompanionMarshaller(RdId(140704772302623256), classLoader, "com.jetbrains.rd.ide.model.AzureTranslationSettings"))
            serializers.register(LazyCompanionMarshaller(RdId(-6608617628705468346), classLoader, "com.jetbrains.rd.ide.model.ArbTranslationItem"))
            serializers.register(LazyCompanionMarshaller(RdId(-1952961815073205569), classLoader, "com.jetbrains.rd.ide.model.ArbTranslateRequest"))
            serializers.register(LazyCompanionMarshaller(RdId(-6163743818377381401), classLoader, "com.jetbrains.rd.ide.model.ArbTranslatedItem"))
            serializers.register(LazyCompanionMarshaller(RdId(-5201584046087783919), classLoader, "com.jetbrains.rd.ide.model.ArbTranslateResponse"))
            serializers.register(LazyCompanionMarshaller(RdId(1021191144752783279), classLoader, "com.jetbrains.rd.ide.model.ArbCsvPreviewRequest"))
            serializers.register(LazyCompanionMarshaller(RdId(561016179811538), classLoader, "com.jetbrains.rd.ide.model.ArbCsvRow"))
            serializers.register(LazyCompanionMarshaller(RdId(-5236562660029887711), classLoader, "com.jetbrains.rd.ide.model.ArbCsvPreviewResponse"))
            serializers.register(LazyCompanionMarshaller(RdId(3921998836132476962), classLoader, "com.jetbrains.rd.ide.model.ArbCsvImportRequest"))
            serializers.register(LazyCompanionMarshaller(RdId(-7545244595807141618), classLoader, "com.jetbrains.rd.ide.model.ArbCsvImportResponse"))
            serializers.register(LazyCompanionMarshaller(RdId(-4317418795365506531), classLoader, "com.jetbrains.rd.ide.model.ArbCsvExportResponse"))
            serializers.register(LazyCompanionMarshaller(RdId(17391508275880079), classLoader, "com.jetbrains.rd.ide.model.ArbKeyInfo"))
            serializers.register(LazyCompanionMarshaller(RdId(1601703604575690805), classLoader, "com.jetbrains.rd.ide.model.ArbOpenEditor"))
        }
        
        
        
        
        private val __ArbLocaleDataListSerializer = ArbLocaleData.list()
        private val __ArbKeyInfoListSerializer = ArbKeyInfo.list()
        
        const val serializationHash = -7749148818641045260L
        
    }
    override val serializersOwner: ISerializersOwner get() = ArbModel
    override val serializationHash: Long get() = ArbModel.serializationHash
    
    //fields
    val getArbData: IRdCall<String, List<ArbLocaleData>> get() = _getArbData
    val saveArbEntry: IRdCall<ArbEntryUpdate, Boolean> get() = _saveArbEntry
    val renameArbKey: IRdCall<ArbKeyRename, Boolean> get() = _renameArbKey
    val renameArbPlaceholder: IRdCall<ArbPlaceholderRename, Boolean> get() = _renameArbPlaceholder
    val addArbKey: IRdCall<ArbNewKey, Boolean> get() = _addArbKey
    val removeArbKey: IRdCall<ArbRemoveKey, Boolean> get() = _removeArbKey
    val addArbLocale: IRdCall<ArbNewLocale, Boolean> get() = _addArbLocale
    val translateArbEntries: IRdCall<ArbTranslateRequest, ArbTranslateResponse> get() = _translateArbEntries
    val previewArbCsvImport: IRdCall<ArbCsvPreviewRequest, ArbCsvPreviewResponse> get() = _previewArbCsvImport
    val applyArbCsvImport: IRdCall<ArbCsvImportRequest, ArbCsvImportResponse> get() = _applyArbCsvImport
    val exportArbCsv: IRdCall<String, ArbCsvExportResponse> get() = _exportArbCsv
    val getArbKeys: IRdCall<String, List<ArbKeyInfo>> get() = _getArbKeys
    val openArbEditor: ISignal<ArbOpenEditor> get() = _openArbEditor
    val arbKeysChanged: ISignal<String> get() = _arbKeysChanged
    //methods
    //initializer
    init {
        bindableChildren.add("getArbData" to _getArbData)
        bindableChildren.add("saveArbEntry" to _saveArbEntry)
        bindableChildren.add("renameArbKey" to _renameArbKey)
        bindableChildren.add("renameArbPlaceholder" to _renameArbPlaceholder)
        bindableChildren.add("addArbKey" to _addArbKey)
        bindableChildren.add("removeArbKey" to _removeArbKey)
        bindableChildren.add("addArbLocale" to _addArbLocale)
        bindableChildren.add("translateArbEntries" to _translateArbEntries)
        bindableChildren.add("previewArbCsvImport" to _previewArbCsvImport)
        bindableChildren.add("applyArbCsvImport" to _applyArbCsvImport)
        bindableChildren.add("exportArbCsv" to _exportArbCsv)
        bindableChildren.add("getArbKeys" to _getArbKeys)
        bindableChildren.add("openArbEditor" to _openArbEditor)
        bindableChildren.add("arbKeysChanged" to _arbKeysChanged)
    }
    
    //secondary constructor
    internal constructor(
    ) : this(
        RdCall<String, List<ArbLocaleData>>(FrameworkMarshallers.String, __ArbLocaleDataListSerializer),
        RdCall<ArbEntryUpdate, Boolean>(ArbEntryUpdate, FrameworkMarshallers.Bool),
        RdCall<ArbKeyRename, Boolean>(ArbKeyRename, FrameworkMarshallers.Bool),
        RdCall<ArbPlaceholderRename, Boolean>(ArbPlaceholderRename, FrameworkMarshallers.Bool),
        RdCall<ArbNewKey, Boolean>(ArbNewKey, FrameworkMarshallers.Bool),
        RdCall<ArbRemoveKey, Boolean>(ArbRemoveKey, FrameworkMarshallers.Bool),
        RdCall<ArbNewLocale, Boolean>(ArbNewLocale, FrameworkMarshallers.Bool),
        RdCall<ArbTranslateRequest, ArbTranslateResponse>(ArbTranslateRequest, ArbTranslateResponse),
        RdCall<ArbCsvPreviewRequest, ArbCsvPreviewResponse>(ArbCsvPreviewRequest, ArbCsvPreviewResponse),
        RdCall<ArbCsvImportRequest, ArbCsvImportResponse>(ArbCsvImportRequest, ArbCsvImportResponse),
        RdCall<String, ArbCsvExportResponse>(FrameworkMarshallers.String, ArbCsvExportResponse),
        RdCall<String, List<ArbKeyInfo>>(FrameworkMarshallers.String, __ArbKeyInfoListSerializer),
        RdSignal<ArbOpenEditor>(ArbOpenEditor),
        RdSignal<String>(FrameworkMarshallers.String)
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
            print("renameArbPlaceholder = "); _renameArbPlaceholder.print(printer); println()
            print("addArbKey = "); _addArbKey.print(printer); println()
            print("removeArbKey = "); _removeArbKey.print(printer); println()
            print("addArbLocale = "); _addArbLocale.print(printer); println()
            print("translateArbEntries = "); _translateArbEntries.print(printer); println()
            print("previewArbCsvImport = "); _previewArbCsvImport.print(printer); println()
            print("applyArbCsvImport = "); _applyArbCsvImport.print(printer); println()
            print("exportArbCsv = "); _exportArbCsv.print(printer); println()
            print("getArbKeys = "); _getArbKeys.print(printer); println()
            print("openArbEditor = "); _openArbEditor.print(printer); println()
            print("arbKeysChanged = "); _arbKeysChanged.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    override fun deepClone(): ArbModel   {
        return ArbModel(
            _getArbData.deepClonePolymorphic(),
            _saveArbEntry.deepClonePolymorphic(),
            _renameArbKey.deepClonePolymorphic(),
            _renameArbPlaceholder.deepClonePolymorphic(),
            _addArbKey.deepClonePolymorphic(),
            _removeArbKey.deepClonePolymorphic(),
            _addArbLocale.deepClonePolymorphic(),
            _translateArbEntries.deepClonePolymorphic(),
            _previewArbCsvImport.deepClonePolymorphic(),
            _applyArbCsvImport.deepClonePolymorphic(),
            _exportArbCsv.deepClonePolymorphic(),
            _getArbKeys.deepClonePolymorphic(),
            _openArbEditor.deepClonePolymorphic(),
            _arbKeysChanged.deepClonePolymorphic()
        )
    }
    //contexts
    //threading
    override val extThreading: ExtThreadingKind get() = ExtThreadingKind.Default
}
val Solution.arbModel get() = getOrCreateExtension("arbModel", ::ArbModel)



/**
 * #### Generated from [ArbModel.kt:136]
 */
data class ArbCsvExportResponse (
    val success: Boolean,
    val errorMessage: String?,
    val csvContent: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbCsvExportResponse> {
        override val _type: KClass<ArbCsvExportResponse> = ArbCsvExportResponse::class
        override val id: RdId get() = RdId(-4317418795365506531)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbCsvExportResponse  {
            val success = buffer.readBool()
            val errorMessage = buffer.readNullable { buffer.readString() }
            val csvContent = buffer.readString()
            return ArbCsvExportResponse(success, errorMessage, csvContent)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbCsvExportResponse)  {
            buffer.writeBool(value.success)
            buffer.writeNullable(value.errorMessage) { buffer.writeString(it) }
            buffer.writeString(value.csvContent)
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
        
        other as ArbCsvExportResponse
        
        if (success != other.success) return false
        if (errorMessage != other.errorMessage) return false
        if (csvContent != other.csvContent) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + success.hashCode()
        __r = __r*31 + if (errorMessage != null) errorMessage.hashCode() else 0
        __r = __r*31 + csvContent.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbCsvExportResponse (")
        printer.indent {
            print("success = "); success.print(printer); println()
            print("errorMessage = "); errorMessage.print(printer); println()
            print("csvContent = "); csvContent.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:121]
 */
data class ArbCsvImportRequest (
    val directory: String,
    val csvContent: String,
    val mappings: List<String>,
    val mode: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbCsvImportRequest> {
        override val _type: KClass<ArbCsvImportRequest> = ArbCsvImportRequest::class
        override val id: RdId get() = RdId(3921998836132476962)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbCsvImportRequest  {
            val directory = buffer.readString()
            val csvContent = buffer.readString()
            val mappings = buffer.readList { buffer.readString() }
            val mode = buffer.readString()
            return ArbCsvImportRequest(directory, csvContent, mappings, mode)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbCsvImportRequest)  {
            buffer.writeString(value.directory)
            buffer.writeString(value.csvContent)
            buffer.writeList(value.mappings) { v -> buffer.writeString(v) }
            buffer.writeString(value.mode)
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
        
        other as ArbCsvImportRequest
        
        if (directory != other.directory) return false
        if (csvContent != other.csvContent) return false
        if (mappings != other.mappings) return false
        if (mode != other.mode) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + directory.hashCode()
        __r = __r*31 + csvContent.hashCode()
        __r = __r*31 + mappings.hashCode()
        __r = __r*31 + mode.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbCsvImportRequest (")
        printer.indent {
            print("directory = "); directory.print(printer); println()
            print("csvContent = "); csvContent.print(printer); println()
            print("mappings = "); mappings.print(printer); println()
            print("mode = "); mode.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:128]
 */
data class ArbCsvImportResponse (
    val success: Boolean,
    val errorMessage: String?,
    val importedKeyCount: Int,
    val affectedLocaleCount: Int,
    val createdLocaleCount: Int
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbCsvImportResponse> {
        override val _type: KClass<ArbCsvImportResponse> = ArbCsvImportResponse::class
        override val id: RdId get() = RdId(-7545244595807141618)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbCsvImportResponse  {
            val success = buffer.readBool()
            val errorMessage = buffer.readNullable { buffer.readString() }
            val importedKeyCount = buffer.readInt()
            val affectedLocaleCount = buffer.readInt()
            val createdLocaleCount = buffer.readInt()
            return ArbCsvImportResponse(success, errorMessage, importedKeyCount, affectedLocaleCount, createdLocaleCount)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbCsvImportResponse)  {
            buffer.writeBool(value.success)
            buffer.writeNullable(value.errorMessage) { buffer.writeString(it) }
            buffer.writeInt(value.importedKeyCount)
            buffer.writeInt(value.affectedLocaleCount)
            buffer.writeInt(value.createdLocaleCount)
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
        
        other as ArbCsvImportResponse
        
        if (success != other.success) return false
        if (errorMessage != other.errorMessage) return false
        if (importedKeyCount != other.importedKeyCount) return false
        if (affectedLocaleCount != other.affectedLocaleCount) return false
        if (createdLocaleCount != other.createdLocaleCount) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + success.hashCode()
        __r = __r*31 + if (errorMessage != null) errorMessage.hashCode() else 0
        __r = __r*31 + importedKeyCount.hashCode()
        __r = __r*31 + affectedLocaleCount.hashCode()
        __r = __r*31 + createdLocaleCount.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbCsvImportResponse (")
        printer.indent {
            print("success = "); success.print(printer); println()
            print("errorMessage = "); errorMessage.print(printer); println()
            print("importedKeyCount = "); importedKeyCount.print(printer); println()
            print("affectedLocaleCount = "); affectedLocaleCount.print(printer); println()
            print("createdLocaleCount = "); createdLocaleCount.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:102]
 */
data class ArbCsvPreviewRequest (
    val directory: String,
    val csvContent: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbCsvPreviewRequest> {
        override val _type: KClass<ArbCsvPreviewRequest> = ArbCsvPreviewRequest::class
        override val id: RdId get() = RdId(1021191144752783279)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbCsvPreviewRequest  {
            val directory = buffer.readString()
            val csvContent = buffer.readString()
            return ArbCsvPreviewRequest(directory, csvContent)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbCsvPreviewRequest)  {
            buffer.writeString(value.directory)
            buffer.writeString(value.csvContent)
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
        
        other as ArbCsvPreviewRequest
        
        if (directory != other.directory) return false
        if (csvContent != other.csvContent) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + directory.hashCode()
        __r = __r*31 + csvContent.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbCsvPreviewRequest (")
        printer.indent {
            print("directory = "); directory.print(printer); println()
            print("csvContent = "); csvContent.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:111]
 */
data class ArbCsvPreviewResponse (
    val success: Boolean,
    val errorMessage: String?,
    val headers: List<String>,
    val rows: List<ArbCsvRow>,
    val suggestedMappings: List<String>,
    val availableLocaleMappings: List<String>,
    val defaultLocale: String?
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbCsvPreviewResponse> {
        override val _type: KClass<ArbCsvPreviewResponse> = ArbCsvPreviewResponse::class
        override val id: RdId get() = RdId(-5236562660029887711)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbCsvPreviewResponse  {
            val success = buffer.readBool()
            val errorMessage = buffer.readNullable { buffer.readString() }
            val headers = buffer.readList { buffer.readString() }
            val rows = buffer.readList { ArbCsvRow.read(ctx, buffer) }
            val suggestedMappings = buffer.readList { buffer.readString() }
            val availableLocaleMappings = buffer.readList { buffer.readString() }
            val defaultLocale = buffer.readNullable { buffer.readString() }
            return ArbCsvPreviewResponse(success, errorMessage, headers, rows, suggestedMappings, availableLocaleMappings, defaultLocale)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbCsvPreviewResponse)  {
            buffer.writeBool(value.success)
            buffer.writeNullable(value.errorMessage) { buffer.writeString(it) }
            buffer.writeList(value.headers) { v -> buffer.writeString(v) }
            buffer.writeList(value.rows) { v -> ArbCsvRow.write(ctx, buffer, v) }
            buffer.writeList(value.suggestedMappings) { v -> buffer.writeString(v) }
            buffer.writeList(value.availableLocaleMappings) { v -> buffer.writeString(v) }
            buffer.writeNullable(value.defaultLocale) { buffer.writeString(it) }
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
        
        other as ArbCsvPreviewResponse
        
        if (success != other.success) return false
        if (errorMessage != other.errorMessage) return false
        if (headers != other.headers) return false
        if (rows != other.rows) return false
        if (suggestedMappings != other.suggestedMappings) return false
        if (availableLocaleMappings != other.availableLocaleMappings) return false
        if (defaultLocale != other.defaultLocale) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + success.hashCode()
        __r = __r*31 + if (errorMessage != null) errorMessage.hashCode() else 0
        __r = __r*31 + headers.hashCode()
        __r = __r*31 + rows.hashCode()
        __r = __r*31 + suggestedMappings.hashCode()
        __r = __r*31 + availableLocaleMappings.hashCode()
        __r = __r*31 + if (defaultLocale != null) defaultLocale.hashCode() else 0
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbCsvPreviewResponse (")
        printer.indent {
            print("success = "); success.print(printer); println()
            print("errorMessage = "); errorMessage.print(printer); println()
            print("headers = "); headers.print(printer); println()
            print("rows = "); rows.print(printer); println()
            print("suggestedMappings = "); suggestedMappings.print(printer); println()
            print("availableLocaleMappings = "); availableLocaleMappings.print(printer); println()
            print("defaultLocale = "); defaultLocale.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:107]
 */
data class ArbCsvRow (
    val cells: List<String>
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbCsvRow> {
        override val _type: KClass<ArbCsvRow> = ArbCsvRow::class
        override val id: RdId get() = RdId(561016179811538)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbCsvRow  {
            val cells = buffer.readList { buffer.readString() }
            return ArbCsvRow(cells)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbCsvRow)  {
            buffer.writeList(value.cells) { v -> buffer.writeString(v) }
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
        
        other as ArbCsvRow
        
        if (cells != other.cells) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + cells.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbCsvRow (")
        printer.indent {
            print("cells = "); cells.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


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
 * #### Generated from [ArbModel.kt:145]
 */
data class ArbKeyInfo (
    val key: String,
    val isParametric: Boolean,
    val description: String?,
    val arbFilePath: String?,
    val lineNumber: Int,
    val xmlDoc: String?
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbKeyInfo> {
        override val _type: KClass<ArbKeyInfo> = ArbKeyInfo::class
        override val id: RdId get() = RdId(17391508275880079)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbKeyInfo  {
            val key = buffer.readString()
            val isParametric = buffer.readBool()
            val description = buffer.readNullable { buffer.readString() }
            val arbFilePath = buffer.readNullable { buffer.readString() }
            val lineNumber = buffer.readInt()
            val xmlDoc = buffer.readNullable { buffer.readString() }
            return ArbKeyInfo(key, isParametric, description, arbFilePath, lineNumber, xmlDoc)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbKeyInfo)  {
            buffer.writeString(value.key)
            buffer.writeBool(value.isParametric)
            buffer.writeNullable(value.description) { buffer.writeString(it) }
            buffer.writeNullable(value.arbFilePath) { buffer.writeString(it) }
            buffer.writeInt(value.lineNumber)
            buffer.writeNullable(value.xmlDoc) { buffer.writeString(it) }
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
        
        other as ArbKeyInfo
        
        if (key != other.key) return false
        if (isParametric != other.isParametric) return false
        if (description != other.description) return false
        if (arbFilePath != other.arbFilePath) return false
        if (lineNumber != other.lineNumber) return false
        if (xmlDoc != other.xmlDoc) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + key.hashCode()
        __r = __r*31 + isParametric.hashCode()
        __r = __r*31 + if (description != null) description.hashCode() else 0
        __r = __r*31 + if (arbFilePath != null) arbFilePath.hashCode() else 0
        __r = __r*31 + lineNumber.hashCode()
        __r = __r*31 + if (xmlDoc != null) xmlDoc.hashCode() else 0
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbKeyInfo (")
        printer.indent {
            print("key = "); key.print(printer); println()
            print("isParametric = "); isParametric.print(printer); println()
            print("description = "); description.print(printer); println()
            print("arbFilePath = "); arbFilePath.print(printer); println()
            print("lineNumber = "); lineNumber.print(printer); println()
            print("xmlDoc = "); xmlDoc.print(printer); println()
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
 * #### Generated from [ArbModel.kt:51]
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


/**
 * #### Generated from [ArbModel.kt:63]
 */
data class ArbNewLocale (
    val directory: String,
    val locale: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbNewLocale> {
        override val _type: KClass<ArbNewLocale> = ArbNewLocale::class
        override val id: RdId get() = RdId(-1733502063603979140)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbNewLocale  {
            val directory = buffer.readString()
            val locale = buffer.readString()
            return ArbNewLocale(directory, locale)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbNewLocale)  {
            buffer.writeString(value.directory)
            buffer.writeString(value.locale)
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
        
        other as ArbNewLocale
        
        if (directory != other.directory) return false
        if (locale != other.locale) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + directory.hashCode()
        __r = __r*31 + locale.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbNewLocale (")
        printer.indent {
            print("directory = "); directory.print(printer); println()
            print("locale = "); locale.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:155]
 */
data class ArbOpenEditor (
    val arbDir: String,
    val keyFilter: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbOpenEditor> {
        override val _type: KClass<ArbOpenEditor> = ArbOpenEditor::class
        override val id: RdId get() = RdId(1601703604575690805)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbOpenEditor  {
            val arbDir = buffer.readString()
            val keyFilter = buffer.readString()
            return ArbOpenEditor(arbDir, keyFilter)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbOpenEditor)  {
            buffer.writeString(value.arbDir)
            buffer.writeString(value.keyFilter)
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
        
        other as ArbOpenEditor
        
        if (arbDir != other.arbDir) return false
        if (keyFilter != other.keyFilter) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + arbDir.hashCode()
        __r = __r*31 + keyFilter.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbOpenEditor (")
        printer.indent {
            print("arbDir = "); arbDir.print(printer); println()
            print("keyFilter = "); keyFilter.print(printer); println()
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
data class ArbPlaceholderRename (
    val directory: String,
    val key: String,
    val oldName: String,
    val newName: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbPlaceholderRename> {
        override val _type: KClass<ArbPlaceholderRename> = ArbPlaceholderRename::class
        override val id: RdId get() = RdId(8903959258467108083)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbPlaceholderRename  {
            val directory = buffer.readString()
            val key = buffer.readString()
            val oldName = buffer.readString()
            val newName = buffer.readString()
            return ArbPlaceholderRename(directory, key, oldName, newName)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbPlaceholderRename)  {
            buffer.writeString(value.directory)
            buffer.writeString(value.key)
            buffer.writeString(value.oldName)
            buffer.writeString(value.newName)
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
        
        other as ArbPlaceholderRename
        
        if (directory != other.directory) return false
        if (key != other.key) return false
        if (oldName != other.oldName) return false
        if (newName != other.newName) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + directory.hashCode()
        __r = __r*31 + key.hashCode()
        __r = __r*31 + oldName.hashCode()
        __r = __r*31 + newName.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbPlaceholderRename (")
        printer.indent {
            print("directory = "); directory.print(printer); println()
            print("key = "); key.print(printer); println()
            print("oldName = "); oldName.print(printer); println()
            print("newName = "); newName.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:57]
 */
data class ArbRemoveKey (
    val directory: String,
    val key: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbRemoveKey> {
        override val _type: KClass<ArbRemoveKey> = ArbRemoveKey::class
        override val id: RdId get() = RdId(-1733498659906343011)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbRemoveKey  {
            val directory = buffer.readString()
            val key = buffer.readString()
            return ArbRemoveKey(directory, key)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbRemoveKey)  {
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
        
        other as ArbRemoveKey
        
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
        printer.println("ArbRemoveKey (")
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


/**
 * #### Generated from [ArbModel.kt:82]
 */
data class ArbTranslateRequest (
    val directory: String,
    val settings: AzureTranslationSettings,
    val sourceLocale: String,
    val targetLocale: String,
    val items: List<ArbTranslationItem>,
    val provider: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbTranslateRequest> {
        override val _type: KClass<ArbTranslateRequest> = ArbTranslateRequest::class
        override val id: RdId get() = RdId(-1952961815073205569)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbTranslateRequest  {
            val directory = buffer.readString()
            val settings = AzureTranslationSettings.read(ctx, buffer)
            val sourceLocale = buffer.readString()
            val targetLocale = buffer.readString()
            val items = buffer.readList { ArbTranslationItem.read(ctx, buffer) }
            val provider = buffer.readString()
            return ArbTranslateRequest(directory, settings, sourceLocale, targetLocale, items, provider)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbTranslateRequest)  {
            buffer.writeString(value.directory)
            AzureTranslationSettings.write(ctx, buffer, value.settings)
            buffer.writeString(value.sourceLocale)
            buffer.writeString(value.targetLocale)
            buffer.writeList(value.items) { v -> ArbTranslationItem.write(ctx, buffer, v) }
            buffer.writeString(value.provider)
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
        
        other as ArbTranslateRequest
        
        if (directory != other.directory) return false
        if (settings != other.settings) return false
        if (sourceLocale != other.sourceLocale) return false
        if (targetLocale != other.targetLocale) return false
        if (items != other.items) return false
        if (provider != other.provider) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + directory.hashCode()
        __r = __r*31 + settings.hashCode()
        __r = __r*31 + sourceLocale.hashCode()
        __r = __r*31 + targetLocale.hashCode()
        __r = __r*31 + items.hashCode()
        __r = __r*31 + provider.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbTranslateRequest (")
        printer.indent {
            print("directory = "); directory.print(printer); println()
            print("settings = "); settings.print(printer); println()
            print("sourceLocale = "); sourceLocale.print(printer); println()
            print("targetLocale = "); targetLocale.print(printer); println()
            print("items = "); items.print(printer); println()
            print("provider = "); provider.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:96]
 */
data class ArbTranslateResponse (
    val success: Boolean,
    val errorMessage: String?,
    val items: List<ArbTranslatedItem>
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbTranslateResponse> {
        override val _type: KClass<ArbTranslateResponse> = ArbTranslateResponse::class
        override val id: RdId get() = RdId(-5201584046087783919)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbTranslateResponse  {
            val success = buffer.readBool()
            val errorMessage = buffer.readNullable { buffer.readString() }
            val items = buffer.readList { ArbTranslatedItem.read(ctx, buffer) }
            return ArbTranslateResponse(success, errorMessage, items)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbTranslateResponse)  {
            buffer.writeBool(value.success)
            buffer.writeNullable(value.errorMessage) { buffer.writeString(it) }
            buffer.writeList(value.items) { v -> ArbTranslatedItem.write(ctx, buffer, v) }
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
        
        other as ArbTranslateResponse
        
        if (success != other.success) return false
        if (errorMessage != other.errorMessage) return false
        if (items != other.items) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + success.hashCode()
        __r = __r*31 + if (errorMessage != null) errorMessage.hashCode() else 0
        __r = __r*31 + items.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbTranslateResponse (")
        printer.indent {
            print("success = "); success.print(printer); println()
            print("errorMessage = "); errorMessage.print(printer); println()
            print("items = "); items.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:91]
 */
data class ArbTranslatedItem (
    val key: String,
    val translatedText: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbTranslatedItem> {
        override val _type: KClass<ArbTranslatedItem> = ArbTranslatedItem::class
        override val id: RdId get() = RdId(-6163743818377381401)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbTranslatedItem  {
            val key = buffer.readString()
            val translatedText = buffer.readString()
            return ArbTranslatedItem(key, translatedText)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbTranslatedItem)  {
            buffer.writeString(value.key)
            buffer.writeString(value.translatedText)
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
        
        other as ArbTranslatedItem
        
        if (key != other.key) return false
        if (translatedText != other.translatedText) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + key.hashCode()
        __r = __r*31 + translatedText.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbTranslatedItem (")
        printer.indent {
            print("key = "); key.print(printer); println()
            print("translatedText = "); translatedText.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:76]
 */
data class ArbTranslationItem (
    val key: String,
    val sourceText: String,
    val description: String?
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbTranslationItem> {
        override val _type: KClass<ArbTranslationItem> = ArbTranslationItem::class
        override val id: RdId get() = RdId(-6608617628705468346)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbTranslationItem  {
            val key = buffer.readString()
            val sourceText = buffer.readString()
            val description = buffer.readNullable { buffer.readString() }
            return ArbTranslationItem(key, sourceText, description)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbTranslationItem)  {
            buffer.writeString(value.key)
            buffer.writeString(value.sourceText)
            buffer.writeNullable(value.description) { buffer.writeString(it) }
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
        
        other as ArbTranslationItem
        
        if (key != other.key) return false
        if (sourceText != other.sourceText) return false
        if (description != other.description) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + key.hashCode()
        __r = __r*31 + sourceText.hashCode()
        __r = __r*31 + if (description != null) description.hashCode() else 0
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbTranslationItem (")
        printer.indent {
            print("key = "); key.print(printer); println()
            print("sourceText = "); sourceText.print(printer); println()
            print("description = "); description.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}


/**
 * #### Generated from [ArbModel.kt:68]
 */
data class AzureTranslationSettings (
    val endpoint: String,
    val deploymentName: String,
    val apiKey: String,
    val customPrompt: String,
    val temperature: Float
) : IPrintable {
    //companion
    
    companion object : IMarshaller<AzureTranslationSettings> {
        override val _type: KClass<AzureTranslationSettings> = AzureTranslationSettings::class
        override val id: RdId get() = RdId(140704772302623256)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): AzureTranslationSettings  {
            val endpoint = buffer.readString()
            val deploymentName = buffer.readString()
            val apiKey = buffer.readString()
            val customPrompt = buffer.readString()
            val temperature = buffer.readFloat()
            return AzureTranslationSettings(endpoint, deploymentName, apiKey, customPrompt, temperature)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: AzureTranslationSettings)  {
            buffer.writeString(value.endpoint)
            buffer.writeString(value.deploymentName)
            buffer.writeString(value.apiKey)
            buffer.writeString(value.customPrompt)
            buffer.writeFloat(value.temperature)
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
        
        other as AzureTranslationSettings
        
        if (endpoint != other.endpoint) return false
        if (deploymentName != other.deploymentName) return false
        if (apiKey != other.apiKey) return false
        if (customPrompt != other.customPrompt) return false
        if (temperature != other.temperature) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + endpoint.hashCode()
        __r = __r*31 + deploymentName.hashCode()
        __r = __r*31 + apiKey.hashCode()
        __r = __r*31 + customPrompt.hashCode()
        __r = __r*31 + temperature.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("AzureTranslationSettings (")
        printer.indent {
            print("endpoint = "); endpoint.print(printer); println()
            print("deploymentName = "); deploymentName.print(printer); println()
            print("apiKey = "); apiKey.print(printer); println()
            print("customPrompt = "); customPrompt.print(printer); println()
            print("temperature = "); temperature.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}
