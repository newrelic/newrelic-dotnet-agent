#pragma once
#include <stdint.h>

//
// The following is all from the cor header files.  We don't want to depend on windows.h so we copy most of it over here and remove dependencies
//

namespace NewRelic { namespace Profiler
{
	typedef enum CorILMethodFlags
	{
		CorILMethod_InitLocals   = 0x0010, // call default constructor on all local vars
		CorILMethod_MoreSects    = 0x0008, // there is another attribute after this one

		CorILMethod_CompressedIL = 0x0040, // Not used.  

		// Indicates the format for the COR_ILMETHOD header
		CorILMethod_FormatShift  = 3,
		CorILMethod_FormatMask   = ((1 << CorILMethod_FormatShift) - 1),
		CorILMethod_TinyFormat   = 0x0002, // use this code if the code size is even
		CorILMethod_SmallFormat  = 0x0000,
		CorILMethod_FatFormat    = 0x0003,
		CorILMethod_TinyFormat1  = 0x0006, // use this code if the code size is odd
	} CorILMethodFlags;

	typedef enum CorILMethodSect                    // codes that identify attributes
	{
		CorILMethod_Sect_Reserved    = 0,
		CorILMethod_Sect_EHTable     = 1,
		CorILMethod_Sect_OptILTable  = 2,

		CorILMethod_Sect_KindMask    = 0x3F,        // The mask for decoding the type code
		CorILMethod_Sect_FatFormat   = 0x40,        // fat format
		CorILMethod_Sect_MoreSects   = 0x80,        // there is another attribute after this one
	} CorILMethodSect;

	typedef enum CorExceptionFlag                   // definitions for the Flags field below (for both big and small)
	{
		COR_ILEXCEPTION_CLAUSE_NONE,                // This is a typed handler
		COR_ILEXCEPTION_CLAUSE_OFFSETLEN  = 0x0000, // Deprecated
		COR_ILEXCEPTION_CLAUSE_DEPRECATED = 0x0000, // Deprecated
		COR_ILEXCEPTION_CLAUSE_FILTER     = 0x0001, // If this bit is on, then this EH entry is for a filter
		COR_ILEXCEPTION_CLAUSE_FINALLY    = 0x0002, // This clause is a finally clause
		COR_ILEXCEPTION_CLAUSE_FAULT      = 0x0004, // Fault clause (finally that is called on exception only)
		COR_ILEXCEPTION_CLAUSE_DUPLICATED = 0x0008, // duplicated clause. This clause was duplicated to a funclet which was pulled out of line
	} CorExceptionFlag;

	typedef enum CorElementType
	{
		ELEMENT_TYPE_END            = 0x00,
		ELEMENT_TYPE_VOID           = 0x01,
		ELEMENT_TYPE_BOOLEAN        = 0x02,
		ELEMENT_TYPE_CHAR           = 0x03,
		ELEMENT_TYPE_I1             = 0x04,
		ELEMENT_TYPE_U1             = 0x05,
		ELEMENT_TYPE_I2             = 0x06,
		ELEMENT_TYPE_U2             = 0x07,
		ELEMENT_TYPE_I4             = 0x08,
		ELEMENT_TYPE_U4             = 0x09,
		ELEMENT_TYPE_I8             = 0x0a,
		ELEMENT_TYPE_U8             = 0x0b,
		ELEMENT_TYPE_R4             = 0x0c,
		ELEMENT_TYPE_R8             = 0x0d,
		ELEMENT_TYPE_STRING         = 0x0e,

		// every type above PTR will be simple type
		ELEMENT_TYPE_PTR            = 0x0f,     // PTR <type>
		ELEMENT_TYPE_BYREF          = 0x10,     // BYREF <type>

		// Please use ELEMENT_TYPE_VALUETYPE. ELEMENT_TYPE_VALUECLASS is deprecated.
		ELEMENT_TYPE_VALUETYPE      = 0x11,     // VALUETYPE <class Token>
		ELEMENT_TYPE_CLASS          = 0x12,     // CLASS <class Token>
		ELEMENT_TYPE_VAR            = 0x13,     // a class type variable VAR <number>
		ELEMENT_TYPE_ARRAY          = 0x14,     // MDARRAY <type> <rank> <bcount> <bound1> ... <lbcount> <lb1> ...
		ELEMENT_TYPE_GENERICINST    = 0x15,     // GENERICINST <generic type> <argCnt> <arg1> ... <argn>
		ELEMENT_TYPE_TYPEDBYREF     = 0x16,     // TYPEDREF  (it takes no args) a typed referece to some other type

		ELEMENT_TYPE_I              = 0x18,     // native integer size
		ELEMENT_TYPE_U              = 0x19,     // native unsigned integer size
		ELEMENT_TYPE_FNPTR          = 0x1b,     // FNPTR <complete sig for the function including calling convention>
		ELEMENT_TYPE_OBJECT         = 0x1c,     // Shortcut for System.Object
		ELEMENT_TYPE_SZARRAY        = 0x1d,     // Shortcut for single dimension zero lower bound array
												// SZARRAY <type>
		ELEMENT_TYPE_MVAR           = 0x1e,     // a method type variable MVAR <number>

		// This is only for binding
		ELEMENT_TYPE_CMOD_REQD      = 0x1f,     // required C modifier : E_T_CMOD_REQD <mdTypeRef/mdTypeDef>
		ELEMENT_TYPE_CMOD_OPT       = 0x20,     // optional C modifier : E_T_CMOD_OPT <mdTypeRef/mdTypeDef>

		// This is for signatures generated internally (which will not be persisted in any way).
		ELEMENT_TYPE_INTERNAL       = 0x21,     // INTERNAL <typehandle>

		// Note that this is the max of base type excluding modifiers
		ELEMENT_TYPE_MAX            = 0x22,     // first invalid element type


		ELEMENT_TYPE_MODIFIER       = 0x40,
		ELEMENT_TYPE_SENTINEL       = 0x01 | ELEMENT_TYPE_MODIFIER, // sentinel for varargs
		ELEMENT_TYPE_PINNED         = 0x05 | ELEMENT_TYPE_MODIFIER,

	} CorElementType;

	typedef enum CorCallingConvention
	{
		IMAGE_CEE_CS_CALLCONV_DEFAULT       = 0x0,

		IMAGE_CEE_CS_CALLCONV_VARARG        = 0x5,
		IMAGE_CEE_CS_CALLCONV_FIELD         = 0x6,
		IMAGE_CEE_CS_CALLCONV_LOCAL_SIG     = 0x7,
		IMAGE_CEE_CS_CALLCONV_PROPERTY      = 0x8,
		IMAGE_CEE_CS_CALLCONV_UNMGD         = 0x9,
		IMAGE_CEE_CS_CALLCONV_GENERICINST   = 0xa,  // generic method instantiation
		IMAGE_CEE_CS_CALLCONV_NATIVEVARARG  = 0xb,  // used ONLY for 64bit vararg PInvoke calls
		IMAGE_CEE_CS_CALLCONV_MAX           = 0xc,  // first invalid calling convention


			// The high bits of the calling convention convey additional info
		IMAGE_CEE_CS_CALLCONV_MASK          = 0x0f,  // Calling convention is bottom 4 bits
		IMAGE_CEE_CS_CALLCONV_HASTHIS       = 0x20,  // Top bit indicates a 'this' parameter
		IMAGE_CEE_CS_CALLCONV_EXPLICITTHIS  = 0x40,  // This parameter is explicitly in the signature
		IMAGE_CEE_CS_CALLCONV_GENERIC       = 0x10,  // Generic method sig with explicit number of type arguments (precedes ordinary parameter count)
		// 0x80 is reserved for internal use
	} CorCallingConvention;

	typedef enum CorUnmanagedCallingConvention
	{
		IMAGE_CEE_UNMANAGED_CALLCONV_C         = 0x1,
		IMAGE_CEE_UNMANAGED_CALLCONV_STDCALL   = 0x2,
		IMAGE_CEE_UNMANAGED_CALLCONV_THISCALL  = 0x3,
		IMAGE_CEE_UNMANAGED_CALLCONV_FASTCALL  = 0x4,

		IMAGE_CEE_CS_CALLCONV_C         = IMAGE_CEE_UNMANAGED_CALLCONV_C,
		IMAGE_CEE_CS_CALLCONV_STDCALL   = IMAGE_CEE_UNMANAGED_CALLCONV_STDCALL,
		IMAGE_CEE_CS_CALLCONV_THISCALL  = IMAGE_CEE_UNMANAGED_CALLCONV_THISCALL,
		IMAGE_CEE_CS_CALLCONV_FASTCALL  = IMAGE_CEE_UNMANAGED_CALLCONV_FASTCALL,

	} CorUnmanagedCallingConvention;

	typedef enum CorSerializationType
	{
		SERIALIZATION_TYPE_UNDEFINED = 0,
		SERIALIZATION_TYPE_BOOLEAN = ELEMENT_TYPE_BOOLEAN,
		SERIALIZATION_TYPE_CHAR = ELEMENT_TYPE_CHAR,
		SERIALIZATION_TYPE_I1 = ELEMENT_TYPE_I1,
		SERIALIZATION_TYPE_U1 = ELEMENT_TYPE_U1,
		SERIALIZATION_TYPE_I2 = ELEMENT_TYPE_I2,
		SERIALIZATION_TYPE_U2 = ELEMENT_TYPE_U2,
		SERIALIZATION_TYPE_I4 = ELEMENT_TYPE_I4,
		SERIALIZATION_TYPE_U4 = ELEMENT_TYPE_U4,
		SERIALIZATION_TYPE_I8 = ELEMENT_TYPE_I8,
		SERIALIZATION_TYPE_U8 = ELEMENT_TYPE_U8,
		SERIALIZATION_TYPE_R4 = ELEMENT_TYPE_R4,
		SERIALIZATION_TYPE_R8 = ELEMENT_TYPE_R8,
		SERIALIZATION_TYPE_STRING = ELEMENT_TYPE_STRING,
		SERIALIZATION_TYPE_SZARRAY = ELEMENT_TYPE_SZARRAY, // Shortcut for single dimension zero lower bound array
		SERIALIZATION_TYPE_TYPE = 0x50,
		SERIALIZATION_TYPE_TAGGED_OBJECT = 0x51,
		SERIALIZATION_TYPE_FIELD = 0x53,
		SERIALIZATION_TYPE_PROPERTY = 0x54,
		SERIALIZATION_TYPE_ENUM = 0x55
	} CorSerializationType;


	typedef struct IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_FAT
	{
		uint32_t     Flags;
		uint32_t     TryOffset;
		uint32_t     TryLength;     // relative to start of try block
		uint32_t     HandlerOffset;
		uint32_t     HandlerLength; // relative to start of handler
		union
		{
			uint32_t ClassToken;     // use for type-based exception handlers
			uint32_t FilterOffset;   // use for filter-based exception handlers (COR_ILEXCEPTION_FILTER is set)
		};
	} IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_FAT;

	typedef struct IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_SMALL
	{
		uint16_t     Flags         : 16;
		uint16_t     TryOffset     : 16;
		uint8_t      TryLength     :  8; // relative to start of try block
		uint16_t     HandlerOffset : 16;
		uint8_t      HandlerLength :  8; // relative to start of handler
		union
		{
			uint32_t ClassToken;
			uint32_t FilterOffset;
		};
	} IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_SMALL;

	typedef struct IMAGE_COR_ILMETHOD_SECT_FAT
	{
		uint8_t  Kind     : 8;
		unsigned DataSize : 24;

	} IMAGE_COR_ILMETHOD_SECT_FAT;

	typedef struct IMAGE_COR_ILMETHOD_SECT_SMALL
	{
		uint8_t Kind;
		uint8_t DataSize;
	} IMAGE_COR_ILMETHOD_SECT_SMALL;

	typedef struct IMAGE_COR_ILMETHOD_FAT
	{
		unsigned Flags          : 12; // Flags see code:CorILMethodFlags
		unsigned Size           :  4; // size in uint32_ts of this structure (currently 3)
		unsigned MaxStack       : 16; // maximum number of items (I4, I, I8, obj ...), on the operand stack
		uint32_t CodeSize       : 32; // size of the code
		uint32_t LocalVarSigTok : 32; // token that indicates the signature of the local vars (0 means none)

	} IMAGE_COR_ILMETHOD_FAT;

	typedef struct IMAGE_COR_ILMETHOD_TINY
	{
		uint8_t Flags_CodeSize;
	} IMAGE_COR_ILMETHOD_TINY;

	typedef struct tagCOR_ILMETHOD_SECT_FAT : IMAGE_COR_ILMETHOD_SECT_FAT
	{
		//Data follows
		const uint8_t* Data() const 
		{ 
			return(((const uint8_t*) this) + sizeof(struct tagCOR_ILMETHOD_SECT_FAT)); 
		}

		//Endian-safe wrappers
		uint8_t GetKind() const
		{
			/* return Kind; */
			return *(uint8_t*)this;
		}

		void SetKind(uint8_t kind)
		{
			/* Kind = kind; */
			*(uint8_t*)this = (uint8_t)kind;
		}

		uint32_t GetDataSize() const
		{
			/* return DataSize; */
			uint8_t* p = (uint8_t*)this;
			return ((unsigned)*(p+1)) |
				(((unsigned)*(p+2)) << 8) |
				(((unsigned)*(p+3)) << 16);
		}

		void SetDataSize(uint32_t datasize)
		{
			/* DataSize = dataSize; */
			uint8_t* p = (uint8_t*)this;
			*(p+1) = (uint8_t)(datasize);
			*(p+2) = (uint8_t)(datasize >> 8);
			*(p+3) = (uint8_t)(datasize >> 16);
		}
	} COR_ILMETHOD_SECT_FAT;

	typedef struct tagCOR_ILMETHOD_SECT_SMALL : IMAGE_COR_ILMETHOD_SECT_SMALL
	{
			//Data follows
		const uint8_t* Data() const 
		{ 
			return(((const uint8_t*) this) + sizeof(struct tagCOR_ILMETHOD_SECT_SMALL)); 
		}

		bool IsSmall() const
		{ 
			return (Kind & CorILMethod_Sect_FatFormat) == 0;
		}

		bool More() const
		{
			return (Kind & CorILMethod_Sect_MoreSects) != 0;
		}
	} COR_ILMETHOD_SECT_SMALL;

	struct COR_ILMETHOD_SECT_EH_SMALL : public COR_ILMETHOD_SECT_SMALL
	{
		static uint32_t Size(uint32_t ehCount)
		{
			return (sizeof(COR_ILMETHOD_SECT_EH_SMALL) + sizeof(IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_SMALL) * (ehCount-1));
		}

		uint16_t Reserved;                                  // alignment padding
		IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_SMALL Clauses[1]; // actually variable size
	};

	struct COR_ILMETHOD_SECT_EH_FAT : public COR_ILMETHOD_SECT_FAT
	{
		static uint32_t Size(uint32_t ehCount)
		{
			return (sizeof(COR_ILMETHOD_SECT_EH_FAT) + sizeof(IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_FAT) * (ehCount-1));
		}

		IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_FAT Clauses[1];     // actually variable size
	};

	struct COR_ILMETHOD_SECT
	{
		bool More() const           
		{ 
			return((AsSmall()->Kind & CorILMethod_Sect_MoreSects) != 0); 
		}

		CorILMethodSect Kind() const
		{ 
			return((CorILMethodSect) (AsSmall()->Kind & CorILMethod_Sect_KindMask)); 
		}

		const COR_ILMETHOD_SECT* Next() const   
		{
			if (!More()) return(0);
			return ((COR_ILMETHOD_SECT*)(((uint8_t *)this) + DataSize()))->Align();
		}

		const uint8_t* Data() const 
		{
			if (IsFat()) return(AsFat()->Data());
			return(AsSmall()->Data());
		}

		uint32_t DataSize() const
		{
			if (Kind() == CorILMethod_Sect_EHTable) 
			{
				// VB and MC++ shipped with bug where they have not accounted for size of COR_ILMETHOD_SECT_EH_XXX
				// in DataSize. To avoid breaking these images, we will align the size of EH sections up. This works
				// because IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_XXX is bigger than COR_ILMETHOD_SECT_EH_XXX 
				// (see VSWhidbey #99031 and related bugs for details).

				if (IsFat())
					return Fat.Size(Fat.GetDataSize() / sizeof(IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_FAT));
				else
					return Small.Size(Small.DataSize / sizeof(IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_SMALL));
			}
			else
			{
				if (IsFat()) return(AsFat()->GetDataSize());
				return(AsSmall()->DataSize);
			}
		}

		friend struct COR_ILMETHOD;
		friend struct tagCOR_ILMETHOD_FAT;
		friend struct tagCOR_ILMETHOD_TINY;
		bool IsFat() const                            
		{ 
			return((AsSmall()->Kind & CorILMethod_Sect_FatFormat) != 0); 
		}

		const COR_ILMETHOD_SECT* Align() const        
		{ 
			return((COR_ILMETHOD_SECT*) ((((size_t) this) + 3) & ~3));  
		}

	protected:
		const COR_ILMETHOD_SECT_FAT*   AsFat() const  
		{ 
			return((COR_ILMETHOD_SECT_FAT*) this); 
		}

		const COR_ILMETHOD_SECT_SMALL* AsSmall() const
		{ 
			return((COR_ILMETHOD_SECT_SMALL*) this); 
		}

	public:
		// The body is either a COR_ILMETHOD_SECT_SMALL or COR_ILMETHOD_SECT_FAT
		// (as indicated by the CorILMethod_Sect_FatFormat bit
		union
		{
			COR_ILMETHOD_SECT_EH_SMALL Small;
			COR_ILMETHOD_SECT_EH_FAT Fat;
		};
	};

	typedef struct tagCOR_ILMETHOD_FAT : IMAGE_COR_ILMETHOD_FAT
	{
		//Endian-safe wrappers
		uint8_t GetSize() const
		{
			/* return Size; */
			uint8_t* p = (uint8_t*)this;
			return *(p+1) >> 4;
		}

		void SetSize(uint8_t size)
		{
			/* Size = size; */
			uint8_t* p = (uint8_t*)this;
			*(p+1) = (uint8_t)((*(p+1) & 0x0F) | (size << 4));
		}

		uint16_t GetFlags() const
		{
			/* return Flags; */
			uint8_t* p = (uint8_t*)this;
			return ((uint16_t)*(p+0)) | (( ((uint16_t)*(p+1)) & 0x0F) << 8);
		}

		void SetFlags(uint16_t flags) {
			/* flags = Flags; */
			uint8_t* p = (uint8_t*)this;
			*p = (uint8_t)flags;
			*(p+1) = (uint8_t)((*(p+1) & 0xF0) | ((flags >> 8) & 0x0F));
		}

		bool IsFat() const {
			/* return((IMAGE_COR_ILMETHOD_FAT::GetFlags() & CorILMethod_FormatMask) == CorILMethod_FatFormat); */
			return (*(uint8_t*)this & CorILMethod_FormatMask) == CorILMethod_FatFormat;
		}

		uint16_t GetMaxStack() const {
			/* return MaxStack; */
			return *(uint16_t*)((uint8_t*)this+2);
		}

		void SetMaxStack(uint16_t maxStack) {
			/* MaxStack = maxStack; */
			*(uint16_t*)((uint8_t*)this+2) = (uint16_t)maxStack;
		}

		uint32_t GetCodeSize() const        
		{ 
			return CodeSize; 
		}

		void SetCodeSize(uint32_t size)        
		{ 
			CodeSize = size; 
		}

		uint32_t GetLocalVarSigTok() const      
		{ 
			return LocalVarSigTok; 
		}

		void SetLocalVarSigTok(uint32_t tok) 
		{ 
			LocalVarSigTok = tok; 
		}

		uint8_t* GetCode() const
		{
			return(((uint8_t*) this) + 4*GetSize());
		}

		bool More() const
		{
			// return (GetFlags() & CorILMethod_MoreSects) != 0;
			return (*(uint8_t*)this & CorILMethod_MoreSects) != 0;
		}

		const COR_ILMETHOD_SECT* GetSect() const
		{
			if (!More()) return (0);
			return(((COR_ILMETHOD_SECT*) (GetCode() + GetCodeSize()))->Align());
		}
	} COR_ILMETHOD_FAT;

	typedef struct tagCOR_ILMETHOD_TINY : IMAGE_COR_ILMETHOD_TINY
	{
		bool IsTiny() const         
		{ 
			return((Flags_CodeSize & (CorILMethod_FormatMask >> 1)) == CorILMethod_TinyFormat); 
		}

		uint8_t GetCodeSize() const    
		{ 
			return(((uint8_t) Flags_CodeSize) >> (CorILMethod_FormatShift-1)); 
		}

		uint16_t GetMaxStack() const    
		{ 
			return 8; 
		}

		uint8_t* GetCode() const        
		{ 
			return(((uint8_t*) this) + sizeof(struct tagCOR_ILMETHOD_TINY)); 
		}

		uint32_t GetLocalVarSigTok() const  
		{ 
			return(0); 
		}

		COR_ILMETHOD_SECT* GetSect() const 
		{ 
			return(0); 
		}
	} COR_ILMETHOD_TINY;

	// TypeDef/ExportedType attr bits, used by DefineTypeDef.
	typedef enum CorTypeAttr
	{
		// Use this mask to retrieve the type visibility information.
		tdVisibilityMask        =   0x00000007,
		tdNotPublic             =   0x00000000,     // Class is not public scope.
		tdPublic                =   0x00000001,     // Class is public scope.
		tdNestedPublic          =   0x00000002,     // Class is nested with public visibility.
		tdNestedPrivate         =   0x00000003,     // Class is nested with private visibility.
		tdNestedFamily          =   0x00000004,     // Class is nested with family visibility.
		tdNestedAssembly        =   0x00000005,     // Class is nested with assembly visibility.
		tdNestedFamANDAssem     =   0x00000006,     // Class is nested with family and assembly visibility.
		tdNestedFamORAssem      =   0x00000007,     // Class is nested with family or assembly visibility.

		// Use this mask to retrieve class layout information
		tdLayoutMask            =   0x00000018,
		tdAutoLayout            =   0x00000000,     // Class fields are auto-laid out
		tdSequentialLayout      =   0x00000008,     // Class fields are laid out sequentially
		tdExplicitLayout        =   0x00000010,     // Layout is supplied explicitly
		// end layout mask

		// Use this mask to retrieve class semantics information.
		tdClassSemanticsMask    =   0x00000020,
		tdClass                 =   0x00000000,     // Type is a class.
		tdInterface             =   0x00000020,     // Type is an interface.
		// end semantics mask

		// Special semantics in addition to class semantics.
		tdAbstract              =   0x00000080,     // Class is abstract
		tdSealed                =   0x00000100,     // Class is concrete and may not be extended
		tdSpecialName           =   0x00000400,     // Class name is special.  Name describes how.

		// Implementation attributes.
		tdImport                =   0x00001000,     // Class / interface is imported
		tdSerializable          =   0x00002000,     // The class is Serializable.

		// Use tdStringFormatMask to retrieve string information for native interop
		tdStringFormatMask      =   0x00030000,
		tdAnsiClass             =   0x00000000,     // LPTSTR is interpreted as ANSI in this class
		tdUnicodeClass          =   0x00010000,     // LPTSTR is interpreted as UNICODE
		tdAutoClass             =   0x00020000,     // LPTSTR is interpreted automatically
		tdCustomFormatClass     =   0x00030000,     // A non-standard encoding specified by CustomFormatMask
		tdCustomFormatMask      =   0x00C00000,     // Use this mask to retrieve non-standard encoding information for native interop. The meaning of the values of these 2 bits is unspecified.

		// end string format mask

		tdBeforeFieldInit       =   0x00100000,     // Initialize the class any time before first static field access.
		tdForwarder             =   0x00200000,     // This ExportedType is a type forwarder.

		// Flags reserved for runtime use.
		tdReservedMask          =   0x00040800,
		tdRTSpecialName         =   0x00000800,     // Runtime should check name encoding.
		tdHasSecurity           =   0x00040000,     // Class has security associate with it.
	} CorTypeAttr;


	// Macros for accessing the members of the CorTypeAttr.
	#define IsTdNotPublic(x)                    (((x) & tdVisibilityMask) == tdNotPublic)
	#define IsTdPublic(x)                       (((x) & tdVisibilityMask) == tdPublic)
	#define IsTdNestedPublic(x)                 (((x) & tdVisibilityMask) == tdNestedPublic)
	#define IsTdNestedPrivate(x)                (((x) & tdVisibilityMask) == tdNestedPrivate)
	#define IsTdNestedFamily(x)                 (((x) & tdVisibilityMask) == tdNestedFamily)
	#define IsTdNestedAssembly(x)               (((x) & tdVisibilityMask) == tdNestedAssembly)
	#define IsTdNestedFamANDAssem(x)            (((x) & tdVisibilityMask) == tdNestedFamANDAssem)
	#define IsTdNestedFamORAssem(x)             (((x) & tdVisibilityMask) == tdNestedFamORAssem)
	#define IsTdNested(x)                       (((x) & tdVisibilityMask) >= tdNestedPublic)

	#define IsTdAutoLayout(x)                   (((x) & tdLayoutMask) == tdAutoLayout)
	#define IsTdSequentialLayout(x)             (((x) & tdLayoutMask) == tdSequentialLayout)
	#define IsTdExplicitLayout(x)               (((x) & tdLayoutMask) == tdExplicitLayout)

	#define IsTdClass(x)                        (((x) & tdClassSemanticsMask) == tdClass)
	#define IsTdInterface(x)                    (((x) & tdClassSemanticsMask) == tdInterface)

	#define IsTdAbstract(x)                     ((x) & tdAbstract)
	#define IsTdSealed(x)                       ((x) & tdSealed)
	#define IsTdSpecialName(x)                  ((x) & tdSpecialName)

	#define IsTdImport(x)                       ((x) & tdImport)
	#define IsTdSerializable(x)                 ((x) & tdSerializable)

	#define IsTdAnsiClass(x)                    (((x) & tdStringFormatMask) == tdAnsiClass)
	#define IsTdUnicodeClass(x)                 (((x) & tdStringFormatMask) == tdUnicodeClass)
	#define IsTdAutoClass(x)                    (((x) & tdStringFormatMask) == tdAutoClass)
	#define IsTdCustomFormatClass(x)            (((x) & tdStringFormatMask) == tdCustomFormatClass)
	#define IsTdBeforeFieldInit(x)              ((x) & tdBeforeFieldInit)
	#define IsTdForwarder(x)                    ((x) & tdForwarder)

	#define IsTdRTSpecialName(x)                ((x) & tdRTSpecialName)
	#define IsTdHasSecurity(x)                  ((x) & tdHasSecurity)
	
	#define COR_CTOR_METHOD_NAME        ".ctor"
	#define COR_CTOR_METHOD_NAME_W      L".ctor"
	#define COR_CCTOR_METHOD_NAME       ".cctor"
	#define COR_CCTOR_METHOD_NAME_W     L".cctor"

	// MethodDef attr bits, Used by DefineMethod.
	typedef enum CorMethodAttr
	{
		// member access mask - Use this mask to retrieve accessibility information.
		mdMemberAccessMask          =   0x0007,
		mdPrivateScope              =   0x0000,     // Member not referenceable.
		mdPrivate                   =   0x0001,     // Accessible only by the parent type.
		mdFamANDAssem               =   0x0002,     // Accessible by sub-types only in this Assembly.
		mdAssem                     =   0x0003,     // Accessibly by anyone in the Assembly.
		mdFamily                    =   0x0004,     // Accessible only by type and sub-types.
		mdFamORAssem                =   0x0005,     // Accessibly by sub-types anywhere, plus anyone in assembly.
		mdPublic                    =   0x0006,     // Accessibly by anyone who has visibility to this scope.
		// end member access mask

		// method contract attributes.
		mdStatic                    =   0x0010,     // Defined on type, else per instance.
		mdFinal                     =   0x0020,     // Method may not be overridden.
		mdVirtual                   =   0x0040,     // Method virtual.
		mdHideBySig                 =   0x0080,     // Method hides by name+sig, else just by name.

		// vtable layout mask - Use this mask to retrieve vtable attributes.
		mdVtableLayoutMask          =   0x0100,
		mdReuseSlot                 =   0x0000,     // The default.
		mdNewSlot                   =   0x0100,     // Method always gets a new slot in the vtable.
		// end vtable layout mask

		// method implementation attributes.
		mdCheckAccessOnOverride     =   0x0200,     // Overridability is the same as the visibility.
		mdAbstract                  =   0x0400,     // Method does not provide an implementation.
		mdSpecialName               =   0x0800,     // Method is special.  Name describes how.

		// interop attributes
		mdPinvokeImpl               =   0x2000,     // Implementation is forwarded through pinvoke.
		mdUnmanagedExport           =   0x0008,     // Managed method exported via thunk to unmanaged code.

		// Reserved flags for runtime use only.
		mdReservedMask              =   0xd000,
		mdRTSpecialName             =   0x1000,     // Runtime should check name encoding.
		mdHasSecurity               =   0x4000,     // Method has security associate with it.
		mdRequireSecObject          =   0x8000,     // Method calls another method containing security code.

	} CorMethodAttr;

	// Macros for accessing the members of CorMethodAttr.
	#define IsMdPrivateScope(x)                 (((x) & mdMemberAccessMask) == mdPrivateScope)
	#define IsMdPrivate(x)                      (((x) & mdMemberAccessMask) == mdPrivate)
	#define IsMdFamANDAssem(x)                  (((x) & mdMemberAccessMask) == mdFamANDAssem)
	#define IsMdAssem(x)                        (((x) & mdMemberAccessMask) == mdAssem)
	#define IsMdFamily(x)                       (((x) & mdMemberAccessMask) == mdFamily)
	#define IsMdFamORAssem(x)                   (((x) & mdMemberAccessMask) == mdFamORAssem)
	#define IsMdPublic(x)                       (((x) & mdMemberAccessMask) == mdPublic)

	#define IsMdStatic(x)                       ((x) & mdStatic)
	#define IsMdFinal(x)                        ((x) & mdFinal)
	#define IsMdVirtual(x)                      ((x) & mdVirtual)
	#define IsMdHideBySig(x)                    ((x) & mdHideBySig)

	#define IsMdReuseSlot(x)                    (((x) & mdVtableLayoutMask) == mdReuseSlot)
	#define IsMdNewSlot(x)                      (((x) & mdVtableLayoutMask) == mdNewSlot)

	#define IsMdCheckAccessOnOverride(x)        ((x) & mdCheckAccessOnOverride)
	#define IsMdAbstract(x)                     ((x) & mdAbstract)
	#define IsMdSpecialName(x)                  ((x) & mdSpecialName)

	#define IsMdPinvokeImpl(x)                  ((x) & mdPinvokeImpl)
	#define IsMdUnmanagedExport(x)              ((x) & mdUnmanagedExport)

	#define IsMdRTSpecialName(x)                ((x) & mdRTSpecialName)
	#define IsMdInstanceInitializer(x, str)     (((x) & mdRTSpecialName) && !strcmp((str), COR_CTOR_METHOD_NAME))
	#define IsMdInstanceInitializerW(x, str)    (((x) & mdRTSpecialName) && !wcscmp((str), COR_CTOR_METHOD_NAME_W))
	#define IsMdClassConstructor(x, str)        (((x) & mdRTSpecialName) && !strcmp((str), COR_CCTOR_METHOD_NAME))
	#define IsMdClassConstructorW(x, str)       (((x) & mdRTSpecialName) && !wcscmp((str), COR_CCTOR_METHOD_NAME_W))
	#define IsMdHasSecurity(x)                  ((x) & mdHasSecurity)
	#define IsMdRequireSecObject(x)             ((x) & mdRequireSecObject)
}}