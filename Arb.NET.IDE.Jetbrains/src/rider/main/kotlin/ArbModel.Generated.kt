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
    private val _getArbData: RdCall<Unit, List<ArbLocaleData>>
) : RdExtBase() {
    //companion
    
    companion object : ISerializersOwner {
        
        override fun registerSerializersCore(serializers: ISerializers)  {
            val classLoader = javaClass.classLoader
            serializers.register(LazyCompanionMarshaller(RdId(18097297820116), classLoader, "com.jetbrains.rd.ide.model.ArbEntry"))
            serializers.register(LazyCompanionMarshaller(RdId(1601623367371735298), classLoader, "com.jetbrains.rd.ide.model.ArbLocaleData"))
        }
        
        
        
        
        private val __ArbLocaleDataListSerializer = ArbLocaleData.list()
        
        const val serializationHash = -1039800800516086927L
        
    }
    override val serializersOwner: ISerializersOwner get() = ArbModel
    override val serializationHash: Long get() = ArbModel.serializationHash
    
    //fields
    val getArbData: IRdCall<Unit, List<ArbLocaleData>> get() = _getArbData
    //methods
    //initializer
    init {
        bindableChildren.add("getArbData" to _getArbData)
    }
    
    //secondary constructor
    internal constructor(
    ) : this(
        RdCall<Unit, List<ArbLocaleData>>(FrameworkMarshallers.Void, __ArbLocaleDataListSerializer)
    )
    
    //equals trait
    //hash code trait
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbModel (")
        printer.indent {
            print("getArbData = "); _getArbData.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    override fun deepClone(): ArbModel   {
        return ArbModel(
            _getArbData.deepClonePolymorphic()
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
 * #### Generated from [ArbModel.kt:18]
 */
data class ArbLocaleData (
    val locale: String,
    val entries: List<ArbEntry>
) : IPrintable {
    //companion
    
    companion object : IMarshaller<ArbLocaleData> {
        override val _type: KClass<ArbLocaleData> = ArbLocaleData::class
        override val id: RdId get() = RdId(1601623367371735298)
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): ArbLocaleData  {
            val locale = buffer.readString()
            val entries = buffer.readList { ArbEntry.read(ctx, buffer) }
            return ArbLocaleData(locale, entries)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: ArbLocaleData)  {
            buffer.writeString(value.locale)
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
        if (entries != other.entries) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int  {
        var __r = 0
        __r = __r*31 + locale.hashCode()
        __r = __r*31 + entries.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter)  {
        printer.println("ArbLocaleData (")
        printer.indent {
            print("locale = "); locale.print(printer); println()
            print("entries = "); entries.print(printer); println()
        }
        printer.print(")")
    }
    //deepClone
    //contexts
    //threading
}
