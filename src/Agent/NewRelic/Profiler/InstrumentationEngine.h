

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0626 */
/* at Mon Jan 18 21:14:07 2038
 */
/* Compiler settings for InstrumentationEngine.idl:
    Oicf, W1, Zp8, env=Win64 (32b run), target_arch=AMD64 8.01.0626 
    protocol : all , ms_ext, c_ext, robust
    error checks: allocation ref bounds_check enum stub_data 
    VC __declspec() decoration level: 
         __declspec(uuid()), __declspec(selectany), __declspec(novtable)
         DECLSPEC_UUID(), MIDL_INTERFACE()
*/
/* @@MIDL_FILE_HEADING(  ) */



/* verify that the <rpcndr.h> version is high enough to compile this file*/
#ifndef __REQUIRED_RPCNDR_H_VERSION__
#define __REQUIRED_RPCNDR_H_VERSION__ 500
#endif

/* verify that the <rpcsal.h> version is high enough to compile this file*/
#ifndef __REQUIRED_RPCSAL_H_VERSION__
#define __REQUIRED_RPCSAL_H_VERSION__ 100
#endif

#include "rpc.h"
#include "rpcndr.h"

#ifndef __RPCNDR_H_VERSION__
#error this stub requires an updated version of <rpcndr.h>
#endif /* __RPCNDR_H_VERSION__ */


#ifndef __InstrumentationEngine_h__
#define __InstrumentationEngine_h__

#if defined(_MSC_VER) && (_MSC_VER >= 1020)
#pragma once
#endif

#ifndef DECLSPEC_XFGVIRT
#if _CONTROL_FLOW_GUARD_XFG
#define DECLSPEC_XFGVIRT(base, func) __declspec(xfg_virtual(base, func))
#else
#define DECLSPEC_XFGVIRT(base, func)
#endif
#endif

/* Forward Declarations */ 

#ifndef __IProfilerManager_FWD_DEFINED__
#define __IProfilerManager_FWD_DEFINED__
typedef interface IProfilerManager IProfilerManager;

#endif 	/* __IProfilerManager_FWD_DEFINED__ */


#ifndef __IProfilerManagerHost_FWD_DEFINED__
#define __IProfilerManagerHost_FWD_DEFINED__
typedef interface IProfilerManagerHost IProfilerManagerHost;

#endif 	/* __IProfilerManagerHost_FWD_DEFINED__ */


#ifndef __IProfilerManagerLogging_FWD_DEFINED__
#define __IProfilerManagerLogging_FWD_DEFINED__
typedef interface IProfilerManagerLogging IProfilerManagerLogging;

#endif 	/* __IProfilerManagerLogging_FWD_DEFINED__ */


#ifndef __IProfilerManagerLoggingHost_FWD_DEFINED__
#define __IProfilerManagerLoggingHost_FWD_DEFINED__
typedef interface IProfilerManagerLoggingHost IProfilerManagerLoggingHost;

#endif 	/* __IProfilerManagerLoggingHost_FWD_DEFINED__ */


#ifndef __IInstrumentationMethod_FWD_DEFINED__
#define __IInstrumentationMethod_FWD_DEFINED__
typedef interface IInstrumentationMethod IInstrumentationMethod;

#endif 	/* __IInstrumentationMethod_FWD_DEFINED__ */


#ifndef __IDataContainer_FWD_DEFINED__
#define __IDataContainer_FWD_DEFINED__
typedef interface IDataContainer IDataContainer;

#endif 	/* __IDataContainer_FWD_DEFINED__ */


#ifndef __IInstruction_FWD_DEFINED__
#define __IInstruction_FWD_DEFINED__
typedef interface IInstruction IInstruction;

#endif 	/* __IInstruction_FWD_DEFINED__ */


#ifndef __IExceptionSection_FWD_DEFINED__
#define __IExceptionSection_FWD_DEFINED__
typedef interface IExceptionSection IExceptionSection;

#endif 	/* __IExceptionSection_FWD_DEFINED__ */


#ifndef __IExceptionClause_FWD_DEFINED__
#define __IExceptionClause_FWD_DEFINED__
typedef interface IExceptionClause IExceptionClause;

#endif 	/* __IExceptionClause_FWD_DEFINED__ */


#ifndef __IEnumExceptionClauses_FWD_DEFINED__
#define __IEnumExceptionClauses_FWD_DEFINED__
typedef interface IEnumExceptionClauses IEnumExceptionClauses;

#endif 	/* __IEnumExceptionClauses_FWD_DEFINED__ */


#ifndef __IOperandInstruction_FWD_DEFINED__
#define __IOperandInstruction_FWD_DEFINED__
typedef interface IOperandInstruction IOperandInstruction;

#endif 	/* __IOperandInstruction_FWD_DEFINED__ */


#ifndef __IBranchInstruction_FWD_DEFINED__
#define __IBranchInstruction_FWD_DEFINED__
typedef interface IBranchInstruction IBranchInstruction;

#endif 	/* __IBranchInstruction_FWD_DEFINED__ */


#ifndef __ISwitchInstruction_FWD_DEFINED__
#define __ISwitchInstruction_FWD_DEFINED__
typedef interface ISwitchInstruction ISwitchInstruction;

#endif 	/* __ISwitchInstruction_FWD_DEFINED__ */


#ifndef __IInstructionGraph_FWD_DEFINED__
#define __IInstructionGraph_FWD_DEFINED__
typedef interface IInstructionGraph IInstructionGraph;

#endif 	/* __IInstructionGraph_FWD_DEFINED__ */


#ifndef __IMethodInfo_FWD_DEFINED__
#define __IMethodInfo_FWD_DEFINED__
typedef interface IMethodInfo IMethodInfo;

#endif 	/* __IMethodInfo_FWD_DEFINED__ */


#ifndef __IMethodInfo2_FWD_DEFINED__
#define __IMethodInfo2_FWD_DEFINED__
typedef interface IMethodInfo2 IMethodInfo2;

#endif 	/* __IMethodInfo2_FWD_DEFINED__ */


#ifndef __IAssemblyInfo_FWD_DEFINED__
#define __IAssemblyInfo_FWD_DEFINED__
typedef interface IAssemblyInfo IAssemblyInfo;

#endif 	/* __IAssemblyInfo_FWD_DEFINED__ */


#ifndef __IEnumAssemblyInfo_FWD_DEFINED__
#define __IEnumAssemblyInfo_FWD_DEFINED__
typedef interface IEnumAssemblyInfo IEnumAssemblyInfo;

#endif 	/* __IEnumAssemblyInfo_FWD_DEFINED__ */


#ifndef __IModuleInfo_FWD_DEFINED__
#define __IModuleInfo_FWD_DEFINED__
typedef interface IModuleInfo IModuleInfo;

#endif 	/* __IModuleInfo_FWD_DEFINED__ */


#ifndef __IModuleInfo2_FWD_DEFINED__
#define __IModuleInfo2_FWD_DEFINED__
typedef interface IModuleInfo2 IModuleInfo2;

#endif 	/* __IModuleInfo2_FWD_DEFINED__ */


#ifndef __IModuleInfo3_FWD_DEFINED__
#define __IModuleInfo3_FWD_DEFINED__
typedef interface IModuleInfo3 IModuleInfo3;

#endif 	/* __IModuleInfo3_FWD_DEFINED__ */


#ifndef __IEnumModuleInfo_FWD_DEFINED__
#define __IEnumModuleInfo_FWD_DEFINED__
typedef interface IEnumModuleInfo IEnumModuleInfo;

#endif 	/* __IEnumModuleInfo_FWD_DEFINED__ */


#ifndef __IAppDomainInfo_FWD_DEFINED__
#define __IAppDomainInfo_FWD_DEFINED__
typedef interface IAppDomainInfo IAppDomainInfo;

#endif 	/* __IAppDomainInfo_FWD_DEFINED__ */


#ifndef __IEnumAppDomainInfo_FWD_DEFINED__
#define __IEnumAppDomainInfo_FWD_DEFINED__
typedef interface IEnumAppDomainInfo IEnumAppDomainInfo;

#endif 	/* __IEnumAppDomainInfo_FWD_DEFINED__ */


#ifndef __ILocalVariableCollection_FWD_DEFINED__
#define __ILocalVariableCollection_FWD_DEFINED__
typedef interface ILocalVariableCollection ILocalVariableCollection;

#endif 	/* __ILocalVariableCollection_FWD_DEFINED__ */


#ifndef __IType_FWD_DEFINED__
#define __IType_FWD_DEFINED__
typedef interface IType IType;

#endif 	/* __IType_FWD_DEFINED__ */


#ifndef __IAppDomainCollection_FWD_DEFINED__
#define __IAppDomainCollection_FWD_DEFINED__
typedef interface IAppDomainCollection IAppDomainCollection;

#endif 	/* __IAppDomainCollection_FWD_DEFINED__ */


#ifndef __ISignatureBuilder_FWD_DEFINED__
#define __ISignatureBuilder_FWD_DEFINED__
typedef interface ISignatureBuilder ISignatureBuilder;

#endif 	/* __ISignatureBuilder_FWD_DEFINED__ */


#ifndef __ITypeCreator_FWD_DEFINED__
#define __ITypeCreator_FWD_DEFINED__
typedef interface ITypeCreator ITypeCreator;

#endif 	/* __ITypeCreator_FWD_DEFINED__ */


#ifndef __IMethodLocal_FWD_DEFINED__
#define __IMethodLocal_FWD_DEFINED__
typedef interface IMethodLocal IMethodLocal;

#endif 	/* __IMethodLocal_FWD_DEFINED__ */


#ifndef __IMethodParameter_FWD_DEFINED__
#define __IMethodParameter_FWD_DEFINED__
typedef interface IMethodParameter IMethodParameter;

#endif 	/* __IMethodParameter_FWD_DEFINED__ */


#ifndef __IEnumMethodLocals_FWD_DEFINED__
#define __IEnumMethodLocals_FWD_DEFINED__
typedef interface IEnumMethodLocals IEnumMethodLocals;

#endif 	/* __IEnumMethodLocals_FWD_DEFINED__ */


#ifndef __IEnumMethodParameters_FWD_DEFINED__
#define __IEnumMethodParameters_FWD_DEFINED__
typedef interface IEnumMethodParameters IEnumMethodParameters;

#endif 	/* __IEnumMethodParameters_FWD_DEFINED__ */


#ifndef __ISingleRetDefaultInstrumentation_FWD_DEFINED__
#define __ISingleRetDefaultInstrumentation_FWD_DEFINED__
typedef interface ISingleRetDefaultInstrumentation ISingleRetDefaultInstrumentation;

#endif 	/* __ISingleRetDefaultInstrumentation_FWD_DEFINED__ */


#ifndef __IProfilerManager2_FWD_DEFINED__
#define __IProfilerManager2_FWD_DEFINED__
typedef interface IProfilerManager2 IProfilerManager2;

#endif 	/* __IProfilerManager2_FWD_DEFINED__ */


#ifndef __IProfilerManager3_FWD_DEFINED__
#define __IProfilerManager3_FWD_DEFINED__
typedef interface IProfilerManager3 IProfilerManager3;

#endif 	/* __IProfilerManager3_FWD_DEFINED__ */


#ifndef __IProfilerManager4_FWD_DEFINED__
#define __IProfilerManager4_FWD_DEFINED__
typedef interface IProfilerManager4 IProfilerManager4;

#endif 	/* __IProfilerManager4_FWD_DEFINED__ */


#ifndef __IProfilerManager5_FWD_DEFINED__
#define __IProfilerManager5_FWD_DEFINED__
typedef interface IProfilerManager5 IProfilerManager5;

#endif 	/* __IProfilerManager5_FWD_DEFINED__ */


#ifndef __IProfilerStringManager_FWD_DEFINED__
#define __IProfilerStringManager_FWD_DEFINED__
typedef interface IProfilerStringManager IProfilerStringManager;

#endif 	/* __IProfilerStringManager_FWD_DEFINED__ */


#ifndef __IInstrumentationMethodExceptionEvents_FWD_DEFINED__
#define __IInstrumentationMethodExceptionEvents_FWD_DEFINED__
typedef interface IInstrumentationMethodExceptionEvents IInstrumentationMethodExceptionEvents;

#endif 	/* __IInstrumentationMethodExceptionEvents_FWD_DEFINED__ */


#ifndef __IEnumInstructions_FWD_DEFINED__
#define __IEnumInstructions_FWD_DEFINED__
typedef interface IEnumInstructions IEnumInstructions;

#endif 	/* __IEnumInstructions_FWD_DEFINED__ */


#ifndef __IInstructionFactory_FWD_DEFINED__
#define __IInstructionFactory_FWD_DEFINED__
typedef interface IInstructionFactory IInstructionFactory;

#endif 	/* __IInstructionFactory_FWD_DEFINED__ */


#ifndef __IEnumAppMethodInfo_FWD_DEFINED__
#define __IEnumAppMethodInfo_FWD_DEFINED__
typedef interface IEnumAppMethodInfo IEnumAppMethodInfo;

#endif 	/* __IEnumAppMethodInfo_FWD_DEFINED__ */


#ifndef __ILocalVariableCollection2_FWD_DEFINED__
#define __ILocalVariableCollection2_FWD_DEFINED__
typedef interface ILocalVariableCollection2 ILocalVariableCollection2;

#endif 	/* __ILocalVariableCollection2_FWD_DEFINED__ */


#ifndef __IEnumTypes_FWD_DEFINED__
#define __IEnumTypes_FWD_DEFINED__
typedef interface IEnumTypes IEnumTypes;

#endif 	/* __IEnumTypes_FWD_DEFINED__ */


#ifndef __ISignatureParser_FWD_DEFINED__
#define __ISignatureParser_FWD_DEFINED__
typedef interface ISignatureParser ISignatureParser;

#endif 	/* __ISignatureParser_FWD_DEFINED__ */


#ifndef __ITokenType_FWD_DEFINED__
#define __ITokenType_FWD_DEFINED__
typedef interface ITokenType ITokenType;

#endif 	/* __ITokenType_FWD_DEFINED__ */


#ifndef __ICompositeType_FWD_DEFINED__
#define __ICompositeType_FWD_DEFINED__
typedef interface ICompositeType ICompositeType;

#endif 	/* __ICompositeType_FWD_DEFINED__ */


#ifndef __IGenericParameterType_FWD_DEFINED__
#define __IGenericParameterType_FWD_DEFINED__
typedef interface IGenericParameterType IGenericParameterType;

#endif 	/* __IGenericParameterType_FWD_DEFINED__ */


#ifndef __ISingleRetDefaultInstrumentation2_FWD_DEFINED__
#define __ISingleRetDefaultInstrumentation2_FWD_DEFINED__
typedef interface ISingleRetDefaultInstrumentation2 ISingleRetDefaultInstrumentation2;

#endif 	/* __ISingleRetDefaultInstrumentation2_FWD_DEFINED__ */


#ifndef __IInstrumentationMethodJitEvents_FWD_DEFINED__
#define __IInstrumentationMethodJitEvents_FWD_DEFINED__
typedef interface IInstrumentationMethodJitEvents IInstrumentationMethodJitEvents;

#endif 	/* __IInstrumentationMethodJitEvents_FWD_DEFINED__ */


#ifndef __IMethodJitInfo_FWD_DEFINED__
#define __IMethodJitInfo_FWD_DEFINED__
typedef interface IMethodJitInfo IMethodJitInfo;

#endif 	/* __IMethodJitInfo_FWD_DEFINED__ */


#ifndef __IMethodJitInfo2_FWD_DEFINED__
#define __IMethodJitInfo2_FWD_DEFINED__
typedef interface IMethodJitInfo2 IMethodJitInfo2;

#endif 	/* __IMethodJitInfo2_FWD_DEFINED__ */


#ifndef __IInstrumentationMethodJitEvents2_FWD_DEFINED__
#define __IInstrumentationMethodJitEvents2_FWD_DEFINED__
typedef interface IInstrumentationMethodJitEvents2 IInstrumentationMethodJitEvents2;

#endif 	/* __IInstrumentationMethodJitEvents2_FWD_DEFINED__ */


#ifndef __IInstrumentationMethodSetting_FWD_DEFINED__
#define __IInstrumentationMethodSetting_FWD_DEFINED__
typedef interface IInstrumentationMethodSetting IInstrumentationMethodSetting;

#endif 	/* __IInstrumentationMethodSetting_FWD_DEFINED__ */


#ifndef __IEnumInstrumentationMethodSettings_FWD_DEFINED__
#define __IEnumInstrumentationMethodSettings_FWD_DEFINED__
typedef interface IEnumInstrumentationMethodSettings IEnumInstrumentationMethodSettings;

#endif 	/* __IEnumInstrumentationMethodSettings_FWD_DEFINED__ */


#ifndef __IInstrumentationMethodAttachContext_FWD_DEFINED__
#define __IInstrumentationMethodAttachContext_FWD_DEFINED__
typedef interface IInstrumentationMethodAttachContext IInstrumentationMethodAttachContext;

#endif 	/* __IInstrumentationMethodAttachContext_FWD_DEFINED__ */


#ifndef __IInstrumentationMethodAttach_FWD_DEFINED__
#define __IInstrumentationMethodAttach_FWD_DEFINED__
typedef interface IInstrumentationMethodAttach IInstrumentationMethodAttach;

#endif 	/* __IInstrumentationMethodAttach_FWD_DEFINED__ */


/* header files for imported files */
#include "ocidl.h"
#include "corprof.h"

#ifdef __cplusplus
extern "C"{
#endif 



#ifndef __MicrosoftInstrumentationEngine_LIBRARY_DEFINED__
#define __MicrosoftInstrumentationEngine_LIBRARY_DEFINED__

/* library MicrosoftInstrumentationEngine */
/* [uuid] */ 






































#define	CLR_INSTRUMENTATION_ENGINE_API_VER	( 7 )


enum LoggingFlags
    {
        LoggingFlags_None	= 0,
        LoggingFlags_Errors	= 0x1,
        LoggingFlags_Trace	= 0x2,
        LoggingFlags_InstrumentationResults	= 0x4
    } ;

enum ILOrdinalOpcode
    {
        Cee_Nop	= 0,
        Cee_Break	= 0x1,
        Cee_Ldarg_0	= 0x2,
        Cee_Ldarg_1	= 0x3,
        Cee_Ldarg_2	= 0x4,
        Cee_Ldarg_3	= 0x5,
        Cee_Ldloc_0	= 0x6,
        Cee_Ldloc_1	= 0x7,
        Cee_Ldloc_2	= 0x8,
        Cee_Ldloc_3	= 0x9,
        Cee_Stloc_0	= 0xa,
        Cee_Stloc_1	= 0xb,
        Cee_Stloc_2	= 0xc,
        Cee_Stloc_3	= 0xd,
        Cee_Ldarg_S	= 0xe,
        Cee_Ldarga_S	= 0xf,
        Cee_Starg_S	= 0x10,
        Cee_Ldloc_S	= 0x11,
        Cee_Ldloca_S	= 0x12,
        Cee_Stloc_S	= 0x13,
        Cee_Ldnull	= 0x14,
        Cee_Ldc_I4_M1	= 0x15,
        Cee_Ldc_I4_0	= 0x16,
        Cee_Ldc_I4_1	= 0x17,
        Cee_Ldc_I4_2	= 0x18,
        Cee_Ldc_I4_3	= 0x19,
        Cee_Ldc_I4_4	= 0x1a,
        Cee_Ldc_I4_5	= 0x1b,
        Cee_Ldc_I4_6	= 0x1c,
        Cee_Ldc_I4_7	= 0x1d,
        Cee_Ldc_I4_8	= 0x1e,
        Cee_Ldc_I4_S	= 0x1f,
        Cee_Ldc_I4	= 0x20,
        Cee_Ldc_I8	= 0x21,
        Cee_Ldc_R4	= 0x22,
        Cee_Ldc_R8	= 0x23,
        Cee_Unused49	= 0x24,
        Cee_Dup	= 0x25,
        Cee_Pop	= 0x26,
        Cee_Jmp	= 0x27,
        Cee_Call	= 0x28,
        Cee_Calli	= 0x29,
        Cee_Ret	= 0x2a,
        Cee_Br_S	= 0x2b,
        Cee_Brfalse_S	= 0x2c,
        Cee_Brtrue_S	= 0x2d,
        Cee_Beq_S	= 0x2e,
        Cee_Bge_S	= 0x2f,
        Cee_Bgt_S	= 0x30,
        Cee_Ble_S	= 0x31,
        Cee_Blt_S	= 0x32,
        Cee_Bne_Un_S	= 0x33,
        Cee_Bge_Un_S	= 0x34,
        Cee_Bgt_Un_S	= 0x35,
        Cee_Ble_Un_S	= 0x36,
        Cee_Blt_Un_S	= 0x37,
        Cee_Br	= 0x38,
        Cee_Brfalse	= 0x39,
        Cee_Brtrue	= 0x3a,
        Cee_Beq	= 0x3b,
        Cee_Bge	= 0x3c,
        Cee_Bgt	= 0x3d,
        Cee_Ble	= 0x3e,
        Cee_Blt	= 0x3f,
        Cee_Bne_Un	= 0x40,
        Cee_Bge_Un	= 0x41,
        Cee_Bgt_Un	= 0x42,
        Cee_Ble_Un	= 0x43,
        Cee_Blt_Un	= 0x44,
        Cee_Switch	= 0x45,
        Cee_Ldind_I1	= 0x46,
        Cee_Ldind_U1	= 0x47,
        Cee_Ldind_I2	= 0x48,
        Cee_Ldind_U2	= 0x49,
        Cee_Ldind_I4	= 0x4a,
        Cee_Ldind_U4	= 0x4b,
        Cee_Ldind_I8	= 0x4c,
        Cee_Ldind_I	= 0x4d,
        Cee_Ldind_R4	= 0x4e,
        Cee_Ldind_R8	= 0x4f,
        Cee_Ldind_Ref	= 0x50,
        Cee_Stind_Ref	= 0x51,
        Cee_Stind_I1	= 0x52,
        Cee_Stind_I2	= 0x53,
        Cee_Stind_I4	= 0x54,
        Cee_Stind_I8	= 0x55,
        Cee_Stind_R4	= 0x56,
        Cee_Stind_R8	= 0x57,
        Cee_Add	= 0x58,
        Cee_Sub	= 0x59,
        Cee_Mul	= 0x5a,
        Cee_Div	= 0x5b,
        Cee_Div_Un	= 0x5c,
        Cee_Rem	= 0x5d,
        Cee_Rem_Un	= 0x5e,
        Cee_And	= 0x5f,
        Cee_Or	= 0x60,
        Cee_Xor	= 0x61,
        Cee_Shl	= 0x62,
        Cee_Shr	= 0x63,
        Cee_Shr_Un	= 0x64,
        Cee_Neg	= 0x65,
        Cee_Not	= 0x66,
        Cee_Conv_I1	= 0x67,
        Cee_Conv_I2	= 0x68,
        Cee_Conv_I4	= 0x69,
        Cee_Conv_I8	= 0x6a,
        Cee_Conv_R4	= 0x6b,
        Cee_Conv_R8	= 0x6c,
        Cee_Conv_U4	= 0x6d,
        Cee_Conv_U8	= 0x6e,
        Cee_Callvirt	= 0x6f,
        Cee_Cpobj	= 0x70,
        Cee_Ldobj	= 0x71,
        Cee_Ldstr	= 0x72,
        Cee_Newobj	= 0x73,
        Cee_Castclass	= 0x74,
        Cee_Isinst	= 0x75,
        Cee_Conv_R_Un	= 0x76,
        Cee_Unused58	= 0x77,
        Cee_Unused1	= 0x78,
        Cee_Unbox	= 0x79,
        Cee_Throw	= 0x7a,
        Cee_Ldfld	= 0x7b,
        Cee_Ldflda	= 0x7c,
        Cee_Stfld	= 0x7d,
        Cee_Ldsfld	= 0x7e,
        Cee_Ldsflda	= 0x7f,
        Cee_Stsfld	= 0x80,
        Cee_Stobj	= 0x81,
        Cee_Conv_Ovf_I1_Un	= 0x82,
        Cee_Conv_Ovf_I2_Un	= 0x83,
        Cee_Conv_Ovf_I4_Un	= 0x84,
        Cee_Conv_Ovf_I8_Un	= 0x85,
        Cee_Conv_Ovf_U1_Un	= 0x86,
        Cee_Conv_Ovf_U2_Un	= 0x87,
        Cee_Conv_Ovf_U4_Un	= 0x88,
        Cee_Conv_Ovf_U8_Un	= 0x89,
        Cee_Conv_Ovf_I_Un	= 0x8a,
        Cee_Conv_Ovf_U_Un	= 0x8b,
        Cee_Box	= 0x8c,
        Cee_Newarr	= 0x8d,
        Cee_Ldlen	= 0x8e,
        Cee_Ldelema	= 0x8f,
        Cee_Ldelem_I1	= 0x90,
        Cee_Ldelem_U1	= 0x91,
        Cee_Ldelem_I2	= 0x92,
        Cee_Ldelem_U2	= 0x93,
        Cee_Ldelem_I4	= 0x94,
        Cee_Ldelem_U4	= 0x95,
        Cee_Ldelem_I8	= 0x96,
        Cee_Ldelem_I	= 0x97,
        Cee_Ldelem_R4	= 0x98,
        Cee_Ldelem_R8	= 0x99,
        Cee_Ldelem_Ref	= 0x9a,
        Cee_Stelem_I	= 0x9b,
        Cee_Stelem_I1	= 0x9c,
        Cee_Stelem_I2	= 0x9d,
        Cee_Stelem_I4	= 0x9e,
        Cee_Stelem_I8	= 0x9f,
        Cee_Stelem_R4	= 0xa0,
        Cee_Stelem_R8	= 0xa1,
        Cee_Stelem_Ref	= 0xa2,
        Cee_Ldelem	= 0xa3,
        Cee_Stelem	= 0xa4,
        Cee_Unbox_Any	= 0xa5,
        Cee_Unused5	= 0xa6,
        Cee_Unused6	= 0xa7,
        Cee_Unused7	= 0xa8,
        Cee_Unused8	= 0xa9,
        Cee_Unused9	= 0xaa,
        Cee_Unused10	= 0xab,
        Cee_Unused11	= 0xac,
        Cee_Unused12	= 0xad,
        Cee_Unused13	= 0xae,
        Cee_Unused14	= 0xaf,
        Cee_Unused15	= 0xb0,
        Cee_Unused16	= 0xb1,
        Cee_Unused17	= 0xb2,
        Cee_Conv_Ovf_I1	= 0xb3,
        Cee_Conv_Ovf_U1	= 0xb4,
        Cee_Conv_Ovf_I2	= 0xb5,
        Cee_Conv_Ovf_U2	= 0xb6,
        Cee_Conv_Ovf_I4	= 0xb7,
        Cee_Conv_Ovf_U4	= 0xb8,
        Cee_Conv_Ovf_I8	= 0xb9,
        Cee_Conv_Ovf_U8	= 0xba,
        Cee_Unused50	= 0xbb,
        Cee_Unused18	= 0xbc,
        Cee_Unused19	= 0xbd,
        Cee_Unused20	= 0xbe,
        Cee_Unused21	= 0xbf,
        Cee_Unused22	= 0xc0,
        Cee_Unused23	= 0xc1,
        Cee_Refanyval	= 0xc2,
        Cee_Ckfinite	= 0xc3,
        Cee_Unused24	= 0xc4,
        Cee_Unused25	= 0xc5,
        Cee_Mkrefany	= 0xc6,
        Cee_Unused59	= 0xc7,
        Cee_Unused60	= 0xc8,
        Cee_Unused61	= 0xc9,
        Cee_Unused62	= 0xca,
        Cee_Unused63	= 0xcb,
        Cee_Unused64	= 0xcc,
        Cee_Unused65	= 0xcd,
        Cee_Unused66	= 0xce,
        Cee_Unused67	= 0xcf,
        Cee_Ldtoken	= 0xd0,
        Cee_Conv_U2	= 0xd1,
        Cee_Conv_U1	= 0xd2,
        Cee_Conv_I	= 0xd3,
        Cee_Conv_Ovf_I	= 0xd4,
        Cee_Conv_Ovf_U	= 0xd5,
        Cee_Add_Ovf	= 0xd6,
        Cee_Add_Ovf_Un	= 0xd7,
        Cee_Mul_Ovf	= 0xd8,
        Cee_Mul_Ovf_Un	= 0xd9,
        Cee_Sub_Ovf	= 0xda,
        Cee_Sub_Ovf_Un	= 0xdb,
        Cee_Endfinally	= 0xdc,
        Cee_Leave	= 0xdd,
        Cee_Leave_S	= 0xde,
        Cee_Stind_I	= 0xdf,
        Cee_Conv_U	= 0xe0,
        Cee_Unused26	= 0xe1,
        Cee_Unused27	= 0xe2,
        Cee_Unused28	= 0xe3,
        Cee_Unused29	= 0xe4,
        Cee_Unused30	= 0xe5,
        Cee_Unused31	= 0xe6,
        Cee_Unused32	= 0xe7,
        Cee_Unused33	= 0xe8,
        Cee_Unused34	= 0xe9,
        Cee_Unused35	= 0xea,
        Cee_Unused36	= 0xeb,
        Cee_Unused37	= 0xec,
        Cee_Unused38	= 0xed,
        Cee_Unused39	= 0xee,
        Cee_Unused40	= 0xef,
        Cee_Unused41	= 0xf0,
        Cee_Unused42	= 0xf1,
        Cee_Unused43	= 0xf2,
        Cee_Unused44	= 0xf3,
        Cee_Unused45	= 0xf4,
        Cee_Unused46	= 0xf5,
        Cee_Unused47	= 0xf6,
        Cee_Unused48	= 0xf7,
        Cee_Prefix7	= 0xf8,
        Cee_Prefix6	= 0xf9,
        Cee_Prefix5	= 0xfa,
        Cee_Prefix4	= 0xfb,
        Cee_Prefix3	= 0xfc,
        Cee_Prefix2	= 0xfd,
        Cee_Prefix1	= 0xfe,
        Cee_Prefixref	= 0xff,
        Cee_Arglist	= 0x100,
        Cee_Ceq	= 0x101,
        Cee_Cgt	= 0x102,
        Cee_Cgt_Un	= 0x103,
        Cee_Clt	= 0x104,
        Cee_Clt_Un	= 0x105,
        Cee_Ldftn	= 0x106,
        Cee_Ldvirtftn	= 0x107,
        Cee_Unused56	= 0x108,
        Cee_Ldarg	= 0x109,
        Cee_Ldarga	= 0x10a,
        Cee_Starg	= 0x10b,
        Cee_Ldloc	= 0x10c,
        Cee_Ldloca	= 0x10d,
        Cee_Stloc	= 0x10e,
        Cee_Localloc	= 0x10f,
        Cee_Unused57	= 0x110,
        Cee_Endfilter	= 0x111,
        Cee_Unaligned	= 0x112,
        Cee_Volatile	= 0x113,
        Cee_Tailcall	= 0x114,
        Cee_Initobj	= 0x115,
        Cee_Constrained	= 0x116,
        Cee_Cpblk	= 0x117,
        Cee_Initblk	= 0x118,
        Cee_Unused69	= 0x119,
        Cee_Rethrow	= 0x11a,
        Cee_Unused51	= 0x11b,
        Cee_Sizeof	= 0x11c,
        Cee_Refanytype	= 0x11d,
        Cee_Readonly	= 0x11e,
        Cee_Unused53	= 0x11f,
        Cee_Unused54	= 0x120,
        Cee_Unused55	= 0x121,
        Cee_Unused70	= 0x122,
        Cee_Count	= 0x123,
        Cee_Invalid	= 0xffff
    } ;

enum ILOperandType
    {
        ILOperandType_None	= 0,
        ILOperandType_Byte	= ( ILOperandType_None + 1 ) ,
        ILOperandType_Int	= ( ILOperandType_Byte + 1 ) ,
        ILOperandType_UShort	= ( ILOperandType_Int + 1 ) ,
        ILOperandType_Long	= ( ILOperandType_UShort + 1 ) ,
        ILOperandType_Single	= ( ILOperandType_Long + 1 ) ,
        ILOperandType_Double	= ( ILOperandType_Single + 1 ) ,
        ILOperandType_Token	= ( ILOperandType_Double + 1 ) ,
        ILOperandType_Switch	= ( ILOperandType_Token + 1 ) 
    } ;

enum ILOpcodeFlags
    {
        ILOpcodeFlag_None	= 0,
        ILOpcodeFlag_Meta	= 0x1,
        ILOpcodeFlag_Unused	= 0x2,
        ILOpcodeFlag_Branch	= 0x4
    } ;

enum InstructionGeneration
    {
        Generation_Original	= 0x1,
        Generation_Baseline	= 0x2,
        Generation_New	= 0x3
    } ;

enum InstructionTerminationType
    {
        TerminationType_FallThrough	= 0,
        TerminationType_Branch	= ( TerminationType_FallThrough + 1 ) ,
        TerminationType_ConditionalBranch	= ( TerminationType_Branch + 1 ) ,
        TerminationType_Throw	= ( TerminationType_ConditionalBranch + 1 ) ,
        TerminationType_Switch	= ( TerminationType_Throw + 1 ) ,
        TerminationType_Call	= ( TerminationType_Switch + 1 ) ,
        TerminationType_IndirectCall	= ( TerminationType_Call + 1 ) ,
        TerminationType_Return	= ( TerminationType_IndirectCall + 1 ) ,
        TerminationType_Trap	= ( TerminationType_Return + 1 ) 
    } ;

EXTERN_C const IID LIBID_MicrosoftInstrumentationEngine;

#ifndef __IProfilerManager_INTERFACE_DEFINED__
#define __IProfilerManager_INTERFACE_DEFINED__

/* interface IProfilerManager */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IProfilerManager;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("3A09AD0A-25C6-4093-93E1-3F64EB160A9D")
    IProfilerManager : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE SetupProfilingEnvironment( 
            /* [in] */ __RPC__deref_in_opt BSTR bstrConfigPath[  ],
            /* [in] */ UINT numConfigPaths) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddRawProfilerHook( 
            /* [in] */ __RPC__in_opt IUnknown *pUnkProfilerCallback) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE RemoveRawProfilerHook( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCorProfilerInfo( 
            /* [out] */ __RPC__deref_out_opt IUnknown **ppCorProfiler) = 0;
        
        virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE GetProfilerHost( 
            /* [out] */ __RPC__deref_out_opt IProfilerManagerHost **ppProfilerManagerHost) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetLoggingInstance( 
            /* [out] */ __RPC__deref_out_opt IProfilerManagerLogging **ppLogging) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetLoggingHost( 
            /* [in] */ __RPC__in_opt IProfilerManagerLoggingHost *pLoggingHost) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetAppDomainCollection( 
            /* [out] */ __RPC__deref_out_opt IAppDomainCollection **ppAppDomainCollection) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateSignatureBuilder( 
            /* [out] */ __RPC__deref_out_opt ISignatureBuilder **ppSignatureBuilder) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetInstrumentationMethod( 
            /* [in] */ __RPC__in REFGUID cslid,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppUnknown) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE RemoveInstrumentationMethod( 
            /* [in] */ __RPC__in_opt IInstrumentationMethod *pInstrumentationMethod) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddInstrumentationMethod( 
            /* [in] */ __RPC__in BSTR bstrModulePath,
            /* [in] */ __RPC__in BSTR bstrName,
            /* [in] */ __RPC__in BSTR bstrDescription,
            /* [in] */ __RPC__in BSTR bstrModule,
            /* [in] */ __RPC__in BSTR bstrClassGuid,
            /* [in] */ DWORD dwPriority,
            /* [out] */ __RPC__deref_out_opt IInstrumentationMethod **pInstrumentationMethod) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetRuntimeType( 
            /* [out] */ __RPC__out COR_PRF_RUNTIME_TYPE *pRuntimeType) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IProfilerManagerVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IProfilerManager * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IProfilerManager * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IProfilerManager * This);
        
        DECLSPEC_XFGVIRT(IProfilerManager, SetupProfilingEnvironment)
        HRESULT ( STDMETHODCALLTYPE *SetupProfilingEnvironment )( 
            __RPC__in IProfilerManager * This,
            /* [in] */ __RPC__deref_in_opt BSTR bstrConfigPath[  ],
            /* [in] */ UINT numConfigPaths);
        
        DECLSPEC_XFGVIRT(IProfilerManager, AddRawProfilerHook)
        HRESULT ( STDMETHODCALLTYPE *AddRawProfilerHook )( 
            __RPC__in IProfilerManager * This,
            /* [in] */ __RPC__in_opt IUnknown *pUnkProfilerCallback);
        
        DECLSPEC_XFGVIRT(IProfilerManager, RemoveRawProfilerHook)
        HRESULT ( STDMETHODCALLTYPE *RemoveRawProfilerHook )( 
            __RPC__in IProfilerManager * This);
        
        DECLSPEC_XFGVIRT(IProfilerManager, GetCorProfilerInfo)
        HRESULT ( STDMETHODCALLTYPE *GetCorProfilerInfo )( 
            __RPC__in IProfilerManager * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppCorProfiler);
        
        DECLSPEC_XFGVIRT(IProfilerManager, GetProfilerHost)
        /* [helpstring] */ HRESULT ( STDMETHODCALLTYPE *GetProfilerHost )( 
            __RPC__in IProfilerManager * This,
            /* [out] */ __RPC__deref_out_opt IProfilerManagerHost **ppProfilerManagerHost);
        
        DECLSPEC_XFGVIRT(IProfilerManager, GetLoggingInstance)
        HRESULT ( STDMETHODCALLTYPE *GetLoggingInstance )( 
            __RPC__in IProfilerManager * This,
            /* [out] */ __RPC__deref_out_opt IProfilerManagerLogging **ppLogging);
        
        DECLSPEC_XFGVIRT(IProfilerManager, SetLoggingHost)
        HRESULT ( STDMETHODCALLTYPE *SetLoggingHost )( 
            __RPC__in IProfilerManager * This,
            /* [in] */ __RPC__in_opt IProfilerManagerLoggingHost *pLoggingHost);
        
        DECLSPEC_XFGVIRT(IProfilerManager, GetAppDomainCollection)
        HRESULT ( STDMETHODCALLTYPE *GetAppDomainCollection )( 
            __RPC__in IProfilerManager * This,
            /* [out] */ __RPC__deref_out_opt IAppDomainCollection **ppAppDomainCollection);
        
        DECLSPEC_XFGVIRT(IProfilerManager, CreateSignatureBuilder)
        HRESULT ( STDMETHODCALLTYPE *CreateSignatureBuilder )( 
            __RPC__in IProfilerManager * This,
            /* [out] */ __RPC__deref_out_opt ISignatureBuilder **ppSignatureBuilder);
        
        DECLSPEC_XFGVIRT(IProfilerManager, GetInstrumentationMethod)
        HRESULT ( STDMETHODCALLTYPE *GetInstrumentationMethod )( 
            __RPC__in IProfilerManager * This,
            /* [in] */ __RPC__in REFGUID cslid,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppUnknown);
        
        DECLSPEC_XFGVIRT(IProfilerManager, RemoveInstrumentationMethod)
        HRESULT ( STDMETHODCALLTYPE *RemoveInstrumentationMethod )( 
            __RPC__in IProfilerManager * This,
            /* [in] */ __RPC__in_opt IInstrumentationMethod *pInstrumentationMethod);
        
        DECLSPEC_XFGVIRT(IProfilerManager, AddInstrumentationMethod)
        HRESULT ( STDMETHODCALLTYPE *AddInstrumentationMethod )( 
            __RPC__in IProfilerManager * This,
            /* [in] */ __RPC__in BSTR bstrModulePath,
            /* [in] */ __RPC__in BSTR bstrName,
            /* [in] */ __RPC__in BSTR bstrDescription,
            /* [in] */ __RPC__in BSTR bstrModule,
            /* [in] */ __RPC__in BSTR bstrClassGuid,
            /* [in] */ DWORD dwPriority,
            /* [out] */ __RPC__deref_out_opt IInstrumentationMethod **pInstrumentationMethod);
        
        DECLSPEC_XFGVIRT(IProfilerManager, GetRuntimeType)
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeType )( 
            __RPC__in IProfilerManager * This,
            /* [out] */ __RPC__out COR_PRF_RUNTIME_TYPE *pRuntimeType);
        
        END_INTERFACE
    } IProfilerManagerVtbl;

    interface IProfilerManager
    {
        CONST_VTBL struct IProfilerManagerVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IProfilerManager_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IProfilerManager_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IProfilerManager_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IProfilerManager_SetupProfilingEnvironment(This,bstrConfigPath,numConfigPaths)	\
    ( (This)->lpVtbl -> SetupProfilingEnvironment(This,bstrConfigPath,numConfigPaths) ) 

#define IProfilerManager_AddRawProfilerHook(This,pUnkProfilerCallback)	\
    ( (This)->lpVtbl -> AddRawProfilerHook(This,pUnkProfilerCallback) ) 

#define IProfilerManager_RemoveRawProfilerHook(This)	\
    ( (This)->lpVtbl -> RemoveRawProfilerHook(This) ) 

#define IProfilerManager_GetCorProfilerInfo(This,ppCorProfiler)	\
    ( (This)->lpVtbl -> GetCorProfilerInfo(This,ppCorProfiler) ) 

#define IProfilerManager_GetProfilerHost(This,ppProfilerManagerHost)	\
    ( (This)->lpVtbl -> GetProfilerHost(This,ppProfilerManagerHost) ) 

#define IProfilerManager_GetLoggingInstance(This,ppLogging)	\
    ( (This)->lpVtbl -> GetLoggingInstance(This,ppLogging) ) 

#define IProfilerManager_SetLoggingHost(This,pLoggingHost)	\
    ( (This)->lpVtbl -> SetLoggingHost(This,pLoggingHost) ) 

#define IProfilerManager_GetAppDomainCollection(This,ppAppDomainCollection)	\
    ( (This)->lpVtbl -> GetAppDomainCollection(This,ppAppDomainCollection) ) 

#define IProfilerManager_CreateSignatureBuilder(This,ppSignatureBuilder)	\
    ( (This)->lpVtbl -> CreateSignatureBuilder(This,ppSignatureBuilder) ) 

#define IProfilerManager_GetInstrumentationMethod(This,cslid,ppUnknown)	\
    ( (This)->lpVtbl -> GetInstrumentationMethod(This,cslid,ppUnknown) ) 

#define IProfilerManager_RemoveInstrumentationMethod(This,pInstrumentationMethod)	\
    ( (This)->lpVtbl -> RemoveInstrumentationMethod(This,pInstrumentationMethod) ) 

#define IProfilerManager_AddInstrumentationMethod(This,bstrModulePath,bstrName,bstrDescription,bstrModule,bstrClassGuid,dwPriority,pInstrumentationMethod)	\
    ( (This)->lpVtbl -> AddInstrumentationMethod(This,bstrModulePath,bstrName,bstrDescription,bstrModule,bstrClassGuid,dwPriority,pInstrumentationMethod) ) 

#define IProfilerManager_GetRuntimeType(This,pRuntimeType)	\
    ( (This)->lpVtbl -> GetRuntimeType(This,pRuntimeType) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IProfilerManager_INTERFACE_DEFINED__ */


#ifndef __IProfilerManagerHost_INTERFACE_DEFINED__
#define __IProfilerManagerHost_INTERFACE_DEFINED__

/* interface IProfilerManagerHost */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IProfilerManagerHost;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("BA9193B4-287F-4BF4-8E1B-00FCE33862EB")
    IProfilerManagerHost : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Initialize( 
            /* [in] */ __RPC__in_opt IProfilerManager *pProfilerManager) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IProfilerManagerHostVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IProfilerManagerHost * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IProfilerManagerHost * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IProfilerManagerHost * This);
        
        DECLSPEC_XFGVIRT(IProfilerManagerHost, Initialize)
        HRESULT ( STDMETHODCALLTYPE *Initialize )( 
            __RPC__in IProfilerManagerHost * This,
            /* [in] */ __RPC__in_opt IProfilerManager *pProfilerManager);
        
        END_INTERFACE
    } IProfilerManagerHostVtbl;

    interface IProfilerManagerHost
    {
        CONST_VTBL struct IProfilerManagerHostVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IProfilerManagerHost_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IProfilerManagerHost_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IProfilerManagerHost_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IProfilerManagerHost_Initialize(This,pProfilerManager)	\
    ( (This)->lpVtbl -> Initialize(This,pProfilerManager) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IProfilerManagerHost_INTERFACE_DEFINED__ */


#ifndef __IProfilerManagerLogging_INTERFACE_DEFINED__
#define __IProfilerManagerLogging_INTERFACE_DEFINED__

/* interface IProfilerManagerLogging */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IProfilerManagerLogging;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("9CFECED7-2123-4115-BF06-3693D1D19E22")
    IProfilerManagerLogging : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE LogMessage( 
            /* [in] */ __RPC__in const WCHAR *wszMessage) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE LogError( 
            /* [in] */ __RPC__in const WCHAR *wszError) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE LogDumpMessage( 
            /* [in] */ __RPC__in const WCHAR *wszMessage) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE EnableDiagnosticLogToDebugPort( 
            /* [in] */ BOOL enable) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetLoggingFlags( 
            /* [out] */ __RPC__out enum LoggingFlags *pLoggingFlags) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetLoggingFlags( 
            /* [in] */ enum LoggingFlags loggingFlags) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IProfilerManagerLoggingVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IProfilerManagerLogging * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IProfilerManagerLogging * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IProfilerManagerLogging * This);
        
        DECLSPEC_XFGVIRT(IProfilerManagerLogging, LogMessage)
        HRESULT ( STDMETHODCALLTYPE *LogMessage )( 
            __RPC__in IProfilerManagerLogging * This,
            /* [in] */ __RPC__in const WCHAR *wszMessage);
        
        DECLSPEC_XFGVIRT(IProfilerManagerLogging, LogError)
        HRESULT ( STDMETHODCALLTYPE *LogError )( 
            __RPC__in IProfilerManagerLogging * This,
            /* [in] */ __RPC__in const WCHAR *wszError);
        
        DECLSPEC_XFGVIRT(IProfilerManagerLogging, LogDumpMessage)
        HRESULT ( STDMETHODCALLTYPE *LogDumpMessage )( 
            __RPC__in IProfilerManagerLogging * This,
            /* [in] */ __RPC__in const WCHAR *wszMessage);
        
        DECLSPEC_XFGVIRT(IProfilerManagerLogging, EnableDiagnosticLogToDebugPort)
        HRESULT ( STDMETHODCALLTYPE *EnableDiagnosticLogToDebugPort )( 
            __RPC__in IProfilerManagerLogging * This,
            /* [in] */ BOOL enable);
        
        DECLSPEC_XFGVIRT(IProfilerManagerLogging, GetLoggingFlags)
        HRESULT ( STDMETHODCALLTYPE *GetLoggingFlags )( 
            __RPC__in IProfilerManagerLogging * This,
            /* [out] */ __RPC__out enum LoggingFlags *pLoggingFlags);
        
        DECLSPEC_XFGVIRT(IProfilerManagerLogging, SetLoggingFlags)
        HRESULT ( STDMETHODCALLTYPE *SetLoggingFlags )( 
            __RPC__in IProfilerManagerLogging * This,
            /* [in] */ enum LoggingFlags loggingFlags);
        
        END_INTERFACE
    } IProfilerManagerLoggingVtbl;

    interface IProfilerManagerLogging
    {
        CONST_VTBL struct IProfilerManagerLoggingVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IProfilerManagerLogging_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IProfilerManagerLogging_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IProfilerManagerLogging_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IProfilerManagerLogging_LogMessage(This,wszMessage)	\
    ( (This)->lpVtbl -> LogMessage(This,wszMessage) ) 

#define IProfilerManagerLogging_LogError(This,wszError)	\
    ( (This)->lpVtbl -> LogError(This,wszError) ) 

#define IProfilerManagerLogging_LogDumpMessage(This,wszMessage)	\
    ( (This)->lpVtbl -> LogDumpMessage(This,wszMessage) ) 

#define IProfilerManagerLogging_EnableDiagnosticLogToDebugPort(This,enable)	\
    ( (This)->lpVtbl -> EnableDiagnosticLogToDebugPort(This,enable) ) 

#define IProfilerManagerLogging_GetLoggingFlags(This,pLoggingFlags)	\
    ( (This)->lpVtbl -> GetLoggingFlags(This,pLoggingFlags) ) 

#define IProfilerManagerLogging_SetLoggingFlags(This,loggingFlags)	\
    ( (This)->lpVtbl -> SetLoggingFlags(This,loggingFlags) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IProfilerManagerLogging_INTERFACE_DEFINED__ */


#ifndef __IProfilerManagerLoggingHost_INTERFACE_DEFINED__
#define __IProfilerManagerLoggingHost_INTERFACE_DEFINED__

/* interface IProfilerManagerLoggingHost */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IProfilerManagerLoggingHost;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("99F828EE-EA00-473C-A829-D400235C11C1")
    IProfilerManagerLoggingHost : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE LogMessage( 
            /* [in] */ __RPC__in const WCHAR *wszMessage) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE LogError( 
            /* [in] */ __RPC__in const WCHAR *wszError) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE LogDumpMessage( 
            /* [in] */ __RPC__in const WCHAR *wszMessage) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IProfilerManagerLoggingHostVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IProfilerManagerLoggingHost * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IProfilerManagerLoggingHost * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IProfilerManagerLoggingHost * This);
        
        DECLSPEC_XFGVIRT(IProfilerManagerLoggingHost, LogMessage)
        HRESULT ( STDMETHODCALLTYPE *LogMessage )( 
            __RPC__in IProfilerManagerLoggingHost * This,
            /* [in] */ __RPC__in const WCHAR *wszMessage);
        
        DECLSPEC_XFGVIRT(IProfilerManagerLoggingHost, LogError)
        HRESULT ( STDMETHODCALLTYPE *LogError )( 
            __RPC__in IProfilerManagerLoggingHost * This,
            /* [in] */ __RPC__in const WCHAR *wszError);
        
        DECLSPEC_XFGVIRT(IProfilerManagerLoggingHost, LogDumpMessage)
        HRESULT ( STDMETHODCALLTYPE *LogDumpMessage )( 
            __RPC__in IProfilerManagerLoggingHost * This,
            /* [in] */ __RPC__in const WCHAR *wszMessage);
        
        END_INTERFACE
    } IProfilerManagerLoggingHostVtbl;

    interface IProfilerManagerLoggingHost
    {
        CONST_VTBL struct IProfilerManagerLoggingHostVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IProfilerManagerLoggingHost_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IProfilerManagerLoggingHost_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IProfilerManagerLoggingHost_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IProfilerManagerLoggingHost_LogMessage(This,wszMessage)	\
    ( (This)->lpVtbl -> LogMessage(This,wszMessage) ) 

#define IProfilerManagerLoggingHost_LogError(This,wszError)	\
    ( (This)->lpVtbl -> LogError(This,wszError) ) 

#define IProfilerManagerLoggingHost_LogDumpMessage(This,wszMessage)	\
    ( (This)->lpVtbl -> LogDumpMessage(This,wszMessage) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IProfilerManagerLoggingHost_INTERFACE_DEFINED__ */


#ifndef __IInstrumentationMethod_INTERFACE_DEFINED__
#define __IInstrumentationMethod_INTERFACE_DEFINED__

/* interface IInstrumentationMethod */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IInstrumentationMethod;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("0D92A8D9-6645-4803-B94B-06A1C4F4E633")
    IInstrumentationMethod : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Initialize( 
            /* [in] */ __RPC__in_opt IProfilerManager *pProfilerManager) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE OnAppDomainCreated( 
            /* [in] */ __RPC__in_opt IAppDomainInfo *pAppDomainInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE OnAppDomainShutdown( 
            /* [in] */ __RPC__in_opt IAppDomainInfo *pAppDomainInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE OnAssemblyLoaded( 
            /* [in] */ __RPC__in_opt IAssemblyInfo *pAssemblyInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE OnAssemblyUnloaded( 
            /* [in] */ __RPC__in_opt IAssemblyInfo *pAssemblyInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE OnModuleLoaded( 
            /* [in] */ __RPC__in_opt IModuleInfo *pModuleInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE OnModuleUnloaded( 
            /* [in] */ __RPC__in_opt IModuleInfo *pModuleInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE OnShutdown( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ShouldInstrumentMethod( 
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo,
            /* [in] */ BOOL isRejit,
            /* [out] */ __RPC__out BOOL *pbInstrument) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE BeforeInstrumentMethod( 
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo,
            /* [in] */ BOOL isRejit) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE InstrumentMethod( 
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo,
            /* [in] */ BOOL isRejit) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE OnInstrumentationComplete( 
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo,
            /* [in] */ BOOL isRejit) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AllowInlineSite( 
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfoInlinee,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfoCaller,
            /* [out] */ __RPC__out BOOL *pbAllowInline) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IInstrumentationMethodVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IInstrumentationMethod * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IInstrumentationMethod * This);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, Initialize)
        HRESULT ( STDMETHODCALLTYPE *Initialize )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in_opt IProfilerManager *pProfilerManager);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, OnAppDomainCreated)
        HRESULT ( STDMETHODCALLTYPE *OnAppDomainCreated )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in_opt IAppDomainInfo *pAppDomainInfo);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, OnAppDomainShutdown)
        HRESULT ( STDMETHODCALLTYPE *OnAppDomainShutdown )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in_opt IAppDomainInfo *pAppDomainInfo);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, OnAssemblyLoaded)
        HRESULT ( STDMETHODCALLTYPE *OnAssemblyLoaded )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in_opt IAssemblyInfo *pAssemblyInfo);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, OnAssemblyUnloaded)
        HRESULT ( STDMETHODCALLTYPE *OnAssemblyUnloaded )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in_opt IAssemblyInfo *pAssemblyInfo);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, OnModuleLoaded)
        HRESULT ( STDMETHODCALLTYPE *OnModuleLoaded )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in_opt IModuleInfo *pModuleInfo);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, OnModuleUnloaded)
        HRESULT ( STDMETHODCALLTYPE *OnModuleUnloaded )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in_opt IModuleInfo *pModuleInfo);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, OnShutdown)
        HRESULT ( STDMETHODCALLTYPE *OnShutdown )( 
            __RPC__in IInstrumentationMethod * This);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, ShouldInstrumentMethod)
        HRESULT ( STDMETHODCALLTYPE *ShouldInstrumentMethod )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo,
            /* [in] */ BOOL isRejit,
            /* [out] */ __RPC__out BOOL *pbInstrument);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, BeforeInstrumentMethod)
        HRESULT ( STDMETHODCALLTYPE *BeforeInstrumentMethod )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo,
            /* [in] */ BOOL isRejit);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, InstrumentMethod)
        HRESULT ( STDMETHODCALLTYPE *InstrumentMethod )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo,
            /* [in] */ BOOL isRejit);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, OnInstrumentationComplete)
        HRESULT ( STDMETHODCALLTYPE *OnInstrumentationComplete )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo,
            /* [in] */ BOOL isRejit);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethod, AllowInlineSite)
        HRESULT ( STDMETHODCALLTYPE *AllowInlineSite )( 
            __RPC__in IInstrumentationMethod * This,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfoInlinee,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfoCaller,
            /* [out] */ __RPC__out BOOL *pbAllowInline);
        
        END_INTERFACE
    } IInstrumentationMethodVtbl;

    interface IInstrumentationMethod
    {
        CONST_VTBL struct IInstrumentationMethodVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IInstrumentationMethod_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IInstrumentationMethod_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IInstrumentationMethod_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IInstrumentationMethod_Initialize(This,pProfilerManager)	\
    ( (This)->lpVtbl -> Initialize(This,pProfilerManager) ) 

#define IInstrumentationMethod_OnAppDomainCreated(This,pAppDomainInfo)	\
    ( (This)->lpVtbl -> OnAppDomainCreated(This,pAppDomainInfo) ) 

#define IInstrumentationMethod_OnAppDomainShutdown(This,pAppDomainInfo)	\
    ( (This)->lpVtbl -> OnAppDomainShutdown(This,pAppDomainInfo) ) 

#define IInstrumentationMethod_OnAssemblyLoaded(This,pAssemblyInfo)	\
    ( (This)->lpVtbl -> OnAssemblyLoaded(This,pAssemblyInfo) ) 

#define IInstrumentationMethod_OnAssemblyUnloaded(This,pAssemblyInfo)	\
    ( (This)->lpVtbl -> OnAssemblyUnloaded(This,pAssemblyInfo) ) 

#define IInstrumentationMethod_OnModuleLoaded(This,pModuleInfo)	\
    ( (This)->lpVtbl -> OnModuleLoaded(This,pModuleInfo) ) 

#define IInstrumentationMethod_OnModuleUnloaded(This,pModuleInfo)	\
    ( (This)->lpVtbl -> OnModuleUnloaded(This,pModuleInfo) ) 

#define IInstrumentationMethod_OnShutdown(This)	\
    ( (This)->lpVtbl -> OnShutdown(This) ) 

#define IInstrumentationMethod_ShouldInstrumentMethod(This,pMethodInfo,isRejit,pbInstrument)	\
    ( (This)->lpVtbl -> ShouldInstrumentMethod(This,pMethodInfo,isRejit,pbInstrument) ) 

#define IInstrumentationMethod_BeforeInstrumentMethod(This,pMethodInfo,isRejit)	\
    ( (This)->lpVtbl -> BeforeInstrumentMethod(This,pMethodInfo,isRejit) ) 

#define IInstrumentationMethod_InstrumentMethod(This,pMethodInfo,isRejit)	\
    ( (This)->lpVtbl -> InstrumentMethod(This,pMethodInfo,isRejit) ) 

#define IInstrumentationMethod_OnInstrumentationComplete(This,pMethodInfo,isRejit)	\
    ( (This)->lpVtbl -> OnInstrumentationComplete(This,pMethodInfo,isRejit) ) 

#define IInstrumentationMethod_AllowInlineSite(This,pMethodInfoInlinee,pMethodInfoCaller,pbAllowInline)	\
    ( (This)->lpVtbl -> AllowInlineSite(This,pMethodInfoInlinee,pMethodInfoCaller,pbAllowInline) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IInstrumentationMethod_INTERFACE_DEFINED__ */


#ifndef __IDataContainer_INTERFACE_DEFINED__
#define __IDataContainer_INTERFACE_DEFINED__

/* interface IDataContainer */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IDataContainer;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("2A4FDF66-FC5B-442D-8FAA-4137F023A4EA")
    IDataContainer : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE SetDataItem( 
            /* [in] */ __RPC__in const GUID *componentId,
            /* [in] */ __RPC__in const GUID *objectGuid,
            /* [in] */ __RPC__in_opt IUnknown *pDataItem) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetDataItem( 
            /* [in] */ __RPC__in const GUID *componentId,
            /* [in] */ __RPC__in const GUID *objectGuid,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppDataItem) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IDataContainerVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IDataContainer * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IDataContainer * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IDataContainer * This);
        
        DECLSPEC_XFGVIRT(IDataContainer, SetDataItem)
        HRESULT ( STDMETHODCALLTYPE *SetDataItem )( 
            __RPC__in IDataContainer * This,
            /* [in] */ __RPC__in const GUID *componentId,
            /* [in] */ __RPC__in const GUID *objectGuid,
            /* [in] */ __RPC__in_opt IUnknown *pDataItem);
        
        DECLSPEC_XFGVIRT(IDataContainer, GetDataItem)
        HRESULT ( STDMETHODCALLTYPE *GetDataItem )( 
            __RPC__in IDataContainer * This,
            /* [in] */ __RPC__in const GUID *componentId,
            /* [in] */ __RPC__in const GUID *objectGuid,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppDataItem);
        
        END_INTERFACE
    } IDataContainerVtbl;

    interface IDataContainer
    {
        CONST_VTBL struct IDataContainerVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IDataContainer_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IDataContainer_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IDataContainer_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IDataContainer_SetDataItem(This,componentId,objectGuid,pDataItem)	\
    ( (This)->lpVtbl -> SetDataItem(This,componentId,objectGuid,pDataItem) ) 

#define IDataContainer_GetDataItem(This,componentId,objectGuid,ppDataItem)	\
    ( (This)->lpVtbl -> GetDataItem(This,componentId,objectGuid,ppDataItem) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IDataContainer_INTERFACE_DEFINED__ */


#ifndef __IInstruction_INTERFACE_DEFINED__
#define __IInstruction_INTERFACE_DEFINED__

/* interface IInstruction */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IInstruction;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("E80D8434-2976-4242-8F3B-0C837C343F6C")
    IInstruction : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetOffset( 
            /* [out] */ __RPC__out DWORD *pdwOffset) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOriginalOffset( 
            /* [out] */ __RPC__out DWORD *pdwOffset) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOpCodeName( 
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOpCode( 
            /* [out] */ __RPC__out enum ILOrdinalOpcode *pOpCode) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetAlternateOrdinalOpcode( 
            /* [out] */ __RPC__out enum ILOrdinalOpcode *pOpCode) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetInstructionLength( 
            /* [out] */ __RPC__out DWORD *pdwLength) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOpcodeFlags( 
            /* [out] */ __RPC__out enum ILOpcodeFlags *pFlags) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOpcodeLength( 
            /* [out] */ __RPC__out DWORD *pdwLength) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOperandType( 
            /* [out] */ __RPC__out enum ILOperandType *pType) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOperandLength( 
            /* [out] */ __RPC__out DWORD *pdwLength) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsNew( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsRemoved( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsBranch( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsSwitch( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsCallInstruction( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetTerminationType( 
            /* [out] */ __RPC__out enum InstructionTerminationType *pTerminationType) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsFallThrough( 
            /* [out] */ __RPC__out BOOL *pbIsFallThrough) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetNextInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppNextInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetPreviousInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppPrevInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOriginalNextInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppNextInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOriginalPreviousInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppPrevInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetInstructionGeneration( 
            /* [out] */ __RPC__out enum InstructionGeneration *pInstructionGeneration) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IInstructionVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IInstruction * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IInstruction * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IInstruction * This);
        
        DECLSPEC_XFGVIRT(IInstruction, GetOffset)
        HRESULT ( STDMETHODCALLTYPE *GetOffset )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out DWORD *pdwOffset);
        
        DECLSPEC_XFGVIRT(IInstruction, GetOriginalOffset)
        HRESULT ( STDMETHODCALLTYPE *GetOriginalOffset )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out DWORD *pdwOffset);
        
        DECLSPEC_XFGVIRT(IInstruction, GetOpCodeName)
        HRESULT ( STDMETHODCALLTYPE *GetOpCodeName )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName);
        
        DECLSPEC_XFGVIRT(IInstruction, GetOpCode)
        HRESULT ( STDMETHODCALLTYPE *GetOpCode )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out enum ILOrdinalOpcode *pOpCode);
        
        DECLSPEC_XFGVIRT(IInstruction, GetAlternateOrdinalOpcode)
        HRESULT ( STDMETHODCALLTYPE *GetAlternateOrdinalOpcode )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out enum ILOrdinalOpcode *pOpCode);
        
        DECLSPEC_XFGVIRT(IInstruction, GetInstructionLength)
        HRESULT ( STDMETHODCALLTYPE *GetInstructionLength )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out DWORD *pdwLength);
        
        DECLSPEC_XFGVIRT(IInstruction, GetOpcodeFlags)
        HRESULT ( STDMETHODCALLTYPE *GetOpcodeFlags )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out enum ILOpcodeFlags *pFlags);
        
        DECLSPEC_XFGVIRT(IInstruction, GetOpcodeLength)
        HRESULT ( STDMETHODCALLTYPE *GetOpcodeLength )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out DWORD *pdwLength);
        
        DECLSPEC_XFGVIRT(IInstruction, GetOperandType)
        HRESULT ( STDMETHODCALLTYPE *GetOperandType )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out enum ILOperandType *pType);
        
        DECLSPEC_XFGVIRT(IInstruction, GetOperandLength)
        HRESULT ( STDMETHODCALLTYPE *GetOperandLength )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out DWORD *pdwLength);
        
        DECLSPEC_XFGVIRT(IInstruction, GetIsNew)
        HRESULT ( STDMETHODCALLTYPE *GetIsNew )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IInstruction, GetIsRemoved)
        HRESULT ( STDMETHODCALLTYPE *GetIsRemoved )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IInstruction, GetIsBranch)
        HRESULT ( STDMETHODCALLTYPE *GetIsBranch )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IInstruction, GetIsSwitch)
        HRESULT ( STDMETHODCALLTYPE *GetIsSwitch )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IInstruction, GetIsCallInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetIsCallInstruction )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IInstruction, GetTerminationType)
        HRESULT ( STDMETHODCALLTYPE *GetTerminationType )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out enum InstructionTerminationType *pTerminationType);
        
        DECLSPEC_XFGVIRT(IInstruction, GetIsFallThrough)
        HRESULT ( STDMETHODCALLTYPE *GetIsFallThrough )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out BOOL *pbIsFallThrough);
        
        DECLSPEC_XFGVIRT(IInstruction, GetNextInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetNextInstruction )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppNextInstruction);
        
        DECLSPEC_XFGVIRT(IInstruction, GetPreviousInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetPreviousInstruction )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppPrevInstruction);
        
        DECLSPEC_XFGVIRT(IInstruction, GetOriginalNextInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetOriginalNextInstruction )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppNextInstruction);
        
        DECLSPEC_XFGVIRT(IInstruction, GetOriginalPreviousInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetOriginalPreviousInstruction )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppPrevInstruction);
        
        DECLSPEC_XFGVIRT(IInstruction, GetInstructionGeneration)
        HRESULT ( STDMETHODCALLTYPE *GetInstructionGeneration )( 
            __RPC__in IInstruction * This,
            /* [out] */ __RPC__out enum InstructionGeneration *pInstructionGeneration);
        
        END_INTERFACE
    } IInstructionVtbl;

    interface IInstruction
    {
        CONST_VTBL struct IInstructionVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IInstruction_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IInstruction_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IInstruction_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IInstruction_GetOffset(This,pdwOffset)	\
    ( (This)->lpVtbl -> GetOffset(This,pdwOffset) ) 

#define IInstruction_GetOriginalOffset(This,pdwOffset)	\
    ( (This)->lpVtbl -> GetOriginalOffset(This,pdwOffset) ) 

#define IInstruction_GetOpCodeName(This,pbstrName)	\
    ( (This)->lpVtbl -> GetOpCodeName(This,pbstrName) ) 

#define IInstruction_GetOpCode(This,pOpCode)	\
    ( (This)->lpVtbl -> GetOpCode(This,pOpCode) ) 

#define IInstruction_GetAlternateOrdinalOpcode(This,pOpCode)	\
    ( (This)->lpVtbl -> GetAlternateOrdinalOpcode(This,pOpCode) ) 

#define IInstruction_GetInstructionLength(This,pdwLength)	\
    ( (This)->lpVtbl -> GetInstructionLength(This,pdwLength) ) 

#define IInstruction_GetOpcodeFlags(This,pFlags)	\
    ( (This)->lpVtbl -> GetOpcodeFlags(This,pFlags) ) 

#define IInstruction_GetOpcodeLength(This,pdwLength)	\
    ( (This)->lpVtbl -> GetOpcodeLength(This,pdwLength) ) 

#define IInstruction_GetOperandType(This,pType)	\
    ( (This)->lpVtbl -> GetOperandType(This,pType) ) 

#define IInstruction_GetOperandLength(This,pdwLength)	\
    ( (This)->lpVtbl -> GetOperandLength(This,pdwLength) ) 

#define IInstruction_GetIsNew(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsNew(This,pbValue) ) 

#define IInstruction_GetIsRemoved(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsRemoved(This,pbValue) ) 

#define IInstruction_GetIsBranch(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsBranch(This,pbValue) ) 

#define IInstruction_GetIsSwitch(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsSwitch(This,pbValue) ) 

#define IInstruction_GetIsCallInstruction(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsCallInstruction(This,pbValue) ) 

#define IInstruction_GetTerminationType(This,pTerminationType)	\
    ( (This)->lpVtbl -> GetTerminationType(This,pTerminationType) ) 

#define IInstruction_GetIsFallThrough(This,pbIsFallThrough)	\
    ( (This)->lpVtbl -> GetIsFallThrough(This,pbIsFallThrough) ) 

#define IInstruction_GetNextInstruction(This,ppNextInstruction)	\
    ( (This)->lpVtbl -> GetNextInstruction(This,ppNextInstruction) ) 

#define IInstruction_GetPreviousInstruction(This,ppPrevInstruction)	\
    ( (This)->lpVtbl -> GetPreviousInstruction(This,ppPrevInstruction) ) 

#define IInstruction_GetOriginalNextInstruction(This,ppNextInstruction)	\
    ( (This)->lpVtbl -> GetOriginalNextInstruction(This,ppNextInstruction) ) 

#define IInstruction_GetOriginalPreviousInstruction(This,ppPrevInstruction)	\
    ( (This)->lpVtbl -> GetOriginalPreviousInstruction(This,ppPrevInstruction) ) 

#define IInstruction_GetInstructionGeneration(This,pInstructionGeneration)	\
    ( (This)->lpVtbl -> GetInstructionGeneration(This,pInstructionGeneration) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IInstruction_INTERFACE_DEFINED__ */


#ifndef __IExceptionSection_INTERFACE_DEFINED__
#define __IExceptionSection_INTERFACE_DEFINED__

/* interface IExceptionSection */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IExceptionSection;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("42CE95A2-F814-4DCD-952F-68CE9801FDD3")
    IExceptionSection : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetMethodInfo( 
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetExceptionClauses( 
            /* [out] */ __RPC__deref_out_opt IEnumExceptionClauses **ppEnumExceptionClauses) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddExceptionClause( 
            /* [in] */ __RPC__in_opt IExceptionClause *pExceptionClause) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE RemoveExceptionClause( 
            /* [in] */ __RPC__in_opt IExceptionClause *pExceptionClause) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE RemoveAllExceptionClauses( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddNewExceptionClause( 
            /* [in] */ DWORD flags,
            /* [in] */ __RPC__in_opt IInstruction *pTryFirstInstruction,
            /* [in] */ __RPC__in_opt IInstruction *pTryLastInstruction,
            /* [in] */ __RPC__in_opt IInstruction *pHandlerFirstInstruction,
            /* [in] */ __RPC__in_opt IInstruction *pHandlerLastInstruction,
            /* [in] */ __RPC__in_opt IInstruction *pFilterLastInstruction,
            /* [in] */ mdToken handlerTypeToken,
            /* [out] */ __RPC__deref_out_opt IExceptionClause **ppExceptionClause) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IExceptionSectionVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IExceptionSection * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IExceptionSection * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IExceptionSection * This);
        
        DECLSPEC_XFGVIRT(IExceptionSection, GetMethodInfo)
        HRESULT ( STDMETHODCALLTYPE *GetMethodInfo )( 
            __RPC__in IExceptionSection * This,
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo);
        
        DECLSPEC_XFGVIRT(IExceptionSection, GetExceptionClauses)
        HRESULT ( STDMETHODCALLTYPE *GetExceptionClauses )( 
            __RPC__in IExceptionSection * This,
            /* [out] */ __RPC__deref_out_opt IEnumExceptionClauses **ppEnumExceptionClauses);
        
        DECLSPEC_XFGVIRT(IExceptionSection, AddExceptionClause)
        HRESULT ( STDMETHODCALLTYPE *AddExceptionClause )( 
            __RPC__in IExceptionSection * This,
            /* [in] */ __RPC__in_opt IExceptionClause *pExceptionClause);
        
        DECLSPEC_XFGVIRT(IExceptionSection, RemoveExceptionClause)
        HRESULT ( STDMETHODCALLTYPE *RemoveExceptionClause )( 
            __RPC__in IExceptionSection * This,
            /* [in] */ __RPC__in_opt IExceptionClause *pExceptionClause);
        
        DECLSPEC_XFGVIRT(IExceptionSection, RemoveAllExceptionClauses)
        HRESULT ( STDMETHODCALLTYPE *RemoveAllExceptionClauses )( 
            __RPC__in IExceptionSection * This);
        
        DECLSPEC_XFGVIRT(IExceptionSection, AddNewExceptionClause)
        HRESULT ( STDMETHODCALLTYPE *AddNewExceptionClause )( 
            __RPC__in IExceptionSection * This,
            /* [in] */ DWORD flags,
            /* [in] */ __RPC__in_opt IInstruction *pTryFirstInstruction,
            /* [in] */ __RPC__in_opt IInstruction *pTryLastInstruction,
            /* [in] */ __RPC__in_opt IInstruction *pHandlerFirstInstruction,
            /* [in] */ __RPC__in_opt IInstruction *pHandlerLastInstruction,
            /* [in] */ __RPC__in_opt IInstruction *pFilterLastInstruction,
            /* [in] */ mdToken handlerTypeToken,
            /* [out] */ __RPC__deref_out_opt IExceptionClause **ppExceptionClause);
        
        END_INTERFACE
    } IExceptionSectionVtbl;

    interface IExceptionSection
    {
        CONST_VTBL struct IExceptionSectionVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IExceptionSection_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IExceptionSection_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IExceptionSection_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IExceptionSection_GetMethodInfo(This,ppMethodInfo)	\
    ( (This)->lpVtbl -> GetMethodInfo(This,ppMethodInfo) ) 

#define IExceptionSection_GetExceptionClauses(This,ppEnumExceptionClauses)	\
    ( (This)->lpVtbl -> GetExceptionClauses(This,ppEnumExceptionClauses) ) 

#define IExceptionSection_AddExceptionClause(This,pExceptionClause)	\
    ( (This)->lpVtbl -> AddExceptionClause(This,pExceptionClause) ) 

#define IExceptionSection_RemoveExceptionClause(This,pExceptionClause)	\
    ( (This)->lpVtbl -> RemoveExceptionClause(This,pExceptionClause) ) 

#define IExceptionSection_RemoveAllExceptionClauses(This)	\
    ( (This)->lpVtbl -> RemoveAllExceptionClauses(This) ) 

#define IExceptionSection_AddNewExceptionClause(This,flags,pTryFirstInstruction,pTryLastInstruction,pHandlerFirstInstruction,pHandlerLastInstruction,pFilterLastInstruction,handlerTypeToken,ppExceptionClause)	\
    ( (This)->lpVtbl -> AddNewExceptionClause(This,flags,pTryFirstInstruction,pTryLastInstruction,pHandlerFirstInstruction,pHandlerLastInstruction,pFilterLastInstruction,handlerTypeToken,ppExceptionClause) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IExceptionSection_INTERFACE_DEFINED__ */


#ifndef __IExceptionClause_INTERFACE_DEFINED__
#define __IExceptionClause_INTERFACE_DEFINED__

/* interface IExceptionClause */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IExceptionClause;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("1D57EAF6-FCFE-4874-AA0E-C9D1DF714950")
    IExceptionClause : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetFlags( 
            /* [out] */ __RPC__out DWORD *pFlags) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetTryFirstInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetTryLastInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetHandlerFirstInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetHandlerLastInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetFilterFirstInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetExceptionHandlerType( 
            /* [out] */ __RPC__out mdToken *pToken) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetFlags( 
            /* [in] */ DWORD flags) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetTryFirstInstruction( 
            /* [in] */ __RPC__in_opt IInstruction *pInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetTryLastInstruction( 
            /* [in] */ __RPC__in_opt IInstruction *pInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetHandlerFirstInstruction( 
            /* [in] */ __RPC__in_opt IInstruction *pInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetHandlerLastInstruction( 
            /* [in] */ __RPC__in_opt IInstruction *pInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetFilterFirstInstruction( 
            /* [in] */ __RPC__in_opt IInstruction *pInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetExceptionHandlerType( 
            /* [in] */ mdToken token) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IExceptionClauseVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IExceptionClause * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IExceptionClause * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IExceptionClause * This);
        
        DECLSPEC_XFGVIRT(IExceptionClause, GetFlags)
        HRESULT ( STDMETHODCALLTYPE *GetFlags )( 
            __RPC__in IExceptionClause * This,
            /* [out] */ __RPC__out DWORD *pFlags);
        
        DECLSPEC_XFGVIRT(IExceptionClause, GetTryFirstInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetTryFirstInstruction )( 
            __RPC__in IExceptionClause * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IExceptionClause, GetTryLastInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetTryLastInstruction )( 
            __RPC__in IExceptionClause * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IExceptionClause, GetHandlerFirstInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetHandlerFirstInstruction )( 
            __RPC__in IExceptionClause * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IExceptionClause, GetHandlerLastInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetHandlerLastInstruction )( 
            __RPC__in IExceptionClause * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IExceptionClause, GetFilterFirstInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetFilterFirstInstruction )( 
            __RPC__in IExceptionClause * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IExceptionClause, GetExceptionHandlerType)
        HRESULT ( STDMETHODCALLTYPE *GetExceptionHandlerType )( 
            __RPC__in IExceptionClause * This,
            /* [out] */ __RPC__out mdToken *pToken);
        
        DECLSPEC_XFGVIRT(IExceptionClause, SetFlags)
        HRESULT ( STDMETHODCALLTYPE *SetFlags )( 
            __RPC__in IExceptionClause * This,
            /* [in] */ DWORD flags);
        
        DECLSPEC_XFGVIRT(IExceptionClause, SetTryFirstInstruction)
        HRESULT ( STDMETHODCALLTYPE *SetTryFirstInstruction )( 
            __RPC__in IExceptionClause * This,
            /* [in] */ __RPC__in_opt IInstruction *pInstruction);
        
        DECLSPEC_XFGVIRT(IExceptionClause, SetTryLastInstruction)
        HRESULT ( STDMETHODCALLTYPE *SetTryLastInstruction )( 
            __RPC__in IExceptionClause * This,
            /* [in] */ __RPC__in_opt IInstruction *pInstruction);
        
        DECLSPEC_XFGVIRT(IExceptionClause, SetHandlerFirstInstruction)
        HRESULT ( STDMETHODCALLTYPE *SetHandlerFirstInstruction )( 
            __RPC__in IExceptionClause * This,
            /* [in] */ __RPC__in_opt IInstruction *pInstruction);
        
        DECLSPEC_XFGVIRT(IExceptionClause, SetHandlerLastInstruction)
        HRESULT ( STDMETHODCALLTYPE *SetHandlerLastInstruction )( 
            __RPC__in IExceptionClause * This,
            /* [in] */ __RPC__in_opt IInstruction *pInstruction);
        
        DECLSPEC_XFGVIRT(IExceptionClause, SetFilterFirstInstruction)
        HRESULT ( STDMETHODCALLTYPE *SetFilterFirstInstruction )( 
            __RPC__in IExceptionClause * This,
            /* [in] */ __RPC__in_opt IInstruction *pInstruction);
        
        DECLSPEC_XFGVIRT(IExceptionClause, SetExceptionHandlerType)
        HRESULT ( STDMETHODCALLTYPE *SetExceptionHandlerType )( 
            __RPC__in IExceptionClause * This,
            /* [in] */ mdToken token);
        
        END_INTERFACE
    } IExceptionClauseVtbl;

    interface IExceptionClause
    {
        CONST_VTBL struct IExceptionClauseVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IExceptionClause_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IExceptionClause_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IExceptionClause_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IExceptionClause_GetFlags(This,pFlags)	\
    ( (This)->lpVtbl -> GetFlags(This,pFlags) ) 

#define IExceptionClause_GetTryFirstInstruction(This,ppInstruction)	\
    ( (This)->lpVtbl -> GetTryFirstInstruction(This,ppInstruction) ) 

#define IExceptionClause_GetTryLastInstruction(This,ppInstruction)	\
    ( (This)->lpVtbl -> GetTryLastInstruction(This,ppInstruction) ) 

#define IExceptionClause_GetHandlerFirstInstruction(This,ppInstruction)	\
    ( (This)->lpVtbl -> GetHandlerFirstInstruction(This,ppInstruction) ) 

#define IExceptionClause_GetHandlerLastInstruction(This,ppInstruction)	\
    ( (This)->lpVtbl -> GetHandlerLastInstruction(This,ppInstruction) ) 

#define IExceptionClause_GetFilterFirstInstruction(This,ppInstruction)	\
    ( (This)->lpVtbl -> GetFilterFirstInstruction(This,ppInstruction) ) 

#define IExceptionClause_GetExceptionHandlerType(This,pToken)	\
    ( (This)->lpVtbl -> GetExceptionHandlerType(This,pToken) ) 

#define IExceptionClause_SetFlags(This,flags)	\
    ( (This)->lpVtbl -> SetFlags(This,flags) ) 

#define IExceptionClause_SetTryFirstInstruction(This,pInstruction)	\
    ( (This)->lpVtbl -> SetTryFirstInstruction(This,pInstruction) ) 

#define IExceptionClause_SetTryLastInstruction(This,pInstruction)	\
    ( (This)->lpVtbl -> SetTryLastInstruction(This,pInstruction) ) 

#define IExceptionClause_SetHandlerFirstInstruction(This,pInstruction)	\
    ( (This)->lpVtbl -> SetHandlerFirstInstruction(This,pInstruction) ) 

#define IExceptionClause_SetHandlerLastInstruction(This,pInstruction)	\
    ( (This)->lpVtbl -> SetHandlerLastInstruction(This,pInstruction) ) 

#define IExceptionClause_SetFilterFirstInstruction(This,pInstruction)	\
    ( (This)->lpVtbl -> SetFilterFirstInstruction(This,pInstruction) ) 

#define IExceptionClause_SetExceptionHandlerType(This,token)	\
    ( (This)->lpVtbl -> SetExceptionHandlerType(This,token) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IExceptionClause_INTERFACE_DEFINED__ */


#ifndef __IEnumExceptionClauses_INTERFACE_DEFINED__
#define __IEnumExceptionClauses_INTERFACE_DEFINED__

/* interface IEnumExceptionClauses */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IEnumExceptionClauses;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("85B0B99F-73D7-4C69-8659-BF6196F5264F")
    IEnumExceptionClauses : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Next( 
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IExceptionClause **rgelt,
            /* [in] */ __RPC__in ULONG *pceltFetched) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Skip( 
            /* [in] */ ULONG celt) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Reset( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Clone( 
            /* [out] */ __RPC__deref_out_opt IEnumExceptionClauses **ppenum) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCount( 
            /* [out] */ __RPC__out DWORD *pLength) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IEnumExceptionClausesVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IEnumExceptionClauses * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IEnumExceptionClauses * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IEnumExceptionClauses * This);
        
        DECLSPEC_XFGVIRT(IEnumExceptionClauses, Next)
        HRESULT ( STDMETHODCALLTYPE *Next )( 
            __RPC__in IEnumExceptionClauses * This,
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IExceptionClause **rgelt,
            /* [in] */ __RPC__in ULONG *pceltFetched);
        
        DECLSPEC_XFGVIRT(IEnumExceptionClauses, Skip)
        HRESULT ( STDMETHODCALLTYPE *Skip )( 
            __RPC__in IEnumExceptionClauses * This,
            /* [in] */ ULONG celt);
        
        DECLSPEC_XFGVIRT(IEnumExceptionClauses, Reset)
        HRESULT ( STDMETHODCALLTYPE *Reset )( 
            __RPC__in IEnumExceptionClauses * This);
        
        DECLSPEC_XFGVIRT(IEnumExceptionClauses, Clone)
        HRESULT ( STDMETHODCALLTYPE *Clone )( 
            __RPC__in IEnumExceptionClauses * This,
            /* [out] */ __RPC__deref_out_opt IEnumExceptionClauses **ppenum);
        
        DECLSPEC_XFGVIRT(IEnumExceptionClauses, GetCount)
        HRESULT ( STDMETHODCALLTYPE *GetCount )( 
            __RPC__in IEnumExceptionClauses * This,
            /* [out] */ __RPC__out DWORD *pLength);
        
        END_INTERFACE
    } IEnumExceptionClausesVtbl;

    interface IEnumExceptionClauses
    {
        CONST_VTBL struct IEnumExceptionClausesVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IEnumExceptionClauses_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IEnumExceptionClauses_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IEnumExceptionClauses_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IEnumExceptionClauses_Next(This,celt,rgelt,pceltFetched)	\
    ( (This)->lpVtbl -> Next(This,celt,rgelt,pceltFetched) ) 

#define IEnumExceptionClauses_Skip(This,celt)	\
    ( (This)->lpVtbl -> Skip(This,celt) ) 

#define IEnumExceptionClauses_Reset(This)	\
    ( (This)->lpVtbl -> Reset(This) ) 

#define IEnumExceptionClauses_Clone(This,ppenum)	\
    ( (This)->lpVtbl -> Clone(This,ppenum) ) 

#define IEnumExceptionClauses_GetCount(This,pLength)	\
    ( (This)->lpVtbl -> GetCount(This,pLength) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IEnumExceptionClauses_INTERFACE_DEFINED__ */


#ifndef __IOperandInstruction_INTERFACE_DEFINED__
#define __IOperandInstruction_INTERFACE_DEFINED__

/* interface IOperandInstruction */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IOperandInstruction;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("1F014299-F383-46CE-B7A6-1982C85F9FEA")
    IOperandInstruction : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetOperandType( 
            /* [out] */ __RPC__out enum ILOperandType *pType) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOperandValue( 
            /* [in] */ DWORD dwSize,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(dwSize, dwSize) BYTE *pBytes) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetOperandValue( 
            /* [in] */ DWORD dwSize,
            /* [length_is][size_is][in] */ __RPC__in_ecount_part(dwSize, dwSize) BYTE *pBytes) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IOperandInstructionVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IOperandInstruction * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IOperandInstruction * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IOperandInstruction * This);
        
        DECLSPEC_XFGVIRT(IOperandInstruction, GetOperandType)
        HRESULT ( STDMETHODCALLTYPE *GetOperandType )( 
            __RPC__in IOperandInstruction * This,
            /* [out] */ __RPC__out enum ILOperandType *pType);
        
        DECLSPEC_XFGVIRT(IOperandInstruction, GetOperandValue)
        HRESULT ( STDMETHODCALLTYPE *GetOperandValue )( 
            __RPC__in IOperandInstruction * This,
            /* [in] */ DWORD dwSize,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(dwSize, dwSize) BYTE *pBytes);
        
        DECLSPEC_XFGVIRT(IOperandInstruction, SetOperandValue)
        HRESULT ( STDMETHODCALLTYPE *SetOperandValue )( 
            __RPC__in IOperandInstruction * This,
            /* [in] */ DWORD dwSize,
            /* [length_is][size_is][in] */ __RPC__in_ecount_part(dwSize, dwSize) BYTE *pBytes);
        
        END_INTERFACE
    } IOperandInstructionVtbl;

    interface IOperandInstruction
    {
        CONST_VTBL struct IOperandInstructionVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IOperandInstruction_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IOperandInstruction_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IOperandInstruction_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IOperandInstruction_GetOperandType(This,pType)	\
    ( (This)->lpVtbl -> GetOperandType(This,pType) ) 

#define IOperandInstruction_GetOperandValue(This,dwSize,pBytes)	\
    ( (This)->lpVtbl -> GetOperandValue(This,dwSize,pBytes) ) 

#define IOperandInstruction_SetOperandValue(This,dwSize,pBytes)	\
    ( (This)->lpVtbl -> SetOperandValue(This,dwSize,pBytes) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IOperandInstruction_INTERFACE_DEFINED__ */


#ifndef __IBranchInstruction_INTERFACE_DEFINED__
#define __IBranchInstruction_INTERFACE_DEFINED__

/* interface IBranchInstruction */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IBranchInstruction;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("73728F9D-B4B5-4149-8396-A79C4726636E")
    IBranchInstruction : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE IsShortBranch( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExpandBranch( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetBranchTarget( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppTarget) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetTargetOffset( 
            /* [out] */ __RPC__out DWORD *pOffset) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetBranchTarget( 
            /* [in] */ __RPC__in_opt IInstruction *pTarget) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IBranchInstructionVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IBranchInstruction * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IBranchInstruction * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IBranchInstruction * This);
        
        DECLSPEC_XFGVIRT(IBranchInstruction, IsShortBranch)
        HRESULT ( STDMETHODCALLTYPE *IsShortBranch )( 
            __RPC__in IBranchInstruction * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IBranchInstruction, ExpandBranch)
        HRESULT ( STDMETHODCALLTYPE *ExpandBranch )( 
            __RPC__in IBranchInstruction * This);
        
        DECLSPEC_XFGVIRT(IBranchInstruction, GetBranchTarget)
        HRESULT ( STDMETHODCALLTYPE *GetBranchTarget )( 
            __RPC__in IBranchInstruction * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppTarget);
        
        DECLSPEC_XFGVIRT(IBranchInstruction, GetTargetOffset)
        HRESULT ( STDMETHODCALLTYPE *GetTargetOffset )( 
            __RPC__in IBranchInstruction * This,
            /* [out] */ __RPC__out DWORD *pOffset);
        
        DECLSPEC_XFGVIRT(IBranchInstruction, SetBranchTarget)
        HRESULT ( STDMETHODCALLTYPE *SetBranchTarget )( 
            __RPC__in IBranchInstruction * This,
            /* [in] */ __RPC__in_opt IInstruction *pTarget);
        
        END_INTERFACE
    } IBranchInstructionVtbl;

    interface IBranchInstruction
    {
        CONST_VTBL struct IBranchInstructionVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IBranchInstruction_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IBranchInstruction_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IBranchInstruction_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IBranchInstruction_IsShortBranch(This,pbValue)	\
    ( (This)->lpVtbl -> IsShortBranch(This,pbValue) ) 

#define IBranchInstruction_ExpandBranch(This)	\
    ( (This)->lpVtbl -> ExpandBranch(This) ) 

#define IBranchInstruction_GetBranchTarget(This,ppTarget)	\
    ( (This)->lpVtbl -> GetBranchTarget(This,ppTarget) ) 

#define IBranchInstruction_GetTargetOffset(This,pOffset)	\
    ( (This)->lpVtbl -> GetTargetOffset(This,pOffset) ) 

#define IBranchInstruction_SetBranchTarget(This,pTarget)	\
    ( (This)->lpVtbl -> SetBranchTarget(This,pTarget) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IBranchInstruction_INTERFACE_DEFINED__ */


#ifndef __ISwitchInstruction_INTERFACE_DEFINED__
#define __ISwitchInstruction_INTERFACE_DEFINED__

/* interface ISwitchInstruction */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ISwitchInstruction;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("66B79035-4F18-4689-A16D-95AF469460A3")
    ISwitchInstruction : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetBranchTarget( 
            /* [in] */ DWORD index,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppTarget) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetBranchTarget( 
            /* [in] */ DWORD index,
            /* [in] */ __RPC__in_opt IInstruction *pTarget) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE RemoveBranchTargetAt( 
            /* [in] */ DWORD index) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE RemoveBranchTarget( 
            /* [in] */ __RPC__in_opt IInstruction *pTarget) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ReplaceBranchTarget( 
            /* [in] */ __RPC__in_opt IInstruction *pOriginal,
            /* [in] */ __RPC__in_opt IInstruction *pNew) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetBranchCount( 
            /* [out] */ __RPC__out DWORD *pBranchCount) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetBranchOffset( 
            /* [in] */ DWORD index,
            /* [out] */ __RPC__out DWORD *pdwOffset) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ISwitchInstructionVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in ISwitchInstruction * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in ISwitchInstruction * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in ISwitchInstruction * This);
        
        DECLSPEC_XFGVIRT(ISwitchInstruction, GetBranchTarget)
        HRESULT ( STDMETHODCALLTYPE *GetBranchTarget )( 
            __RPC__in ISwitchInstruction * This,
            /* [in] */ DWORD index,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppTarget);
        
        DECLSPEC_XFGVIRT(ISwitchInstruction, SetBranchTarget)
        HRESULT ( STDMETHODCALLTYPE *SetBranchTarget )( 
            __RPC__in ISwitchInstruction * This,
            /* [in] */ DWORD index,
            /* [in] */ __RPC__in_opt IInstruction *pTarget);
        
        DECLSPEC_XFGVIRT(ISwitchInstruction, RemoveBranchTargetAt)
        HRESULT ( STDMETHODCALLTYPE *RemoveBranchTargetAt )( 
            __RPC__in ISwitchInstruction * This,
            /* [in] */ DWORD index);
        
        DECLSPEC_XFGVIRT(ISwitchInstruction, RemoveBranchTarget)
        HRESULT ( STDMETHODCALLTYPE *RemoveBranchTarget )( 
            __RPC__in ISwitchInstruction * This,
            /* [in] */ __RPC__in_opt IInstruction *pTarget);
        
        DECLSPEC_XFGVIRT(ISwitchInstruction, ReplaceBranchTarget)
        HRESULT ( STDMETHODCALLTYPE *ReplaceBranchTarget )( 
            __RPC__in ISwitchInstruction * This,
            /* [in] */ __RPC__in_opt IInstruction *pOriginal,
            /* [in] */ __RPC__in_opt IInstruction *pNew);
        
        DECLSPEC_XFGVIRT(ISwitchInstruction, GetBranchCount)
        HRESULT ( STDMETHODCALLTYPE *GetBranchCount )( 
            __RPC__in ISwitchInstruction * This,
            /* [out] */ __RPC__out DWORD *pBranchCount);
        
        DECLSPEC_XFGVIRT(ISwitchInstruction, GetBranchOffset)
        HRESULT ( STDMETHODCALLTYPE *GetBranchOffset )( 
            __RPC__in ISwitchInstruction * This,
            /* [in] */ DWORD index,
            /* [out] */ __RPC__out DWORD *pdwOffset);
        
        END_INTERFACE
    } ISwitchInstructionVtbl;

    interface ISwitchInstruction
    {
        CONST_VTBL struct ISwitchInstructionVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ISwitchInstruction_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ISwitchInstruction_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ISwitchInstruction_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ISwitchInstruction_GetBranchTarget(This,index,ppTarget)	\
    ( (This)->lpVtbl -> GetBranchTarget(This,index,ppTarget) ) 

#define ISwitchInstruction_SetBranchTarget(This,index,pTarget)	\
    ( (This)->lpVtbl -> SetBranchTarget(This,index,pTarget) ) 

#define ISwitchInstruction_RemoveBranchTargetAt(This,index)	\
    ( (This)->lpVtbl -> RemoveBranchTargetAt(This,index) ) 

#define ISwitchInstruction_RemoveBranchTarget(This,pTarget)	\
    ( (This)->lpVtbl -> RemoveBranchTarget(This,pTarget) ) 

#define ISwitchInstruction_ReplaceBranchTarget(This,pOriginal,pNew)	\
    ( (This)->lpVtbl -> ReplaceBranchTarget(This,pOriginal,pNew) ) 

#define ISwitchInstruction_GetBranchCount(This,pBranchCount)	\
    ( (This)->lpVtbl -> GetBranchCount(This,pBranchCount) ) 

#define ISwitchInstruction_GetBranchOffset(This,index,pdwOffset)	\
    ( (This)->lpVtbl -> GetBranchOffset(This,index,pdwOffset) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ISwitchInstruction_INTERFACE_DEFINED__ */


#ifndef __IInstructionGraph_INTERFACE_DEFINED__
#define __IInstructionGraph_INTERFACE_DEFINED__

/* interface IInstructionGraph */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IInstructionGraph;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("9165F2D1-2D6D-4B89-B2AB-2CACA66CAA48")
    IInstructionGraph : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetMethodInfo( 
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetFirstInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetLastInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOriginalFirstInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOriginalLastInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetUninstrumentedFirstInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetUninstrumentedLastInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetInstructionAtOffset( 
            /* [in] */ DWORD offset,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetInstructionAtOriginalOffset( 
            /* [in] */ DWORD offset,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetInstructionAtUninstrumentedOffset( 
            /* [in] */ DWORD dwOffset,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE InsertBefore( 
            /* [in] */ __RPC__in_opt IInstruction *pInstructionOrig,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionNew) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE InsertAfter( 
            /* [in] */ __RPC__in_opt IInstruction *pInstructionOrig,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionNew) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE InsertBeforeAndRetargetOffsets( 
            /* [in] */ __RPC__in_opt IInstruction *pInstructionOrig,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionNew) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Replace( 
            /* [in] */ __RPC__in_opt IInstruction *pInstructionOrig,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionNew) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Remove( 
            /* [in] */ __RPC__in_opt IInstruction *pInstructionOrig) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE RemoveAll( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateBaseline( 
            /* [in] */ __RPC__in LPCBYTE pCodeBase,
            /* [in] */ __RPC__in LPCBYTE pEndOfCode,
            /* [in] */ DWORD originalToBaselineCorIlMapSize,
            /* [size_is][in] */ __RPC__in_ecount_full(originalToBaselineCorIlMapSize) COR_IL_MAP originalToBaselineCorIlMap[  ],
            /* [in] */ DWORD baselineSequencePointSize,
            /* [size_is][in] */ __RPC__in_ecount_full(baselineSequencePointSize) DWORD baselineSequencePointList[  ]) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE HasBaselineBeenSet( 
            /* [out] */ __RPC__out BOOL *pHasBaselineBeenSet) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExpandBranches( void) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IInstructionGraphVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IInstructionGraph * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IInstructionGraph * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IInstructionGraph * This);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, GetMethodInfo)
        HRESULT ( STDMETHODCALLTYPE *GetMethodInfo )( 
            __RPC__in IInstructionGraph * This,
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, GetFirstInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetFirstInstruction )( 
            __RPC__in IInstructionGraph * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, GetLastInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetLastInstruction )( 
            __RPC__in IInstructionGraph * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, GetOriginalFirstInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetOriginalFirstInstruction )( 
            __RPC__in IInstructionGraph * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, GetOriginalLastInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetOriginalLastInstruction )( 
            __RPC__in IInstructionGraph * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, GetUninstrumentedFirstInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetUninstrumentedFirstInstruction )( 
            __RPC__in IInstructionGraph * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, GetUninstrumentedLastInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetUninstrumentedLastInstruction )( 
            __RPC__in IInstructionGraph * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, GetInstructionAtOffset)
        HRESULT ( STDMETHODCALLTYPE *GetInstructionAtOffset )( 
            __RPC__in IInstructionGraph * This,
            /* [in] */ DWORD offset,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, GetInstructionAtOriginalOffset)
        HRESULT ( STDMETHODCALLTYPE *GetInstructionAtOriginalOffset )( 
            __RPC__in IInstructionGraph * This,
            /* [in] */ DWORD offset,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, GetInstructionAtUninstrumentedOffset)
        HRESULT ( STDMETHODCALLTYPE *GetInstructionAtUninstrumentedOffset )( 
            __RPC__in IInstructionGraph * This,
            /* [in] */ DWORD dwOffset,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, InsertBefore)
        HRESULT ( STDMETHODCALLTYPE *InsertBefore )( 
            __RPC__in IInstructionGraph * This,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionOrig,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionNew);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, InsertAfter)
        HRESULT ( STDMETHODCALLTYPE *InsertAfter )( 
            __RPC__in IInstructionGraph * This,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionOrig,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionNew);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, InsertBeforeAndRetargetOffsets)
        HRESULT ( STDMETHODCALLTYPE *InsertBeforeAndRetargetOffsets )( 
            __RPC__in IInstructionGraph * This,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionOrig,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionNew);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, Replace)
        HRESULT ( STDMETHODCALLTYPE *Replace )( 
            __RPC__in IInstructionGraph * This,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionOrig,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionNew);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, Remove)
        HRESULT ( STDMETHODCALLTYPE *Remove )( 
            __RPC__in IInstructionGraph * This,
            /* [in] */ __RPC__in_opt IInstruction *pInstructionOrig);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, RemoveAll)
        HRESULT ( STDMETHODCALLTYPE *RemoveAll )( 
            __RPC__in IInstructionGraph * This);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, CreateBaseline)
        HRESULT ( STDMETHODCALLTYPE *CreateBaseline )( 
            __RPC__in IInstructionGraph * This,
            /* [in] */ __RPC__in LPCBYTE pCodeBase,
            /* [in] */ __RPC__in LPCBYTE pEndOfCode,
            /* [in] */ DWORD originalToBaselineCorIlMapSize,
            /* [size_is][in] */ __RPC__in_ecount_full(originalToBaselineCorIlMapSize) COR_IL_MAP originalToBaselineCorIlMap[  ],
            /* [in] */ DWORD baselineSequencePointSize,
            /* [size_is][in] */ __RPC__in_ecount_full(baselineSequencePointSize) DWORD baselineSequencePointList[  ]);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, HasBaselineBeenSet)
        HRESULT ( STDMETHODCALLTYPE *HasBaselineBeenSet )( 
            __RPC__in IInstructionGraph * This,
            /* [out] */ __RPC__out BOOL *pHasBaselineBeenSet);
        
        DECLSPEC_XFGVIRT(IInstructionGraph, ExpandBranches)
        HRESULT ( STDMETHODCALLTYPE *ExpandBranches )( 
            __RPC__in IInstructionGraph * This);
        
        END_INTERFACE
    } IInstructionGraphVtbl;

    interface IInstructionGraph
    {
        CONST_VTBL struct IInstructionGraphVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IInstructionGraph_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IInstructionGraph_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IInstructionGraph_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IInstructionGraph_GetMethodInfo(This,ppMethodInfo)	\
    ( (This)->lpVtbl -> GetMethodInfo(This,ppMethodInfo) ) 

#define IInstructionGraph_GetFirstInstruction(This,ppInstruction)	\
    ( (This)->lpVtbl -> GetFirstInstruction(This,ppInstruction) ) 

#define IInstructionGraph_GetLastInstruction(This,ppInstruction)	\
    ( (This)->lpVtbl -> GetLastInstruction(This,ppInstruction) ) 

#define IInstructionGraph_GetOriginalFirstInstruction(This,ppInstruction)	\
    ( (This)->lpVtbl -> GetOriginalFirstInstruction(This,ppInstruction) ) 

#define IInstructionGraph_GetOriginalLastInstruction(This,ppInstruction)	\
    ( (This)->lpVtbl -> GetOriginalLastInstruction(This,ppInstruction) ) 

#define IInstructionGraph_GetUninstrumentedFirstInstruction(This,ppInstruction)	\
    ( (This)->lpVtbl -> GetUninstrumentedFirstInstruction(This,ppInstruction) ) 

#define IInstructionGraph_GetUninstrumentedLastInstruction(This,ppInstruction)	\
    ( (This)->lpVtbl -> GetUninstrumentedLastInstruction(This,ppInstruction) ) 

#define IInstructionGraph_GetInstructionAtOffset(This,offset,ppInstruction)	\
    ( (This)->lpVtbl -> GetInstructionAtOffset(This,offset,ppInstruction) ) 

#define IInstructionGraph_GetInstructionAtOriginalOffset(This,offset,ppInstruction)	\
    ( (This)->lpVtbl -> GetInstructionAtOriginalOffset(This,offset,ppInstruction) ) 

#define IInstructionGraph_GetInstructionAtUninstrumentedOffset(This,dwOffset,ppInstruction)	\
    ( (This)->lpVtbl -> GetInstructionAtUninstrumentedOffset(This,dwOffset,ppInstruction) ) 

#define IInstructionGraph_InsertBefore(This,pInstructionOrig,pInstructionNew)	\
    ( (This)->lpVtbl -> InsertBefore(This,pInstructionOrig,pInstructionNew) ) 

#define IInstructionGraph_InsertAfter(This,pInstructionOrig,pInstructionNew)	\
    ( (This)->lpVtbl -> InsertAfter(This,pInstructionOrig,pInstructionNew) ) 

#define IInstructionGraph_InsertBeforeAndRetargetOffsets(This,pInstructionOrig,pInstructionNew)	\
    ( (This)->lpVtbl -> InsertBeforeAndRetargetOffsets(This,pInstructionOrig,pInstructionNew) ) 

#define IInstructionGraph_Replace(This,pInstructionOrig,pInstructionNew)	\
    ( (This)->lpVtbl -> Replace(This,pInstructionOrig,pInstructionNew) ) 

#define IInstructionGraph_Remove(This,pInstructionOrig)	\
    ( (This)->lpVtbl -> Remove(This,pInstructionOrig) ) 

#define IInstructionGraph_RemoveAll(This)	\
    ( (This)->lpVtbl -> RemoveAll(This) ) 

#define IInstructionGraph_CreateBaseline(This,pCodeBase,pEndOfCode,originalToBaselineCorIlMapSize,originalToBaselineCorIlMap,baselineSequencePointSize,baselineSequencePointList)	\
    ( (This)->lpVtbl -> CreateBaseline(This,pCodeBase,pEndOfCode,originalToBaselineCorIlMapSize,originalToBaselineCorIlMap,baselineSequencePointSize,baselineSequencePointList) ) 

#define IInstructionGraph_HasBaselineBeenSet(This,pHasBaselineBeenSet)	\
    ( (This)->lpVtbl -> HasBaselineBeenSet(This,pHasBaselineBeenSet) ) 

#define IInstructionGraph_ExpandBranches(This)	\
    ( (This)->lpVtbl -> ExpandBranches(This) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IInstructionGraph_INTERFACE_DEFINED__ */


#ifndef __IMethodInfo_INTERFACE_DEFINED__
#define __IMethodInfo_INTERFACE_DEFINED__

/* interface IMethodInfo */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IMethodInfo;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("CC21A894-F4DF-4726-8318-D6C24C4985B1")
    IMethodInfo : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetModuleInfo( 
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetName( 
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetFullName( 
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrFullName) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetInstructions( 
            /* [out] */ __RPC__deref_out_opt IInstructionGraph **ppInstructionGraph) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetLocalVariables( 
            /* [out] */ __RPC__deref_out_opt ILocalVariableCollection **ppLocalVariables) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetClassId( 
            /* [out] */ __RPC__out ClassID *pClassId) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetFunctionId( 
            /* [out] */ __RPC__out FunctionID *pFunctionID) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMethodToken( 
            /* [out] */ __RPC__out mdToken *pToken) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetGenericParameterCount( 
            /* [out] */ __RPC__out DWORD *pCount) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsStatic( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsPublic( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsPrivate( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsPropertyGetter( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsPropertySetter( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsFinalizer( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsConstructor( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsStaticConstructor( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetParameters( 
            /* [out] */ __RPC__deref_out_opt IEnumMethodParameters **pMethodArgs) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetDeclaringType( 
            /* [out] */ __RPC__deref_out_opt IType **ppType) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetReturnType( 
            /* [out] */ __RPC__deref_out_opt IType **ppType) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCorSignature( 
            /* [in] */ DWORD cbBuffer,
            /* [size_is][length_is][out] */ __RPC__out_ecount_part(cbBuffer, cbBuffer) BYTE *pCorSignature,
            /* [out] */ __RPC__out DWORD *pcbSignature) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetLocalVarSigToken( 
            /* [out] */ __RPC__out mdToken *pToken) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetLocalVarSigToken( 
            /* [in] */ mdToken token) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetAttributes( 
            /* [out] */ __RPC__out DWORD *pCorMethodAttr) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetRejitCodeGenFlags( 
            /* [out] */ __RPC__out DWORD *pRefitFlags) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCodeRva( 
            /* [out] */ __RPC__out DWORD *pRva) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE MethodImplFlags( 
            /* [out] */ __RPC__out UINT *pCorMethodImpl) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetRejitCodeGenFlags( 
            /* [in] */ DWORD dwFlags) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetExceptionSection( 
            /* [out] */ __RPC__deref_out_opt IExceptionSection **ppExceptionSection) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetInstructionFactory( 
            /* [out] */ __RPC__deref_out_opt IInstructionFactory **ppInstructionFactory) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetRejitCount( 
            /* [out] */ __RPC__out DWORD *pdwRejitCount) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMaxStack( 
            /* [out] */ __RPC__out DWORD *pMaxStack) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetSingleRetDefaultInstrumentation( 
            /* [out] */ __RPC__deref_out_opt ISingleRetDefaultInstrumentation **ppSingleRetDefaultInstrumentation) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IMethodInfoVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IMethodInfo * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IMethodInfo * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IMethodInfo * This);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetModuleInfo)
        HRESULT ( STDMETHODCALLTYPE *GetModuleInfo )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetName)
        HRESULT ( STDMETHODCALLTYPE *GetName )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetFullName)
        HRESULT ( STDMETHODCALLTYPE *GetFullName )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrFullName);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetInstructions)
        HRESULT ( STDMETHODCALLTYPE *GetInstructions )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__deref_out_opt IInstructionGraph **ppInstructionGraph);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetLocalVariables)
        HRESULT ( STDMETHODCALLTYPE *GetLocalVariables )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__deref_out_opt ILocalVariableCollection **ppLocalVariables);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetClassId)
        HRESULT ( STDMETHODCALLTYPE *GetClassId )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out ClassID *pClassId);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetFunctionId)
        HRESULT ( STDMETHODCALLTYPE *GetFunctionId )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out FunctionID *pFunctionID);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetMethodToken)
        HRESULT ( STDMETHODCALLTYPE *GetMethodToken )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out mdToken *pToken);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetGenericParameterCount)
        HRESULT ( STDMETHODCALLTYPE *GetGenericParameterCount )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out DWORD *pCount);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsStatic)
        HRESULT ( STDMETHODCALLTYPE *GetIsStatic )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsPublic)
        HRESULT ( STDMETHODCALLTYPE *GetIsPublic )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsPrivate)
        HRESULT ( STDMETHODCALLTYPE *GetIsPrivate )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsPropertyGetter)
        HRESULT ( STDMETHODCALLTYPE *GetIsPropertyGetter )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsPropertySetter)
        HRESULT ( STDMETHODCALLTYPE *GetIsPropertySetter )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsFinalizer)
        HRESULT ( STDMETHODCALLTYPE *GetIsFinalizer )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsConstructor)
        HRESULT ( STDMETHODCALLTYPE *GetIsConstructor )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsStaticConstructor)
        HRESULT ( STDMETHODCALLTYPE *GetIsStaticConstructor )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetParameters)
        HRESULT ( STDMETHODCALLTYPE *GetParameters )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__deref_out_opt IEnumMethodParameters **pMethodArgs);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetDeclaringType)
        HRESULT ( STDMETHODCALLTYPE *GetDeclaringType )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__deref_out_opt IType **ppType);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetReturnType)
        HRESULT ( STDMETHODCALLTYPE *GetReturnType )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__deref_out_opt IType **ppType);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetCorSignature)
        HRESULT ( STDMETHODCALLTYPE *GetCorSignature )( 
            __RPC__in IMethodInfo * This,
            /* [in] */ DWORD cbBuffer,
            /* [size_is][length_is][out] */ __RPC__out_ecount_part(cbBuffer, cbBuffer) BYTE *pCorSignature,
            /* [out] */ __RPC__out DWORD *pcbSignature);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetLocalVarSigToken)
        HRESULT ( STDMETHODCALLTYPE *GetLocalVarSigToken )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out mdToken *pToken);
        
        DECLSPEC_XFGVIRT(IMethodInfo, SetLocalVarSigToken)
        HRESULT ( STDMETHODCALLTYPE *SetLocalVarSigToken )( 
            __RPC__in IMethodInfo * This,
            /* [in] */ mdToken token);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetAttributes)
        HRESULT ( STDMETHODCALLTYPE *GetAttributes )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out DWORD *pCorMethodAttr);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetRejitCodeGenFlags)
        HRESULT ( STDMETHODCALLTYPE *GetRejitCodeGenFlags )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out DWORD *pRefitFlags);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetCodeRva)
        HRESULT ( STDMETHODCALLTYPE *GetCodeRva )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out DWORD *pRva);
        
        DECLSPEC_XFGVIRT(IMethodInfo, MethodImplFlags)
        HRESULT ( STDMETHODCALLTYPE *MethodImplFlags )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out UINT *pCorMethodImpl);
        
        DECLSPEC_XFGVIRT(IMethodInfo, SetRejitCodeGenFlags)
        HRESULT ( STDMETHODCALLTYPE *SetRejitCodeGenFlags )( 
            __RPC__in IMethodInfo * This,
            /* [in] */ DWORD dwFlags);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetExceptionSection)
        HRESULT ( STDMETHODCALLTYPE *GetExceptionSection )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__deref_out_opt IExceptionSection **ppExceptionSection);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetInstructionFactory)
        HRESULT ( STDMETHODCALLTYPE *GetInstructionFactory )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__deref_out_opt IInstructionFactory **ppInstructionFactory);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetRejitCount)
        HRESULT ( STDMETHODCALLTYPE *GetRejitCount )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out DWORD *pdwRejitCount);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetMaxStack)
        HRESULT ( STDMETHODCALLTYPE *GetMaxStack )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__out DWORD *pMaxStack);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetSingleRetDefaultInstrumentation)
        HRESULT ( STDMETHODCALLTYPE *GetSingleRetDefaultInstrumentation )( 
            __RPC__in IMethodInfo * This,
            /* [out] */ __RPC__deref_out_opt ISingleRetDefaultInstrumentation **ppSingleRetDefaultInstrumentation);
        
        END_INTERFACE
    } IMethodInfoVtbl;

    interface IMethodInfo
    {
        CONST_VTBL struct IMethodInfoVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IMethodInfo_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IMethodInfo_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IMethodInfo_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IMethodInfo_GetModuleInfo(This,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetModuleInfo(This,ppModuleInfo) ) 

#define IMethodInfo_GetName(This,pbstrName)	\
    ( (This)->lpVtbl -> GetName(This,pbstrName) ) 

#define IMethodInfo_GetFullName(This,pbstrFullName)	\
    ( (This)->lpVtbl -> GetFullName(This,pbstrFullName) ) 

#define IMethodInfo_GetInstructions(This,ppInstructionGraph)	\
    ( (This)->lpVtbl -> GetInstructions(This,ppInstructionGraph) ) 

#define IMethodInfo_GetLocalVariables(This,ppLocalVariables)	\
    ( (This)->lpVtbl -> GetLocalVariables(This,ppLocalVariables) ) 

#define IMethodInfo_GetClassId(This,pClassId)	\
    ( (This)->lpVtbl -> GetClassId(This,pClassId) ) 

#define IMethodInfo_GetFunctionId(This,pFunctionID)	\
    ( (This)->lpVtbl -> GetFunctionId(This,pFunctionID) ) 

#define IMethodInfo_GetMethodToken(This,pToken)	\
    ( (This)->lpVtbl -> GetMethodToken(This,pToken) ) 

#define IMethodInfo_GetGenericParameterCount(This,pCount)	\
    ( (This)->lpVtbl -> GetGenericParameterCount(This,pCount) ) 

#define IMethodInfo_GetIsStatic(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsStatic(This,pbValue) ) 

#define IMethodInfo_GetIsPublic(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsPublic(This,pbValue) ) 

#define IMethodInfo_GetIsPrivate(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsPrivate(This,pbValue) ) 

#define IMethodInfo_GetIsPropertyGetter(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsPropertyGetter(This,pbValue) ) 

#define IMethodInfo_GetIsPropertySetter(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsPropertySetter(This,pbValue) ) 

#define IMethodInfo_GetIsFinalizer(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsFinalizer(This,pbValue) ) 

#define IMethodInfo_GetIsConstructor(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsConstructor(This,pbValue) ) 

#define IMethodInfo_GetIsStaticConstructor(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsStaticConstructor(This,pbValue) ) 

#define IMethodInfo_GetParameters(This,pMethodArgs)	\
    ( (This)->lpVtbl -> GetParameters(This,pMethodArgs) ) 

#define IMethodInfo_GetDeclaringType(This,ppType)	\
    ( (This)->lpVtbl -> GetDeclaringType(This,ppType) ) 

#define IMethodInfo_GetReturnType(This,ppType)	\
    ( (This)->lpVtbl -> GetReturnType(This,ppType) ) 

#define IMethodInfo_GetCorSignature(This,cbBuffer,pCorSignature,pcbSignature)	\
    ( (This)->lpVtbl -> GetCorSignature(This,cbBuffer,pCorSignature,pcbSignature) ) 

#define IMethodInfo_GetLocalVarSigToken(This,pToken)	\
    ( (This)->lpVtbl -> GetLocalVarSigToken(This,pToken) ) 

#define IMethodInfo_SetLocalVarSigToken(This,token)	\
    ( (This)->lpVtbl -> SetLocalVarSigToken(This,token) ) 

#define IMethodInfo_GetAttributes(This,pCorMethodAttr)	\
    ( (This)->lpVtbl -> GetAttributes(This,pCorMethodAttr) ) 

#define IMethodInfo_GetRejitCodeGenFlags(This,pRefitFlags)	\
    ( (This)->lpVtbl -> GetRejitCodeGenFlags(This,pRefitFlags) ) 

#define IMethodInfo_GetCodeRva(This,pRva)	\
    ( (This)->lpVtbl -> GetCodeRva(This,pRva) ) 

#define IMethodInfo_MethodImplFlags(This,pCorMethodImpl)	\
    ( (This)->lpVtbl -> MethodImplFlags(This,pCorMethodImpl) ) 

#define IMethodInfo_SetRejitCodeGenFlags(This,dwFlags)	\
    ( (This)->lpVtbl -> SetRejitCodeGenFlags(This,dwFlags) ) 

#define IMethodInfo_GetExceptionSection(This,ppExceptionSection)	\
    ( (This)->lpVtbl -> GetExceptionSection(This,ppExceptionSection) ) 

#define IMethodInfo_GetInstructionFactory(This,ppInstructionFactory)	\
    ( (This)->lpVtbl -> GetInstructionFactory(This,ppInstructionFactory) ) 

#define IMethodInfo_GetRejitCount(This,pdwRejitCount)	\
    ( (This)->lpVtbl -> GetRejitCount(This,pdwRejitCount) ) 

#define IMethodInfo_GetMaxStack(This,pMaxStack)	\
    ( (This)->lpVtbl -> GetMaxStack(This,pMaxStack) ) 

#define IMethodInfo_GetSingleRetDefaultInstrumentation(This,ppSingleRetDefaultInstrumentation)	\
    ( (This)->lpVtbl -> GetSingleRetDefaultInstrumentation(This,ppSingleRetDefaultInstrumentation) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IMethodInfo_INTERFACE_DEFINED__ */


#ifndef __IMethodInfo2_INTERFACE_DEFINED__
#define __IMethodInfo2_INTERFACE_DEFINED__

/* interface IMethodInfo2 */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IMethodInfo2;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("CDF098F7-D04A-4B58-B46E-184C4F223E5F")
    IMethodInfo2 : public IMethodInfo
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetOriginalLocalVariables( 
            /* [out] */ __RPC__deref_out_opt ILocalVariableCollection **ppLocalVariables) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IMethodInfo2Vtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IMethodInfo2 * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IMethodInfo2 * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IMethodInfo2 * This);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetModuleInfo)
        HRESULT ( STDMETHODCALLTYPE *GetModuleInfo )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetName)
        HRESULT ( STDMETHODCALLTYPE *GetName )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetFullName)
        HRESULT ( STDMETHODCALLTYPE *GetFullName )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrFullName);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetInstructions)
        HRESULT ( STDMETHODCALLTYPE *GetInstructions )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IInstructionGraph **ppInstructionGraph);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetLocalVariables)
        HRESULT ( STDMETHODCALLTYPE *GetLocalVariables )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__deref_out_opt ILocalVariableCollection **ppLocalVariables);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetClassId)
        HRESULT ( STDMETHODCALLTYPE *GetClassId )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out ClassID *pClassId);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetFunctionId)
        HRESULT ( STDMETHODCALLTYPE *GetFunctionId )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out FunctionID *pFunctionID);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetMethodToken)
        HRESULT ( STDMETHODCALLTYPE *GetMethodToken )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out mdToken *pToken);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetGenericParameterCount)
        HRESULT ( STDMETHODCALLTYPE *GetGenericParameterCount )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out DWORD *pCount);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsStatic)
        HRESULT ( STDMETHODCALLTYPE *GetIsStatic )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsPublic)
        HRESULT ( STDMETHODCALLTYPE *GetIsPublic )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsPrivate)
        HRESULT ( STDMETHODCALLTYPE *GetIsPrivate )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsPropertyGetter)
        HRESULT ( STDMETHODCALLTYPE *GetIsPropertyGetter )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsPropertySetter)
        HRESULT ( STDMETHODCALLTYPE *GetIsPropertySetter )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsFinalizer)
        HRESULT ( STDMETHODCALLTYPE *GetIsFinalizer )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsConstructor)
        HRESULT ( STDMETHODCALLTYPE *GetIsConstructor )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetIsStaticConstructor)
        HRESULT ( STDMETHODCALLTYPE *GetIsStaticConstructor )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetParameters)
        HRESULT ( STDMETHODCALLTYPE *GetParameters )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IEnumMethodParameters **pMethodArgs);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetDeclaringType)
        HRESULT ( STDMETHODCALLTYPE *GetDeclaringType )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IType **ppType);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetReturnType)
        HRESULT ( STDMETHODCALLTYPE *GetReturnType )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IType **ppType);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetCorSignature)
        HRESULT ( STDMETHODCALLTYPE *GetCorSignature )( 
            __RPC__in IMethodInfo2 * This,
            /* [in] */ DWORD cbBuffer,
            /* [size_is][length_is][out] */ __RPC__out_ecount_part(cbBuffer, cbBuffer) BYTE *pCorSignature,
            /* [out] */ __RPC__out DWORD *pcbSignature);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetLocalVarSigToken)
        HRESULT ( STDMETHODCALLTYPE *GetLocalVarSigToken )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out mdToken *pToken);
        
        DECLSPEC_XFGVIRT(IMethodInfo, SetLocalVarSigToken)
        HRESULT ( STDMETHODCALLTYPE *SetLocalVarSigToken )( 
            __RPC__in IMethodInfo2 * This,
            /* [in] */ mdToken token);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetAttributes)
        HRESULT ( STDMETHODCALLTYPE *GetAttributes )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out DWORD *pCorMethodAttr);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetRejitCodeGenFlags)
        HRESULT ( STDMETHODCALLTYPE *GetRejitCodeGenFlags )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out DWORD *pRefitFlags);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetCodeRva)
        HRESULT ( STDMETHODCALLTYPE *GetCodeRva )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out DWORD *pRva);
        
        DECLSPEC_XFGVIRT(IMethodInfo, MethodImplFlags)
        HRESULT ( STDMETHODCALLTYPE *MethodImplFlags )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out UINT *pCorMethodImpl);
        
        DECLSPEC_XFGVIRT(IMethodInfo, SetRejitCodeGenFlags)
        HRESULT ( STDMETHODCALLTYPE *SetRejitCodeGenFlags )( 
            __RPC__in IMethodInfo2 * This,
            /* [in] */ DWORD dwFlags);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetExceptionSection)
        HRESULT ( STDMETHODCALLTYPE *GetExceptionSection )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IExceptionSection **ppExceptionSection);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetInstructionFactory)
        HRESULT ( STDMETHODCALLTYPE *GetInstructionFactory )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IInstructionFactory **ppInstructionFactory);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetRejitCount)
        HRESULT ( STDMETHODCALLTYPE *GetRejitCount )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out DWORD *pdwRejitCount);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetMaxStack)
        HRESULT ( STDMETHODCALLTYPE *GetMaxStack )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__out DWORD *pMaxStack);
        
        DECLSPEC_XFGVIRT(IMethodInfo, GetSingleRetDefaultInstrumentation)
        HRESULT ( STDMETHODCALLTYPE *GetSingleRetDefaultInstrumentation )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__deref_out_opt ISingleRetDefaultInstrumentation **ppSingleRetDefaultInstrumentation);
        
        DECLSPEC_XFGVIRT(IMethodInfo2, GetOriginalLocalVariables)
        HRESULT ( STDMETHODCALLTYPE *GetOriginalLocalVariables )( 
            __RPC__in IMethodInfo2 * This,
            /* [out] */ __RPC__deref_out_opt ILocalVariableCollection **ppLocalVariables);
        
        END_INTERFACE
    } IMethodInfo2Vtbl;

    interface IMethodInfo2
    {
        CONST_VTBL struct IMethodInfo2Vtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IMethodInfo2_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IMethodInfo2_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IMethodInfo2_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IMethodInfo2_GetModuleInfo(This,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetModuleInfo(This,ppModuleInfo) ) 

#define IMethodInfo2_GetName(This,pbstrName)	\
    ( (This)->lpVtbl -> GetName(This,pbstrName) ) 

#define IMethodInfo2_GetFullName(This,pbstrFullName)	\
    ( (This)->lpVtbl -> GetFullName(This,pbstrFullName) ) 

#define IMethodInfo2_GetInstructions(This,ppInstructionGraph)	\
    ( (This)->lpVtbl -> GetInstructions(This,ppInstructionGraph) ) 

#define IMethodInfo2_GetLocalVariables(This,ppLocalVariables)	\
    ( (This)->lpVtbl -> GetLocalVariables(This,ppLocalVariables) ) 

#define IMethodInfo2_GetClassId(This,pClassId)	\
    ( (This)->lpVtbl -> GetClassId(This,pClassId) ) 

#define IMethodInfo2_GetFunctionId(This,pFunctionID)	\
    ( (This)->lpVtbl -> GetFunctionId(This,pFunctionID) ) 

#define IMethodInfo2_GetMethodToken(This,pToken)	\
    ( (This)->lpVtbl -> GetMethodToken(This,pToken) ) 

#define IMethodInfo2_GetGenericParameterCount(This,pCount)	\
    ( (This)->lpVtbl -> GetGenericParameterCount(This,pCount) ) 

#define IMethodInfo2_GetIsStatic(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsStatic(This,pbValue) ) 

#define IMethodInfo2_GetIsPublic(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsPublic(This,pbValue) ) 

#define IMethodInfo2_GetIsPrivate(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsPrivate(This,pbValue) ) 

#define IMethodInfo2_GetIsPropertyGetter(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsPropertyGetter(This,pbValue) ) 

#define IMethodInfo2_GetIsPropertySetter(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsPropertySetter(This,pbValue) ) 

#define IMethodInfo2_GetIsFinalizer(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsFinalizer(This,pbValue) ) 

#define IMethodInfo2_GetIsConstructor(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsConstructor(This,pbValue) ) 

#define IMethodInfo2_GetIsStaticConstructor(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsStaticConstructor(This,pbValue) ) 

#define IMethodInfo2_GetParameters(This,pMethodArgs)	\
    ( (This)->lpVtbl -> GetParameters(This,pMethodArgs) ) 

#define IMethodInfo2_GetDeclaringType(This,ppType)	\
    ( (This)->lpVtbl -> GetDeclaringType(This,ppType) ) 

#define IMethodInfo2_GetReturnType(This,ppType)	\
    ( (This)->lpVtbl -> GetReturnType(This,ppType) ) 

#define IMethodInfo2_GetCorSignature(This,cbBuffer,pCorSignature,pcbSignature)	\
    ( (This)->lpVtbl -> GetCorSignature(This,cbBuffer,pCorSignature,pcbSignature) ) 

#define IMethodInfo2_GetLocalVarSigToken(This,pToken)	\
    ( (This)->lpVtbl -> GetLocalVarSigToken(This,pToken) ) 

#define IMethodInfo2_SetLocalVarSigToken(This,token)	\
    ( (This)->lpVtbl -> SetLocalVarSigToken(This,token) ) 

#define IMethodInfo2_GetAttributes(This,pCorMethodAttr)	\
    ( (This)->lpVtbl -> GetAttributes(This,pCorMethodAttr) ) 

#define IMethodInfo2_GetRejitCodeGenFlags(This,pRefitFlags)	\
    ( (This)->lpVtbl -> GetRejitCodeGenFlags(This,pRefitFlags) ) 

#define IMethodInfo2_GetCodeRva(This,pRva)	\
    ( (This)->lpVtbl -> GetCodeRva(This,pRva) ) 

#define IMethodInfo2_MethodImplFlags(This,pCorMethodImpl)	\
    ( (This)->lpVtbl -> MethodImplFlags(This,pCorMethodImpl) ) 

#define IMethodInfo2_SetRejitCodeGenFlags(This,dwFlags)	\
    ( (This)->lpVtbl -> SetRejitCodeGenFlags(This,dwFlags) ) 

#define IMethodInfo2_GetExceptionSection(This,ppExceptionSection)	\
    ( (This)->lpVtbl -> GetExceptionSection(This,ppExceptionSection) ) 

#define IMethodInfo2_GetInstructionFactory(This,ppInstructionFactory)	\
    ( (This)->lpVtbl -> GetInstructionFactory(This,ppInstructionFactory) ) 

#define IMethodInfo2_GetRejitCount(This,pdwRejitCount)	\
    ( (This)->lpVtbl -> GetRejitCount(This,pdwRejitCount) ) 

#define IMethodInfo2_GetMaxStack(This,pMaxStack)	\
    ( (This)->lpVtbl -> GetMaxStack(This,pMaxStack) ) 

#define IMethodInfo2_GetSingleRetDefaultInstrumentation(This,ppSingleRetDefaultInstrumentation)	\
    ( (This)->lpVtbl -> GetSingleRetDefaultInstrumentation(This,ppSingleRetDefaultInstrumentation) ) 


#define IMethodInfo2_GetOriginalLocalVariables(This,ppLocalVariables)	\
    ( (This)->lpVtbl -> GetOriginalLocalVariables(This,ppLocalVariables) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IMethodInfo2_INTERFACE_DEFINED__ */


#ifndef __IAssemblyInfo_INTERFACE_DEFINED__
#define __IAssemblyInfo_INTERFACE_DEFINED__

/* interface IAssemblyInfo */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IAssemblyInfo;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("110FE5BA-57CD-4308-86BE-487478ABE2CD")
    IAssemblyInfo : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetAppDomainInfo( 
            /* [out] */ __RPC__deref_out_opt IAppDomainInfo **ppAppDomainInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleCount( 
            /* [out] */ __RPC__out ULONG *pcModuleInfos) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModules( 
            /* [in] */ ULONG cModuleInfos,
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **pModuleInfos) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleById( 
            /* [in] */ ModuleID moduleId,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleByMvid( 
            /* [in] */ __RPC__in GUID *pMvid,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModulesByName( 
            /* [in] */ __RPC__in BSTR pszModuleName,
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **ppModuleInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleForMethod( 
            /* [in] */ mdToken token,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleForType( 
            /* [in] */ __RPC__in BSTR pszTypeName,
            /* [in] */ mdToken tkResolutionScope,
            /* [out] */ __RPC__out mdToken *pTkTypeDef) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetManifestModule( 
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetPublicKey( 
            /* [in] */ ULONG cbBytes,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(cbBytes, cbBytes) BYTE *pbBytes) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetPublicKeySize( 
            /* [out] */ __RPC__out ULONG *pcbBytes) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetPublicKeyToken( 
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrPublicKeyToken) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetID( 
            /* [out] */ __RPC__out AssemblyID *pAssemblyId) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetName( 
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMetaDataToken( 
            /* [out] */ __RPC__out DWORD *pdwToken) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IAssemblyInfoVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IAssemblyInfo * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IAssemblyInfo * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IAssemblyInfo * This);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetAppDomainInfo)
        HRESULT ( STDMETHODCALLTYPE *GetAppDomainInfo )( 
            __RPC__in IAssemblyInfo * This,
            /* [out] */ __RPC__deref_out_opt IAppDomainInfo **ppAppDomainInfo);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetModuleCount)
        HRESULT ( STDMETHODCALLTYPE *GetModuleCount )( 
            __RPC__in IAssemblyInfo * This,
            /* [out] */ __RPC__out ULONG *pcModuleInfos);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetModules)
        HRESULT ( STDMETHODCALLTYPE *GetModules )( 
            __RPC__in IAssemblyInfo * This,
            /* [in] */ ULONG cModuleInfos,
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **pModuleInfos);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetModuleById)
        HRESULT ( STDMETHODCALLTYPE *GetModuleById )( 
            __RPC__in IAssemblyInfo * This,
            /* [in] */ ModuleID moduleId,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetModuleByMvid)
        HRESULT ( STDMETHODCALLTYPE *GetModuleByMvid )( 
            __RPC__in IAssemblyInfo * This,
            /* [in] */ __RPC__in GUID *pMvid,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetModulesByName)
        HRESULT ( STDMETHODCALLTYPE *GetModulesByName )( 
            __RPC__in IAssemblyInfo * This,
            /* [in] */ __RPC__in BSTR pszModuleName,
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **ppModuleInfo);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetModuleForMethod)
        HRESULT ( STDMETHODCALLTYPE *GetModuleForMethod )( 
            __RPC__in IAssemblyInfo * This,
            /* [in] */ mdToken token,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetModuleForType)
        HRESULT ( STDMETHODCALLTYPE *GetModuleForType )( 
            __RPC__in IAssemblyInfo * This,
            /* [in] */ __RPC__in BSTR pszTypeName,
            /* [in] */ mdToken tkResolutionScope,
            /* [out] */ __RPC__out mdToken *pTkTypeDef);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetManifestModule)
        HRESULT ( STDMETHODCALLTYPE *GetManifestModule )( 
            __RPC__in IAssemblyInfo * This,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetPublicKey)
        HRESULT ( STDMETHODCALLTYPE *GetPublicKey )( 
            __RPC__in IAssemblyInfo * This,
            /* [in] */ ULONG cbBytes,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(cbBytes, cbBytes) BYTE *pbBytes);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetPublicKeySize)
        HRESULT ( STDMETHODCALLTYPE *GetPublicKeySize )( 
            __RPC__in IAssemblyInfo * This,
            /* [out] */ __RPC__out ULONG *pcbBytes);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetPublicKeyToken)
        HRESULT ( STDMETHODCALLTYPE *GetPublicKeyToken )( 
            __RPC__in IAssemblyInfo * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrPublicKeyToken);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetID)
        HRESULT ( STDMETHODCALLTYPE *GetID )( 
            __RPC__in IAssemblyInfo * This,
            /* [out] */ __RPC__out AssemblyID *pAssemblyId);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetName)
        HRESULT ( STDMETHODCALLTYPE *GetName )( 
            __RPC__in IAssemblyInfo * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName);
        
        DECLSPEC_XFGVIRT(IAssemblyInfo, GetMetaDataToken)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataToken )( 
            __RPC__in IAssemblyInfo * This,
            /* [out] */ __RPC__out DWORD *pdwToken);
        
        END_INTERFACE
    } IAssemblyInfoVtbl;

    interface IAssemblyInfo
    {
        CONST_VTBL struct IAssemblyInfoVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IAssemblyInfo_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IAssemblyInfo_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IAssemblyInfo_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IAssemblyInfo_GetAppDomainInfo(This,ppAppDomainInfo)	\
    ( (This)->lpVtbl -> GetAppDomainInfo(This,ppAppDomainInfo) ) 

#define IAssemblyInfo_GetModuleCount(This,pcModuleInfos)	\
    ( (This)->lpVtbl -> GetModuleCount(This,pcModuleInfos) ) 

#define IAssemblyInfo_GetModules(This,cModuleInfos,pModuleInfos)	\
    ( (This)->lpVtbl -> GetModules(This,cModuleInfos,pModuleInfos) ) 

#define IAssemblyInfo_GetModuleById(This,moduleId,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetModuleById(This,moduleId,ppModuleInfo) ) 

#define IAssemblyInfo_GetModuleByMvid(This,pMvid,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetModuleByMvid(This,pMvid,ppModuleInfo) ) 

#define IAssemblyInfo_GetModulesByName(This,pszModuleName,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetModulesByName(This,pszModuleName,ppModuleInfo) ) 

#define IAssemblyInfo_GetModuleForMethod(This,token,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetModuleForMethod(This,token,ppModuleInfo) ) 

#define IAssemblyInfo_GetModuleForType(This,pszTypeName,tkResolutionScope,pTkTypeDef)	\
    ( (This)->lpVtbl -> GetModuleForType(This,pszTypeName,tkResolutionScope,pTkTypeDef) ) 

#define IAssemblyInfo_GetManifestModule(This,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetManifestModule(This,ppModuleInfo) ) 

#define IAssemblyInfo_GetPublicKey(This,cbBytes,pbBytes)	\
    ( (This)->lpVtbl -> GetPublicKey(This,cbBytes,pbBytes) ) 

#define IAssemblyInfo_GetPublicKeySize(This,pcbBytes)	\
    ( (This)->lpVtbl -> GetPublicKeySize(This,pcbBytes) ) 

#define IAssemblyInfo_GetPublicKeyToken(This,pbstrPublicKeyToken)	\
    ( (This)->lpVtbl -> GetPublicKeyToken(This,pbstrPublicKeyToken) ) 

#define IAssemblyInfo_GetID(This,pAssemblyId)	\
    ( (This)->lpVtbl -> GetID(This,pAssemblyId) ) 

#define IAssemblyInfo_GetName(This,pbstrName)	\
    ( (This)->lpVtbl -> GetName(This,pbstrName) ) 

#define IAssemblyInfo_GetMetaDataToken(This,pdwToken)	\
    ( (This)->lpVtbl -> GetMetaDataToken(This,pdwToken) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IAssemblyInfo_INTERFACE_DEFINED__ */


#ifndef __IEnumAssemblyInfo_INTERFACE_DEFINED__
#define __IEnumAssemblyInfo_INTERFACE_DEFINED__

/* interface IEnumAssemblyInfo */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IEnumAssemblyInfo;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("71001B79-B50A-4103-9D19-FFCF9F6CE1E9")
    IEnumAssemblyInfo : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Next( 
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IAssemblyInfo **rgelt,
            /* [out] */ __RPC__out ULONG *pceltFetched) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Skip( 
            /* [in] */ ULONG celt) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Reset( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Clone( 
            /* [out] */ __RPC__deref_out_opt IEnumAssemblyInfo **ppenum) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCount( 
            /* [out] */ __RPC__out DWORD *pLength) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IEnumAssemblyInfoVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IEnumAssemblyInfo * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IEnumAssemblyInfo * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IEnumAssemblyInfo * This);
        
        DECLSPEC_XFGVIRT(IEnumAssemblyInfo, Next)
        HRESULT ( STDMETHODCALLTYPE *Next )( 
            __RPC__in IEnumAssemblyInfo * This,
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IAssemblyInfo **rgelt,
            /* [out] */ __RPC__out ULONG *pceltFetched);
        
        DECLSPEC_XFGVIRT(IEnumAssemblyInfo, Skip)
        HRESULT ( STDMETHODCALLTYPE *Skip )( 
            __RPC__in IEnumAssemblyInfo * This,
            /* [in] */ ULONG celt);
        
        DECLSPEC_XFGVIRT(IEnumAssemblyInfo, Reset)
        HRESULT ( STDMETHODCALLTYPE *Reset )( 
            __RPC__in IEnumAssemblyInfo * This);
        
        DECLSPEC_XFGVIRT(IEnumAssemblyInfo, Clone)
        HRESULT ( STDMETHODCALLTYPE *Clone )( 
            __RPC__in IEnumAssemblyInfo * This,
            /* [out] */ __RPC__deref_out_opt IEnumAssemblyInfo **ppenum);
        
        DECLSPEC_XFGVIRT(IEnumAssemblyInfo, GetCount)
        HRESULT ( STDMETHODCALLTYPE *GetCount )( 
            __RPC__in IEnumAssemblyInfo * This,
            /* [out] */ __RPC__out DWORD *pLength);
        
        END_INTERFACE
    } IEnumAssemblyInfoVtbl;

    interface IEnumAssemblyInfo
    {
        CONST_VTBL struct IEnumAssemblyInfoVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IEnumAssemblyInfo_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IEnumAssemblyInfo_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IEnumAssemblyInfo_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IEnumAssemblyInfo_Next(This,celt,rgelt,pceltFetched)	\
    ( (This)->lpVtbl -> Next(This,celt,rgelt,pceltFetched) ) 

#define IEnumAssemblyInfo_Skip(This,celt)	\
    ( (This)->lpVtbl -> Skip(This,celt) ) 

#define IEnumAssemblyInfo_Reset(This)	\
    ( (This)->lpVtbl -> Reset(This) ) 

#define IEnumAssemblyInfo_Clone(This,ppenum)	\
    ( (This)->lpVtbl -> Clone(This,ppenum) ) 

#define IEnumAssemblyInfo_GetCount(This,pLength)	\
    ( (This)->lpVtbl -> GetCount(This,pLength) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IEnumAssemblyInfo_INTERFACE_DEFINED__ */


#ifndef __IModuleInfo_INTERFACE_DEFINED__
#define __IModuleInfo_INTERFACE_DEFINED__

/* interface IModuleInfo */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IModuleInfo;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("0BD963B1-FD87-4492-A417-152F3D0C9CBC")
    IModuleInfo : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetModuleName( 
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrModuleName) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetFullPath( 
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrFullPath) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetAssemblyInfo( 
            /* [out] */ __RPC__deref_out_opt IAssemblyInfo **ppAssemblyInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetAppDomainInfo( 
            /* [out] */ __RPC__deref_out_opt IAppDomainInfo **ppAppDomainInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMetaDataImport( 
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataImport) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMetaDataAssemblyImport( 
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataAssemblyImport) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMetaDataEmit( 
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataEmit) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMetaDataAssemblyEmit( 
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataAssemblyEmit) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleID( 
            /* [out] */ __RPC__out ModuleID *pModuleId) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMVID( 
            /* [out] */ __RPC__out GUID *pguidMvid) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsILOnly( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsMscorlib( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsDynamic( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsNgen( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsWinRT( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIs64bit( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetImageBase( 
            /* [out] */ __RPC__deref_out_opt LPCBYTE *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCorHeader( 
            /* [in] */ DWORD cbValue,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(cbValue, cbValue) BYTE *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetEntrypointToken( 
            /* [out] */ __RPC__out DWORD *pdwEntrypointToken) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleVersion( 
            /* [in] */ DWORD cbValue,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(cbValue, cbValue) BYTE *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE RequestRejit( 
            /* [in] */ mdToken methodToken) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateTypeFactory( 
            /* [out] */ __RPC__deref_out_opt ITypeCreator **ppTypeFactory) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMethodInfoById( 
            /* [in] */ FunctionID functionID,
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMethodInfoByToken( 
            /* [in] */ mdToken token,
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ImportModule( 
            /* [in] */ __RPC__in_opt IUnknown *pSourceModuleMetadataImport,
            /* [in] */ __RPC__deref_in_opt LPCBYTE *pSourceImage) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IModuleInfoVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IModuleInfo * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IModuleInfo * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IModuleInfo * This);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetModuleName)
        HRESULT ( STDMETHODCALLTYPE *GetModuleName )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrModuleName);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetFullPath)
        HRESULT ( STDMETHODCALLTYPE *GetFullPath )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrFullPath);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetAssemblyInfo)
        HRESULT ( STDMETHODCALLTYPE *GetAssemblyInfo )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__deref_out_opt IAssemblyInfo **ppAssemblyInfo);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetAppDomainInfo)
        HRESULT ( STDMETHODCALLTYPE *GetAppDomainInfo )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__deref_out_opt IAppDomainInfo **ppAppDomainInfo);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMetaDataImport)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataImport )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataImport);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMetaDataAssemblyImport)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataAssemblyImport )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataAssemblyImport);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMetaDataEmit)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataEmit )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataEmit);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMetaDataAssemblyEmit)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataAssemblyEmit )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataAssemblyEmit);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetModuleID)
        HRESULT ( STDMETHODCALLTYPE *GetModuleID )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__out ModuleID *pModuleId);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMVID)
        HRESULT ( STDMETHODCALLTYPE *GetMVID )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__out GUID *pguidMvid);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsILOnly)
        HRESULT ( STDMETHODCALLTYPE *GetIsILOnly )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsMscorlib)
        HRESULT ( STDMETHODCALLTYPE *GetIsMscorlib )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsDynamic)
        HRESULT ( STDMETHODCALLTYPE *GetIsDynamic )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsNgen)
        HRESULT ( STDMETHODCALLTYPE *GetIsNgen )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsWinRT)
        HRESULT ( STDMETHODCALLTYPE *GetIsWinRT )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIs64bit)
        HRESULT ( STDMETHODCALLTYPE *GetIs64bit )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetImageBase)
        HRESULT ( STDMETHODCALLTYPE *GetImageBase )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__deref_out_opt LPCBYTE *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetCorHeader)
        HRESULT ( STDMETHODCALLTYPE *GetCorHeader )( 
            __RPC__in IModuleInfo * This,
            /* [in] */ DWORD cbValue,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(cbValue, cbValue) BYTE *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetEntrypointToken)
        HRESULT ( STDMETHODCALLTYPE *GetEntrypointToken )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__out DWORD *pdwEntrypointToken);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetModuleVersion)
        HRESULT ( STDMETHODCALLTYPE *GetModuleVersion )( 
            __RPC__in IModuleInfo * This,
            /* [in] */ DWORD cbValue,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(cbValue, cbValue) BYTE *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, RequestRejit)
        HRESULT ( STDMETHODCALLTYPE *RequestRejit )( 
            __RPC__in IModuleInfo * This,
            /* [in] */ mdToken methodToken);
        
        DECLSPEC_XFGVIRT(IModuleInfo, CreateTypeFactory)
        HRESULT ( STDMETHODCALLTYPE *CreateTypeFactory )( 
            __RPC__in IModuleInfo * This,
            /* [out] */ __RPC__deref_out_opt ITypeCreator **ppTypeFactory);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMethodInfoById)
        HRESULT ( STDMETHODCALLTYPE *GetMethodInfoById )( 
            __RPC__in IModuleInfo * This,
            /* [in] */ FunctionID functionID,
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMethodInfoByToken)
        HRESULT ( STDMETHODCALLTYPE *GetMethodInfoByToken )( 
            __RPC__in IModuleInfo * This,
            /* [in] */ mdToken token,
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo);
        
        DECLSPEC_XFGVIRT(IModuleInfo, ImportModule)
        HRESULT ( STDMETHODCALLTYPE *ImportModule )( 
            __RPC__in IModuleInfo * This,
            /* [in] */ __RPC__in_opt IUnknown *pSourceModuleMetadataImport,
            /* [in] */ __RPC__deref_in_opt LPCBYTE *pSourceImage);
        
        END_INTERFACE
    } IModuleInfoVtbl;

    interface IModuleInfo
    {
        CONST_VTBL struct IModuleInfoVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IModuleInfo_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IModuleInfo_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IModuleInfo_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IModuleInfo_GetModuleName(This,pbstrModuleName)	\
    ( (This)->lpVtbl -> GetModuleName(This,pbstrModuleName) ) 

#define IModuleInfo_GetFullPath(This,pbstrFullPath)	\
    ( (This)->lpVtbl -> GetFullPath(This,pbstrFullPath) ) 

#define IModuleInfo_GetAssemblyInfo(This,ppAssemblyInfo)	\
    ( (This)->lpVtbl -> GetAssemblyInfo(This,ppAssemblyInfo) ) 

#define IModuleInfo_GetAppDomainInfo(This,ppAppDomainInfo)	\
    ( (This)->lpVtbl -> GetAppDomainInfo(This,ppAppDomainInfo) ) 

#define IModuleInfo_GetMetaDataImport(This,ppMetaDataImport)	\
    ( (This)->lpVtbl -> GetMetaDataImport(This,ppMetaDataImport) ) 

#define IModuleInfo_GetMetaDataAssemblyImport(This,ppMetaDataAssemblyImport)	\
    ( (This)->lpVtbl -> GetMetaDataAssemblyImport(This,ppMetaDataAssemblyImport) ) 

#define IModuleInfo_GetMetaDataEmit(This,ppMetaDataEmit)	\
    ( (This)->lpVtbl -> GetMetaDataEmit(This,ppMetaDataEmit) ) 

#define IModuleInfo_GetMetaDataAssemblyEmit(This,ppMetaDataAssemblyEmit)	\
    ( (This)->lpVtbl -> GetMetaDataAssemblyEmit(This,ppMetaDataAssemblyEmit) ) 

#define IModuleInfo_GetModuleID(This,pModuleId)	\
    ( (This)->lpVtbl -> GetModuleID(This,pModuleId) ) 

#define IModuleInfo_GetMVID(This,pguidMvid)	\
    ( (This)->lpVtbl -> GetMVID(This,pguidMvid) ) 

#define IModuleInfo_GetIsILOnly(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsILOnly(This,pbValue) ) 

#define IModuleInfo_GetIsMscorlib(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsMscorlib(This,pbValue) ) 

#define IModuleInfo_GetIsDynamic(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsDynamic(This,pbValue) ) 

#define IModuleInfo_GetIsNgen(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsNgen(This,pbValue) ) 

#define IModuleInfo_GetIsWinRT(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsWinRT(This,pbValue) ) 

#define IModuleInfo_GetIs64bit(This,pbValue)	\
    ( (This)->lpVtbl -> GetIs64bit(This,pbValue) ) 

#define IModuleInfo_GetImageBase(This,pbValue)	\
    ( (This)->lpVtbl -> GetImageBase(This,pbValue) ) 

#define IModuleInfo_GetCorHeader(This,cbValue,pbValue)	\
    ( (This)->lpVtbl -> GetCorHeader(This,cbValue,pbValue) ) 

#define IModuleInfo_GetEntrypointToken(This,pdwEntrypointToken)	\
    ( (This)->lpVtbl -> GetEntrypointToken(This,pdwEntrypointToken) ) 

#define IModuleInfo_GetModuleVersion(This,cbValue,pbValue)	\
    ( (This)->lpVtbl -> GetModuleVersion(This,cbValue,pbValue) ) 

#define IModuleInfo_RequestRejit(This,methodToken)	\
    ( (This)->lpVtbl -> RequestRejit(This,methodToken) ) 

#define IModuleInfo_CreateTypeFactory(This,ppTypeFactory)	\
    ( (This)->lpVtbl -> CreateTypeFactory(This,ppTypeFactory) ) 

#define IModuleInfo_GetMethodInfoById(This,functionID,ppMethodInfo)	\
    ( (This)->lpVtbl -> GetMethodInfoById(This,functionID,ppMethodInfo) ) 

#define IModuleInfo_GetMethodInfoByToken(This,token,ppMethodInfo)	\
    ( (This)->lpVtbl -> GetMethodInfoByToken(This,token,ppMethodInfo) ) 

#define IModuleInfo_ImportModule(This,pSourceModuleMetadataImport,pSourceImage)	\
    ( (This)->lpVtbl -> ImportModule(This,pSourceModuleMetadataImport,pSourceImage) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IModuleInfo_INTERFACE_DEFINED__ */


#ifndef __IModuleInfo2_INTERFACE_DEFINED__
#define __IModuleInfo2_INTERFACE_DEFINED__

/* interface IModuleInfo2 */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IModuleInfo2;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("4200c448-7ede-4e61-ae67-b017d3021f12")
    IModuleInfo2 : public IModuleInfo
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetIsFlatLayout( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ResolveRva( 
            /* [in] */ DWORD rva,
            /* [out] */ __RPC__deref_out_opt LPCBYTE *ppbResolvedAddress) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IModuleInfo2Vtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IModuleInfo2 * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IModuleInfo2 * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IModuleInfo2 * This);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetModuleName)
        HRESULT ( STDMETHODCALLTYPE *GetModuleName )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrModuleName);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetFullPath)
        HRESULT ( STDMETHODCALLTYPE *GetFullPath )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrFullPath);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetAssemblyInfo)
        HRESULT ( STDMETHODCALLTYPE *GetAssemblyInfo )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IAssemblyInfo **ppAssemblyInfo);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetAppDomainInfo)
        HRESULT ( STDMETHODCALLTYPE *GetAppDomainInfo )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IAppDomainInfo **ppAppDomainInfo);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMetaDataImport)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataImport )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataImport);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMetaDataAssemblyImport)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataAssemblyImport )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataAssemblyImport);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMetaDataEmit)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataEmit )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataEmit);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMetaDataAssemblyEmit)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataAssemblyEmit )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataAssemblyEmit);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetModuleID)
        HRESULT ( STDMETHODCALLTYPE *GetModuleID )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__out ModuleID *pModuleId);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMVID)
        HRESULT ( STDMETHODCALLTYPE *GetMVID )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__out GUID *pguidMvid);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsILOnly)
        HRESULT ( STDMETHODCALLTYPE *GetIsILOnly )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsMscorlib)
        HRESULT ( STDMETHODCALLTYPE *GetIsMscorlib )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsDynamic)
        HRESULT ( STDMETHODCALLTYPE *GetIsDynamic )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsNgen)
        HRESULT ( STDMETHODCALLTYPE *GetIsNgen )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsWinRT)
        HRESULT ( STDMETHODCALLTYPE *GetIsWinRT )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIs64bit)
        HRESULT ( STDMETHODCALLTYPE *GetIs64bit )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetImageBase)
        HRESULT ( STDMETHODCALLTYPE *GetImageBase )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__deref_out_opt LPCBYTE *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetCorHeader)
        HRESULT ( STDMETHODCALLTYPE *GetCorHeader )( 
            __RPC__in IModuleInfo2 * This,
            /* [in] */ DWORD cbValue,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(cbValue, cbValue) BYTE *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetEntrypointToken)
        HRESULT ( STDMETHODCALLTYPE *GetEntrypointToken )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__out DWORD *pdwEntrypointToken);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetModuleVersion)
        HRESULT ( STDMETHODCALLTYPE *GetModuleVersion )( 
            __RPC__in IModuleInfo2 * This,
            /* [in] */ DWORD cbValue,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(cbValue, cbValue) BYTE *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, RequestRejit)
        HRESULT ( STDMETHODCALLTYPE *RequestRejit )( 
            __RPC__in IModuleInfo2 * This,
            /* [in] */ mdToken methodToken);
        
        DECLSPEC_XFGVIRT(IModuleInfo, CreateTypeFactory)
        HRESULT ( STDMETHODCALLTYPE *CreateTypeFactory )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__deref_out_opt ITypeCreator **ppTypeFactory);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMethodInfoById)
        HRESULT ( STDMETHODCALLTYPE *GetMethodInfoById )( 
            __RPC__in IModuleInfo2 * This,
            /* [in] */ FunctionID functionID,
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMethodInfoByToken)
        HRESULT ( STDMETHODCALLTYPE *GetMethodInfoByToken )( 
            __RPC__in IModuleInfo2 * This,
            /* [in] */ mdToken token,
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo);
        
        DECLSPEC_XFGVIRT(IModuleInfo, ImportModule)
        HRESULT ( STDMETHODCALLTYPE *ImportModule )( 
            __RPC__in IModuleInfo2 * This,
            /* [in] */ __RPC__in_opt IUnknown *pSourceModuleMetadataImport,
            /* [in] */ __RPC__deref_in_opt LPCBYTE *pSourceImage);
        
        DECLSPEC_XFGVIRT(IModuleInfo2, GetIsFlatLayout)
        HRESULT ( STDMETHODCALLTYPE *GetIsFlatLayout )( 
            __RPC__in IModuleInfo2 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo2, ResolveRva)
        HRESULT ( STDMETHODCALLTYPE *ResolveRva )( 
            __RPC__in IModuleInfo2 * This,
            /* [in] */ DWORD rva,
            /* [out] */ __RPC__deref_out_opt LPCBYTE *ppbResolvedAddress);
        
        END_INTERFACE
    } IModuleInfo2Vtbl;

    interface IModuleInfo2
    {
        CONST_VTBL struct IModuleInfo2Vtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IModuleInfo2_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IModuleInfo2_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IModuleInfo2_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IModuleInfo2_GetModuleName(This,pbstrModuleName)	\
    ( (This)->lpVtbl -> GetModuleName(This,pbstrModuleName) ) 

#define IModuleInfo2_GetFullPath(This,pbstrFullPath)	\
    ( (This)->lpVtbl -> GetFullPath(This,pbstrFullPath) ) 

#define IModuleInfo2_GetAssemblyInfo(This,ppAssemblyInfo)	\
    ( (This)->lpVtbl -> GetAssemblyInfo(This,ppAssemblyInfo) ) 

#define IModuleInfo2_GetAppDomainInfo(This,ppAppDomainInfo)	\
    ( (This)->lpVtbl -> GetAppDomainInfo(This,ppAppDomainInfo) ) 

#define IModuleInfo2_GetMetaDataImport(This,ppMetaDataImport)	\
    ( (This)->lpVtbl -> GetMetaDataImport(This,ppMetaDataImport) ) 

#define IModuleInfo2_GetMetaDataAssemblyImport(This,ppMetaDataAssemblyImport)	\
    ( (This)->lpVtbl -> GetMetaDataAssemblyImport(This,ppMetaDataAssemblyImport) ) 

#define IModuleInfo2_GetMetaDataEmit(This,ppMetaDataEmit)	\
    ( (This)->lpVtbl -> GetMetaDataEmit(This,ppMetaDataEmit) ) 

#define IModuleInfo2_GetMetaDataAssemblyEmit(This,ppMetaDataAssemblyEmit)	\
    ( (This)->lpVtbl -> GetMetaDataAssemblyEmit(This,ppMetaDataAssemblyEmit) ) 

#define IModuleInfo2_GetModuleID(This,pModuleId)	\
    ( (This)->lpVtbl -> GetModuleID(This,pModuleId) ) 

#define IModuleInfo2_GetMVID(This,pguidMvid)	\
    ( (This)->lpVtbl -> GetMVID(This,pguidMvid) ) 

#define IModuleInfo2_GetIsILOnly(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsILOnly(This,pbValue) ) 

#define IModuleInfo2_GetIsMscorlib(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsMscorlib(This,pbValue) ) 

#define IModuleInfo2_GetIsDynamic(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsDynamic(This,pbValue) ) 

#define IModuleInfo2_GetIsNgen(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsNgen(This,pbValue) ) 

#define IModuleInfo2_GetIsWinRT(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsWinRT(This,pbValue) ) 

#define IModuleInfo2_GetIs64bit(This,pbValue)	\
    ( (This)->lpVtbl -> GetIs64bit(This,pbValue) ) 

#define IModuleInfo2_GetImageBase(This,pbValue)	\
    ( (This)->lpVtbl -> GetImageBase(This,pbValue) ) 

#define IModuleInfo2_GetCorHeader(This,cbValue,pbValue)	\
    ( (This)->lpVtbl -> GetCorHeader(This,cbValue,pbValue) ) 

#define IModuleInfo2_GetEntrypointToken(This,pdwEntrypointToken)	\
    ( (This)->lpVtbl -> GetEntrypointToken(This,pdwEntrypointToken) ) 

#define IModuleInfo2_GetModuleVersion(This,cbValue,pbValue)	\
    ( (This)->lpVtbl -> GetModuleVersion(This,cbValue,pbValue) ) 

#define IModuleInfo2_RequestRejit(This,methodToken)	\
    ( (This)->lpVtbl -> RequestRejit(This,methodToken) ) 

#define IModuleInfo2_CreateTypeFactory(This,ppTypeFactory)	\
    ( (This)->lpVtbl -> CreateTypeFactory(This,ppTypeFactory) ) 

#define IModuleInfo2_GetMethodInfoById(This,functionID,ppMethodInfo)	\
    ( (This)->lpVtbl -> GetMethodInfoById(This,functionID,ppMethodInfo) ) 

#define IModuleInfo2_GetMethodInfoByToken(This,token,ppMethodInfo)	\
    ( (This)->lpVtbl -> GetMethodInfoByToken(This,token,ppMethodInfo) ) 

#define IModuleInfo2_ImportModule(This,pSourceModuleMetadataImport,pSourceImage)	\
    ( (This)->lpVtbl -> ImportModule(This,pSourceModuleMetadataImport,pSourceImage) ) 


#define IModuleInfo2_GetIsFlatLayout(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsFlatLayout(This,pbValue) ) 

#define IModuleInfo2_ResolveRva(This,rva,ppbResolvedAddress)	\
    ( (This)->lpVtbl -> ResolveRva(This,rva,ppbResolvedAddress) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IModuleInfo2_INTERFACE_DEFINED__ */


#ifndef __IModuleInfo3_INTERFACE_DEFINED__
#define __IModuleInfo3_INTERFACE_DEFINED__

/* interface IModuleInfo3 */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IModuleInfo3;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("B4C10B86-E3D3-4514-91B9-B2BAA84E7D8B")
    IModuleInfo3 : public IModuleInfo2
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetIsLoadedFromDisk( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IModuleInfo3Vtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IModuleInfo3 * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IModuleInfo3 * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IModuleInfo3 * This);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetModuleName)
        HRESULT ( STDMETHODCALLTYPE *GetModuleName )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrModuleName);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetFullPath)
        HRESULT ( STDMETHODCALLTYPE *GetFullPath )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrFullPath);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetAssemblyInfo)
        HRESULT ( STDMETHODCALLTYPE *GetAssemblyInfo )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__deref_out_opt IAssemblyInfo **ppAssemblyInfo);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetAppDomainInfo)
        HRESULT ( STDMETHODCALLTYPE *GetAppDomainInfo )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__deref_out_opt IAppDomainInfo **ppAppDomainInfo);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMetaDataImport)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataImport )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataImport);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMetaDataAssemblyImport)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataAssemblyImport )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataAssemblyImport);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMetaDataEmit)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataEmit )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataEmit);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMetaDataAssemblyEmit)
        HRESULT ( STDMETHODCALLTYPE *GetMetaDataAssemblyEmit )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__deref_out_opt IUnknown **ppMetaDataAssemblyEmit);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetModuleID)
        HRESULT ( STDMETHODCALLTYPE *GetModuleID )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__out ModuleID *pModuleId);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMVID)
        HRESULT ( STDMETHODCALLTYPE *GetMVID )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__out GUID *pguidMvid);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsILOnly)
        HRESULT ( STDMETHODCALLTYPE *GetIsILOnly )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsMscorlib)
        HRESULT ( STDMETHODCALLTYPE *GetIsMscorlib )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsDynamic)
        HRESULT ( STDMETHODCALLTYPE *GetIsDynamic )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsNgen)
        HRESULT ( STDMETHODCALLTYPE *GetIsNgen )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIsWinRT)
        HRESULT ( STDMETHODCALLTYPE *GetIsWinRT )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetIs64bit)
        HRESULT ( STDMETHODCALLTYPE *GetIs64bit )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetImageBase)
        HRESULT ( STDMETHODCALLTYPE *GetImageBase )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__deref_out_opt LPCBYTE *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetCorHeader)
        HRESULT ( STDMETHODCALLTYPE *GetCorHeader )( 
            __RPC__in IModuleInfo3 * This,
            /* [in] */ DWORD cbValue,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(cbValue, cbValue) BYTE *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetEntrypointToken)
        HRESULT ( STDMETHODCALLTYPE *GetEntrypointToken )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__out DWORD *pdwEntrypointToken);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetModuleVersion)
        HRESULT ( STDMETHODCALLTYPE *GetModuleVersion )( 
            __RPC__in IModuleInfo3 * This,
            /* [in] */ DWORD cbValue,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(cbValue, cbValue) BYTE *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo, RequestRejit)
        HRESULT ( STDMETHODCALLTYPE *RequestRejit )( 
            __RPC__in IModuleInfo3 * This,
            /* [in] */ mdToken methodToken);
        
        DECLSPEC_XFGVIRT(IModuleInfo, CreateTypeFactory)
        HRESULT ( STDMETHODCALLTYPE *CreateTypeFactory )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__deref_out_opt ITypeCreator **ppTypeFactory);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMethodInfoById)
        HRESULT ( STDMETHODCALLTYPE *GetMethodInfoById )( 
            __RPC__in IModuleInfo3 * This,
            /* [in] */ FunctionID functionID,
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo);
        
        DECLSPEC_XFGVIRT(IModuleInfo, GetMethodInfoByToken)
        HRESULT ( STDMETHODCALLTYPE *GetMethodInfoByToken )( 
            __RPC__in IModuleInfo3 * This,
            /* [in] */ mdToken token,
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo);
        
        DECLSPEC_XFGVIRT(IModuleInfo, ImportModule)
        HRESULT ( STDMETHODCALLTYPE *ImportModule )( 
            __RPC__in IModuleInfo3 * This,
            /* [in] */ __RPC__in_opt IUnknown *pSourceModuleMetadataImport,
            /* [in] */ __RPC__deref_in_opt LPCBYTE *pSourceImage);
        
        DECLSPEC_XFGVIRT(IModuleInfo2, GetIsFlatLayout)
        HRESULT ( STDMETHODCALLTYPE *GetIsFlatLayout )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IModuleInfo2, ResolveRva)
        HRESULT ( STDMETHODCALLTYPE *ResolveRva )( 
            __RPC__in IModuleInfo3 * This,
            /* [in] */ DWORD rva,
            /* [out] */ __RPC__deref_out_opt LPCBYTE *ppbResolvedAddress);
        
        DECLSPEC_XFGVIRT(IModuleInfo3, GetIsLoadedFromDisk)
        HRESULT ( STDMETHODCALLTYPE *GetIsLoadedFromDisk )( 
            __RPC__in IModuleInfo3 * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        END_INTERFACE
    } IModuleInfo3Vtbl;

    interface IModuleInfo3
    {
        CONST_VTBL struct IModuleInfo3Vtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IModuleInfo3_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IModuleInfo3_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IModuleInfo3_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IModuleInfo3_GetModuleName(This,pbstrModuleName)	\
    ( (This)->lpVtbl -> GetModuleName(This,pbstrModuleName) ) 

#define IModuleInfo3_GetFullPath(This,pbstrFullPath)	\
    ( (This)->lpVtbl -> GetFullPath(This,pbstrFullPath) ) 

#define IModuleInfo3_GetAssemblyInfo(This,ppAssemblyInfo)	\
    ( (This)->lpVtbl -> GetAssemblyInfo(This,ppAssemblyInfo) ) 

#define IModuleInfo3_GetAppDomainInfo(This,ppAppDomainInfo)	\
    ( (This)->lpVtbl -> GetAppDomainInfo(This,ppAppDomainInfo) ) 

#define IModuleInfo3_GetMetaDataImport(This,ppMetaDataImport)	\
    ( (This)->lpVtbl -> GetMetaDataImport(This,ppMetaDataImport) ) 

#define IModuleInfo3_GetMetaDataAssemblyImport(This,ppMetaDataAssemblyImport)	\
    ( (This)->lpVtbl -> GetMetaDataAssemblyImport(This,ppMetaDataAssemblyImport) ) 

#define IModuleInfo3_GetMetaDataEmit(This,ppMetaDataEmit)	\
    ( (This)->lpVtbl -> GetMetaDataEmit(This,ppMetaDataEmit) ) 

#define IModuleInfo3_GetMetaDataAssemblyEmit(This,ppMetaDataAssemblyEmit)	\
    ( (This)->lpVtbl -> GetMetaDataAssemblyEmit(This,ppMetaDataAssemblyEmit) ) 

#define IModuleInfo3_GetModuleID(This,pModuleId)	\
    ( (This)->lpVtbl -> GetModuleID(This,pModuleId) ) 

#define IModuleInfo3_GetMVID(This,pguidMvid)	\
    ( (This)->lpVtbl -> GetMVID(This,pguidMvid) ) 

#define IModuleInfo3_GetIsILOnly(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsILOnly(This,pbValue) ) 

#define IModuleInfo3_GetIsMscorlib(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsMscorlib(This,pbValue) ) 

#define IModuleInfo3_GetIsDynamic(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsDynamic(This,pbValue) ) 

#define IModuleInfo3_GetIsNgen(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsNgen(This,pbValue) ) 

#define IModuleInfo3_GetIsWinRT(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsWinRT(This,pbValue) ) 

#define IModuleInfo3_GetIs64bit(This,pbValue)	\
    ( (This)->lpVtbl -> GetIs64bit(This,pbValue) ) 

#define IModuleInfo3_GetImageBase(This,pbValue)	\
    ( (This)->lpVtbl -> GetImageBase(This,pbValue) ) 

#define IModuleInfo3_GetCorHeader(This,cbValue,pbValue)	\
    ( (This)->lpVtbl -> GetCorHeader(This,cbValue,pbValue) ) 

#define IModuleInfo3_GetEntrypointToken(This,pdwEntrypointToken)	\
    ( (This)->lpVtbl -> GetEntrypointToken(This,pdwEntrypointToken) ) 

#define IModuleInfo3_GetModuleVersion(This,cbValue,pbValue)	\
    ( (This)->lpVtbl -> GetModuleVersion(This,cbValue,pbValue) ) 

#define IModuleInfo3_RequestRejit(This,methodToken)	\
    ( (This)->lpVtbl -> RequestRejit(This,methodToken) ) 

#define IModuleInfo3_CreateTypeFactory(This,ppTypeFactory)	\
    ( (This)->lpVtbl -> CreateTypeFactory(This,ppTypeFactory) ) 

#define IModuleInfo3_GetMethodInfoById(This,functionID,ppMethodInfo)	\
    ( (This)->lpVtbl -> GetMethodInfoById(This,functionID,ppMethodInfo) ) 

#define IModuleInfo3_GetMethodInfoByToken(This,token,ppMethodInfo)	\
    ( (This)->lpVtbl -> GetMethodInfoByToken(This,token,ppMethodInfo) ) 

#define IModuleInfo3_ImportModule(This,pSourceModuleMetadataImport,pSourceImage)	\
    ( (This)->lpVtbl -> ImportModule(This,pSourceModuleMetadataImport,pSourceImage) ) 


#define IModuleInfo3_GetIsFlatLayout(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsFlatLayout(This,pbValue) ) 

#define IModuleInfo3_ResolveRva(This,rva,ppbResolvedAddress)	\
    ( (This)->lpVtbl -> ResolveRva(This,rva,ppbResolvedAddress) ) 


#define IModuleInfo3_GetIsLoadedFromDisk(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsLoadedFromDisk(This,pbValue) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IModuleInfo3_INTERFACE_DEFINED__ */


#ifndef __IEnumModuleInfo_INTERFACE_DEFINED__
#define __IEnumModuleInfo_INTERFACE_DEFINED__

/* interface IEnumModuleInfo */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IEnumModuleInfo;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("683b3d0b-5cab-49ac-9242-c7de190c7764")
    IEnumModuleInfo : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Next( 
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IModuleInfo **rgelt,
            /* [in] */ __RPC__in ULONG *pceltFetched) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Skip( 
            /* [in] */ ULONG celt) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Reset( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Clone( 
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **ppenum) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCount( 
            /* [out] */ __RPC__out DWORD *pLength) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IEnumModuleInfoVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IEnumModuleInfo * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IEnumModuleInfo * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IEnumModuleInfo * This);
        
        DECLSPEC_XFGVIRT(IEnumModuleInfo, Next)
        HRESULT ( STDMETHODCALLTYPE *Next )( 
            __RPC__in IEnumModuleInfo * This,
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IModuleInfo **rgelt,
            /* [in] */ __RPC__in ULONG *pceltFetched);
        
        DECLSPEC_XFGVIRT(IEnumModuleInfo, Skip)
        HRESULT ( STDMETHODCALLTYPE *Skip )( 
            __RPC__in IEnumModuleInfo * This,
            /* [in] */ ULONG celt);
        
        DECLSPEC_XFGVIRT(IEnumModuleInfo, Reset)
        HRESULT ( STDMETHODCALLTYPE *Reset )( 
            __RPC__in IEnumModuleInfo * This);
        
        DECLSPEC_XFGVIRT(IEnumModuleInfo, Clone)
        HRESULT ( STDMETHODCALLTYPE *Clone )( 
            __RPC__in IEnumModuleInfo * This,
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **ppenum);
        
        DECLSPEC_XFGVIRT(IEnumModuleInfo, GetCount)
        HRESULT ( STDMETHODCALLTYPE *GetCount )( 
            __RPC__in IEnumModuleInfo * This,
            /* [out] */ __RPC__out DWORD *pLength);
        
        END_INTERFACE
    } IEnumModuleInfoVtbl;

    interface IEnumModuleInfo
    {
        CONST_VTBL struct IEnumModuleInfoVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IEnumModuleInfo_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IEnumModuleInfo_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IEnumModuleInfo_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IEnumModuleInfo_Next(This,celt,rgelt,pceltFetched)	\
    ( (This)->lpVtbl -> Next(This,celt,rgelt,pceltFetched) ) 

#define IEnumModuleInfo_Skip(This,celt)	\
    ( (This)->lpVtbl -> Skip(This,celt) ) 

#define IEnumModuleInfo_Reset(This)	\
    ( (This)->lpVtbl -> Reset(This) ) 

#define IEnumModuleInfo_Clone(This,ppenum)	\
    ( (This)->lpVtbl -> Clone(This,ppenum) ) 

#define IEnumModuleInfo_GetCount(This,pLength)	\
    ( (This)->lpVtbl -> GetCount(This,pLength) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IEnumModuleInfo_INTERFACE_DEFINED__ */


#ifndef __IAppDomainInfo_INTERFACE_DEFINED__
#define __IAppDomainInfo_INTERFACE_DEFINED__

/* interface IAppDomainInfo */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IAppDomainInfo;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("A81A5232-4693-47E9-A74D-BB4C71164659")
    IAppDomainInfo : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetAppDomainId( 
            /* [out] */ __RPC__out AppDomainID *pAppDomainId) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsSystemDomain( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsSharedDomain( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsDefaultDomain( 
            /* [out] */ __RPC__out BOOL *pbValue) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetName( 
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetAssemblies( 
            /* [out] */ __RPC__deref_out_opt IEnumAssemblyInfo **ppAssemblyInfos) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModules( 
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **ppModuleInfos) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetAssemblyInfoById( 
            /* [in] */ AssemblyID assemblyID,
            /* [out] */ __RPC__deref_out_opt IAssemblyInfo **ppAssemblyInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetAssemblyInfosByName( 
            /* [in] */ __RPC__in BSTR pszAssemblyName,
            /* [out] */ __RPC__deref_out_opt IEnumAssemblyInfo **ppAssemblyInfos) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleCount( 
            /* [out] */ __RPC__out ULONG *pcModuleInfos) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleInfoById( 
            /* [in] */ ModuleID moduleId,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleInfosByMvid( 
            /* [in] */ GUID mvid,
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **ppModuleInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleInfosByName( 
            /* [in] */ __RPC__in BSTR pszModuleName,
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **ppModuleInfo) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IAppDomainInfoVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IAppDomainInfo * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IAppDomainInfo * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IAppDomainInfo * This);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetAppDomainId)
        HRESULT ( STDMETHODCALLTYPE *GetAppDomainId )( 
            __RPC__in IAppDomainInfo * This,
            /* [out] */ __RPC__out AppDomainID *pAppDomainId);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetIsSystemDomain)
        HRESULT ( STDMETHODCALLTYPE *GetIsSystemDomain )( 
            __RPC__in IAppDomainInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetIsSharedDomain)
        HRESULT ( STDMETHODCALLTYPE *GetIsSharedDomain )( 
            __RPC__in IAppDomainInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetIsDefaultDomain)
        HRESULT ( STDMETHODCALLTYPE *GetIsDefaultDomain )( 
            __RPC__in IAppDomainInfo * This,
            /* [out] */ __RPC__out BOOL *pbValue);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetName)
        HRESULT ( STDMETHODCALLTYPE *GetName )( 
            __RPC__in IAppDomainInfo * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetAssemblies)
        HRESULT ( STDMETHODCALLTYPE *GetAssemblies )( 
            __RPC__in IAppDomainInfo * This,
            /* [out] */ __RPC__deref_out_opt IEnumAssemblyInfo **ppAssemblyInfos);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetModules)
        HRESULT ( STDMETHODCALLTYPE *GetModules )( 
            __RPC__in IAppDomainInfo * This,
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **ppModuleInfos);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetAssemblyInfoById)
        HRESULT ( STDMETHODCALLTYPE *GetAssemblyInfoById )( 
            __RPC__in IAppDomainInfo * This,
            /* [in] */ AssemblyID assemblyID,
            /* [out] */ __RPC__deref_out_opt IAssemblyInfo **ppAssemblyInfo);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetAssemblyInfosByName)
        HRESULT ( STDMETHODCALLTYPE *GetAssemblyInfosByName )( 
            __RPC__in IAppDomainInfo * This,
            /* [in] */ __RPC__in BSTR pszAssemblyName,
            /* [out] */ __RPC__deref_out_opt IEnumAssemblyInfo **ppAssemblyInfos);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetModuleCount)
        HRESULT ( STDMETHODCALLTYPE *GetModuleCount )( 
            __RPC__in IAppDomainInfo * This,
            /* [out] */ __RPC__out ULONG *pcModuleInfos);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetModuleInfoById)
        HRESULT ( STDMETHODCALLTYPE *GetModuleInfoById )( 
            __RPC__in IAppDomainInfo * This,
            /* [in] */ ModuleID moduleId,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetModuleInfosByMvid)
        HRESULT ( STDMETHODCALLTYPE *GetModuleInfosByMvid )( 
            __RPC__in IAppDomainInfo * This,
            /* [in] */ GUID mvid,
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **ppModuleInfo);
        
        DECLSPEC_XFGVIRT(IAppDomainInfo, GetModuleInfosByName)
        HRESULT ( STDMETHODCALLTYPE *GetModuleInfosByName )( 
            __RPC__in IAppDomainInfo * This,
            /* [in] */ __RPC__in BSTR pszModuleName,
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **ppModuleInfo);
        
        END_INTERFACE
    } IAppDomainInfoVtbl;

    interface IAppDomainInfo
    {
        CONST_VTBL struct IAppDomainInfoVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IAppDomainInfo_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IAppDomainInfo_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IAppDomainInfo_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IAppDomainInfo_GetAppDomainId(This,pAppDomainId)	\
    ( (This)->lpVtbl -> GetAppDomainId(This,pAppDomainId) ) 

#define IAppDomainInfo_GetIsSystemDomain(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsSystemDomain(This,pbValue) ) 

#define IAppDomainInfo_GetIsSharedDomain(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsSharedDomain(This,pbValue) ) 

#define IAppDomainInfo_GetIsDefaultDomain(This,pbValue)	\
    ( (This)->lpVtbl -> GetIsDefaultDomain(This,pbValue) ) 

#define IAppDomainInfo_GetName(This,pbstrName)	\
    ( (This)->lpVtbl -> GetName(This,pbstrName) ) 

#define IAppDomainInfo_GetAssemblies(This,ppAssemblyInfos)	\
    ( (This)->lpVtbl -> GetAssemblies(This,ppAssemblyInfos) ) 

#define IAppDomainInfo_GetModules(This,ppModuleInfos)	\
    ( (This)->lpVtbl -> GetModules(This,ppModuleInfos) ) 

#define IAppDomainInfo_GetAssemblyInfoById(This,assemblyID,ppAssemblyInfo)	\
    ( (This)->lpVtbl -> GetAssemblyInfoById(This,assemblyID,ppAssemblyInfo) ) 

#define IAppDomainInfo_GetAssemblyInfosByName(This,pszAssemblyName,ppAssemblyInfos)	\
    ( (This)->lpVtbl -> GetAssemblyInfosByName(This,pszAssemblyName,ppAssemblyInfos) ) 

#define IAppDomainInfo_GetModuleCount(This,pcModuleInfos)	\
    ( (This)->lpVtbl -> GetModuleCount(This,pcModuleInfos) ) 

#define IAppDomainInfo_GetModuleInfoById(This,moduleId,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetModuleInfoById(This,moduleId,ppModuleInfo) ) 

#define IAppDomainInfo_GetModuleInfosByMvid(This,mvid,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetModuleInfosByMvid(This,mvid,ppModuleInfo) ) 

#define IAppDomainInfo_GetModuleInfosByName(This,pszModuleName,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetModuleInfosByName(This,pszModuleName,ppModuleInfo) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IAppDomainInfo_INTERFACE_DEFINED__ */


#ifndef __IEnumAppDomainInfo_INTERFACE_DEFINED__
#define __IEnumAppDomainInfo_INTERFACE_DEFINED__

/* interface IEnumAppDomainInfo */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IEnumAppDomainInfo;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("C2A3E353-08BB-4A13-851E-07B1BB4AD57C")
    IEnumAppDomainInfo : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Next( 
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IAppDomainInfo **rgelt,
            /* [in] */ __RPC__in ULONG *pceltFetched) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Skip( 
            /* [in] */ ULONG celt) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Reset( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Clone( 
            /* [out] */ __RPC__deref_out_opt IEnumAppDomainInfo **ppenum) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCount( 
            /* [out] */ __RPC__out DWORD *pLength) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IEnumAppDomainInfoVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IEnumAppDomainInfo * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IEnumAppDomainInfo * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IEnumAppDomainInfo * This);
        
        DECLSPEC_XFGVIRT(IEnumAppDomainInfo, Next)
        HRESULT ( STDMETHODCALLTYPE *Next )( 
            __RPC__in IEnumAppDomainInfo * This,
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IAppDomainInfo **rgelt,
            /* [in] */ __RPC__in ULONG *pceltFetched);
        
        DECLSPEC_XFGVIRT(IEnumAppDomainInfo, Skip)
        HRESULT ( STDMETHODCALLTYPE *Skip )( 
            __RPC__in IEnumAppDomainInfo * This,
            /* [in] */ ULONG celt);
        
        DECLSPEC_XFGVIRT(IEnumAppDomainInfo, Reset)
        HRESULT ( STDMETHODCALLTYPE *Reset )( 
            __RPC__in IEnumAppDomainInfo * This);
        
        DECLSPEC_XFGVIRT(IEnumAppDomainInfo, Clone)
        HRESULT ( STDMETHODCALLTYPE *Clone )( 
            __RPC__in IEnumAppDomainInfo * This,
            /* [out] */ __RPC__deref_out_opt IEnumAppDomainInfo **ppenum);
        
        DECLSPEC_XFGVIRT(IEnumAppDomainInfo, GetCount)
        HRESULT ( STDMETHODCALLTYPE *GetCount )( 
            __RPC__in IEnumAppDomainInfo * This,
            /* [out] */ __RPC__out DWORD *pLength);
        
        END_INTERFACE
    } IEnumAppDomainInfoVtbl;

    interface IEnumAppDomainInfo
    {
        CONST_VTBL struct IEnumAppDomainInfoVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IEnumAppDomainInfo_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IEnumAppDomainInfo_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IEnumAppDomainInfo_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IEnumAppDomainInfo_Next(This,celt,rgelt,pceltFetched)	\
    ( (This)->lpVtbl -> Next(This,celt,rgelt,pceltFetched) ) 

#define IEnumAppDomainInfo_Skip(This,celt)	\
    ( (This)->lpVtbl -> Skip(This,celt) ) 

#define IEnumAppDomainInfo_Reset(This)	\
    ( (This)->lpVtbl -> Reset(This) ) 

#define IEnumAppDomainInfo_Clone(This,ppenum)	\
    ( (This)->lpVtbl -> Clone(This,ppenum) ) 

#define IEnumAppDomainInfo_GetCount(This,pLength)	\
    ( (This)->lpVtbl -> GetCount(This,pLength) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IEnumAppDomainInfo_INTERFACE_DEFINED__ */


#ifndef __ILocalVariableCollection_INTERFACE_DEFINED__
#define __ILocalVariableCollection_INTERFACE_DEFINED__

/* interface ILocalVariableCollection */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ILocalVariableCollection;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("353F806F-6563-40E0-8EBE-B93A58C0145F")
    ILocalVariableCollection : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Initialize( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCorSignature( 
            /* [out] */ __RPC__deref_out_opt ISignatureBuilder **ppSignature) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCount( 
            /* [out] */ __RPC__out DWORD *pdwCount) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddLocal( 
            /* [in] */ __RPC__in_opt IType *pType,
            /* [optional][full][out][in] */ __RPC__inout_opt DWORD *pIndex) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ReplaceSignature( 
            /* [in] */ __RPC__in const BYTE *pSignature,
            /* [in] */ DWORD dwSigSize) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CommitSignature( void) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ILocalVariableCollectionVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in ILocalVariableCollection * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in ILocalVariableCollection * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in ILocalVariableCollection * This);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection, Initialize)
        HRESULT ( STDMETHODCALLTYPE *Initialize )( 
            __RPC__in ILocalVariableCollection * This);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection, GetCorSignature)
        HRESULT ( STDMETHODCALLTYPE *GetCorSignature )( 
            __RPC__in ILocalVariableCollection * This,
            /* [out] */ __RPC__deref_out_opt ISignatureBuilder **ppSignature);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection, GetCount)
        HRESULT ( STDMETHODCALLTYPE *GetCount )( 
            __RPC__in ILocalVariableCollection * This,
            /* [out] */ __RPC__out DWORD *pdwCount);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection, AddLocal)
        HRESULT ( STDMETHODCALLTYPE *AddLocal )( 
            __RPC__in ILocalVariableCollection * This,
            /* [in] */ __RPC__in_opt IType *pType,
            /* [optional][full][out][in] */ __RPC__inout_opt DWORD *pIndex);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection, ReplaceSignature)
        HRESULT ( STDMETHODCALLTYPE *ReplaceSignature )( 
            __RPC__in ILocalVariableCollection * This,
            /* [in] */ __RPC__in const BYTE *pSignature,
            /* [in] */ DWORD dwSigSize);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection, CommitSignature)
        HRESULT ( STDMETHODCALLTYPE *CommitSignature )( 
            __RPC__in ILocalVariableCollection * This);
        
        END_INTERFACE
    } ILocalVariableCollectionVtbl;

    interface ILocalVariableCollection
    {
        CONST_VTBL struct ILocalVariableCollectionVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ILocalVariableCollection_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ILocalVariableCollection_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ILocalVariableCollection_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ILocalVariableCollection_Initialize(This)	\
    ( (This)->lpVtbl -> Initialize(This) ) 

#define ILocalVariableCollection_GetCorSignature(This,ppSignature)	\
    ( (This)->lpVtbl -> GetCorSignature(This,ppSignature) ) 

#define ILocalVariableCollection_GetCount(This,pdwCount)	\
    ( (This)->lpVtbl -> GetCount(This,pdwCount) ) 

#define ILocalVariableCollection_AddLocal(This,pType,pIndex)	\
    ( (This)->lpVtbl -> AddLocal(This,pType,pIndex) ) 

#define ILocalVariableCollection_ReplaceSignature(This,pSignature,dwSigSize)	\
    ( (This)->lpVtbl -> ReplaceSignature(This,pSignature,dwSigSize) ) 

#define ILocalVariableCollection_CommitSignature(This)	\
    ( (This)->lpVtbl -> CommitSignature(This) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ILocalVariableCollection_INTERFACE_DEFINED__ */


#ifndef __IType_INTERFACE_DEFINED__
#define __IType_INTERFACE_DEFINED__

/* interface IType */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IType;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("6FC96859-ED89-4D9F-A7C9-1DAD7EC35F67")
    IType : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE AddToSignature( 
            /* [in] */ __RPC__in_opt ISignatureBuilder *pSignatureBuilder) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCorElementType( 
            /* [out] */ __RPC__out CorElementType *pCorType) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE IsPrimitive( 
            /* [out] */ __RPC__out BOOL *pIsPrimitive) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE IsArray( 
            /* [out] */ __RPC__out BOOL *pIsArray) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE IsClass( 
            /* [out] */ __RPC__out BOOL *pIsClass) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetName( 
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ITypeVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IType * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IType * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IType * This);
        
        DECLSPEC_XFGVIRT(IType, AddToSignature)
        HRESULT ( STDMETHODCALLTYPE *AddToSignature )( 
            __RPC__in IType * This,
            /* [in] */ __RPC__in_opt ISignatureBuilder *pSignatureBuilder);
        
        DECLSPEC_XFGVIRT(IType, GetCorElementType)
        HRESULT ( STDMETHODCALLTYPE *GetCorElementType )( 
            __RPC__in IType * This,
            /* [out] */ __RPC__out CorElementType *pCorType);
        
        DECLSPEC_XFGVIRT(IType, IsPrimitive)
        HRESULT ( STDMETHODCALLTYPE *IsPrimitive )( 
            __RPC__in IType * This,
            /* [out] */ __RPC__out BOOL *pIsPrimitive);
        
        DECLSPEC_XFGVIRT(IType, IsArray)
        HRESULT ( STDMETHODCALLTYPE *IsArray )( 
            __RPC__in IType * This,
            /* [out] */ __RPC__out BOOL *pIsArray);
        
        DECLSPEC_XFGVIRT(IType, IsClass)
        HRESULT ( STDMETHODCALLTYPE *IsClass )( 
            __RPC__in IType * This,
            /* [out] */ __RPC__out BOOL *pIsClass);
        
        DECLSPEC_XFGVIRT(IType, GetName)
        HRESULT ( STDMETHODCALLTYPE *GetName )( 
            __RPC__in IType * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName);
        
        END_INTERFACE
    } ITypeVtbl;

    interface IType
    {
        CONST_VTBL struct ITypeVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IType_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IType_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IType_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IType_AddToSignature(This,pSignatureBuilder)	\
    ( (This)->lpVtbl -> AddToSignature(This,pSignatureBuilder) ) 

#define IType_GetCorElementType(This,pCorType)	\
    ( (This)->lpVtbl -> GetCorElementType(This,pCorType) ) 

#define IType_IsPrimitive(This,pIsPrimitive)	\
    ( (This)->lpVtbl -> IsPrimitive(This,pIsPrimitive) ) 

#define IType_IsArray(This,pIsArray)	\
    ( (This)->lpVtbl -> IsArray(This,pIsArray) ) 

#define IType_IsClass(This,pIsClass)	\
    ( (This)->lpVtbl -> IsClass(This,pIsClass) ) 

#define IType_GetName(This,pbstrName)	\
    ( (This)->lpVtbl -> GetName(This,pbstrName) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IType_INTERFACE_DEFINED__ */


#ifndef __IAppDomainCollection_INTERFACE_DEFINED__
#define __IAppDomainCollection_INTERFACE_DEFINED__

/* interface IAppDomainCollection */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IAppDomainCollection;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("C79F6730-C5FB-40C4-B528-0A0248CA4DEB")
    IAppDomainCollection : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetAppDomainCount( 
            /* [out] */ __RPC__out DWORD *pdwCount) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetAppDomainById( 
            /* [in] */ AppDomainID appDomainId,
            /* [out] */ __RPC__deref_out_opt IAppDomainInfo **ppAppDomainInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetAppDomainIDs( 
            /* [in] */ DWORD cAppDomains,
            /* [out] */ __RPC__out DWORD *pcActual,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(cAppDomains, cAppDomains) AppDomainID *AppDomainIDs) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetAppDomains( 
            /* [out] */ __RPC__deref_out_opt IEnumAppDomainInfo **ppEnumAppDomains) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetAssemblyInfoById( 
            /* [in] */ AssemblyID assemblyID,
            /* [out] */ __RPC__deref_out_opt IAssemblyInfo **ppAssemblyInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleInfoById( 
            /* [in] */ ModuleID moduleID,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleInfosByMvid( 
            /* [in] */ GUID mvid,
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **ppEnum) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMethodInfoById( 
            /* [in] */ FunctionID functionID,
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IAppDomainCollectionVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IAppDomainCollection * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IAppDomainCollection * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IAppDomainCollection * This);
        
        DECLSPEC_XFGVIRT(IAppDomainCollection, GetAppDomainCount)
        HRESULT ( STDMETHODCALLTYPE *GetAppDomainCount )( 
            __RPC__in IAppDomainCollection * This,
            /* [out] */ __RPC__out DWORD *pdwCount);
        
        DECLSPEC_XFGVIRT(IAppDomainCollection, GetAppDomainById)
        HRESULT ( STDMETHODCALLTYPE *GetAppDomainById )( 
            __RPC__in IAppDomainCollection * This,
            /* [in] */ AppDomainID appDomainId,
            /* [out] */ __RPC__deref_out_opt IAppDomainInfo **ppAppDomainInfo);
        
        DECLSPEC_XFGVIRT(IAppDomainCollection, GetAppDomainIDs)
        HRESULT ( STDMETHODCALLTYPE *GetAppDomainIDs )( 
            __RPC__in IAppDomainCollection * This,
            /* [in] */ DWORD cAppDomains,
            /* [out] */ __RPC__out DWORD *pcActual,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(cAppDomains, cAppDomains) AppDomainID *AppDomainIDs);
        
        DECLSPEC_XFGVIRT(IAppDomainCollection, GetAppDomains)
        HRESULT ( STDMETHODCALLTYPE *GetAppDomains )( 
            __RPC__in IAppDomainCollection * This,
            /* [out] */ __RPC__deref_out_opt IEnumAppDomainInfo **ppEnumAppDomains);
        
        DECLSPEC_XFGVIRT(IAppDomainCollection, GetAssemblyInfoById)
        HRESULT ( STDMETHODCALLTYPE *GetAssemblyInfoById )( 
            __RPC__in IAppDomainCollection * This,
            /* [in] */ AssemblyID assemblyID,
            /* [out] */ __RPC__deref_out_opt IAssemblyInfo **ppAssemblyInfo);
        
        DECLSPEC_XFGVIRT(IAppDomainCollection, GetModuleInfoById)
        HRESULT ( STDMETHODCALLTYPE *GetModuleInfoById )( 
            __RPC__in IAppDomainCollection * This,
            /* [in] */ ModuleID moduleID,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo);
        
        DECLSPEC_XFGVIRT(IAppDomainCollection, GetModuleInfosByMvid)
        HRESULT ( STDMETHODCALLTYPE *GetModuleInfosByMvid )( 
            __RPC__in IAppDomainCollection * This,
            /* [in] */ GUID mvid,
            /* [out] */ __RPC__deref_out_opt IEnumModuleInfo **ppEnum);
        
        DECLSPEC_XFGVIRT(IAppDomainCollection, GetMethodInfoById)
        HRESULT ( STDMETHODCALLTYPE *GetMethodInfoById )( 
            __RPC__in IAppDomainCollection * This,
            /* [in] */ FunctionID functionID,
            /* [out] */ __RPC__deref_out_opt IMethodInfo **ppMethodInfo);
        
        END_INTERFACE
    } IAppDomainCollectionVtbl;

    interface IAppDomainCollection
    {
        CONST_VTBL struct IAppDomainCollectionVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IAppDomainCollection_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IAppDomainCollection_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IAppDomainCollection_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IAppDomainCollection_GetAppDomainCount(This,pdwCount)	\
    ( (This)->lpVtbl -> GetAppDomainCount(This,pdwCount) ) 

#define IAppDomainCollection_GetAppDomainById(This,appDomainId,ppAppDomainInfo)	\
    ( (This)->lpVtbl -> GetAppDomainById(This,appDomainId,ppAppDomainInfo) ) 

#define IAppDomainCollection_GetAppDomainIDs(This,cAppDomains,pcActual,AppDomainIDs)	\
    ( (This)->lpVtbl -> GetAppDomainIDs(This,cAppDomains,pcActual,AppDomainIDs) ) 

#define IAppDomainCollection_GetAppDomains(This,ppEnumAppDomains)	\
    ( (This)->lpVtbl -> GetAppDomains(This,ppEnumAppDomains) ) 

#define IAppDomainCollection_GetAssemblyInfoById(This,assemblyID,ppAssemblyInfo)	\
    ( (This)->lpVtbl -> GetAssemblyInfoById(This,assemblyID,ppAssemblyInfo) ) 

#define IAppDomainCollection_GetModuleInfoById(This,moduleID,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetModuleInfoById(This,moduleID,ppModuleInfo) ) 

#define IAppDomainCollection_GetModuleInfosByMvid(This,mvid,ppEnum)	\
    ( (This)->lpVtbl -> GetModuleInfosByMvid(This,mvid,ppEnum) ) 

#define IAppDomainCollection_GetMethodInfoById(This,functionID,ppMethodInfo)	\
    ( (This)->lpVtbl -> GetMethodInfoById(This,functionID,ppMethodInfo) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IAppDomainCollection_INTERFACE_DEFINED__ */


#ifndef __ISignatureBuilder_INTERFACE_DEFINED__
#define __ISignatureBuilder_INTERFACE_DEFINED__

/* interface ISignatureBuilder */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ISignatureBuilder;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("F574823E-4863-4013-A4EA-C6D9943246E6")
    ISignatureBuilder : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Add( 
            /* [in] */ DWORD value) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddSignedInt( 
            /* [in] */ LONG value) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddToken( 
            /* [in] */ mdToken token) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddElementType( 
            /* [in] */ CorElementType type) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddData( 
            /* [in] */ __RPC__in const BYTE *pData,
            /* [in] */ DWORD cbSize) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddSignature( 
            /* [in] */ __RPC__in_opt ISignatureBuilder *pSignature) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Clear( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetSize( 
            /* [out] */ __RPC__out DWORD *pcbSize) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCorSignature( 
            /* [in] */ DWORD cbBuffer,
            /* [size_is][length_is][out] */ __RPC__out_ecount_part(cbBuffer, cbBuffer) BYTE *pCorSignature,
            /* [out] */ __RPC__out DWORD *pcbSignature) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCorSignaturePtr( 
            /* [out] */ __RPC__deref_out_opt const BYTE **ppSignature) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ISignatureBuilderVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in ISignatureBuilder * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in ISignatureBuilder * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in ISignatureBuilder * This);
        
        DECLSPEC_XFGVIRT(ISignatureBuilder, Add)
        HRESULT ( STDMETHODCALLTYPE *Add )( 
            __RPC__in ISignatureBuilder * This,
            /* [in] */ DWORD value);
        
        DECLSPEC_XFGVIRT(ISignatureBuilder, AddSignedInt)
        HRESULT ( STDMETHODCALLTYPE *AddSignedInt )( 
            __RPC__in ISignatureBuilder * This,
            /* [in] */ LONG value);
        
        DECLSPEC_XFGVIRT(ISignatureBuilder, AddToken)
        HRESULT ( STDMETHODCALLTYPE *AddToken )( 
            __RPC__in ISignatureBuilder * This,
            /* [in] */ mdToken token);
        
        DECLSPEC_XFGVIRT(ISignatureBuilder, AddElementType)
        HRESULT ( STDMETHODCALLTYPE *AddElementType )( 
            __RPC__in ISignatureBuilder * This,
            /* [in] */ CorElementType type);
        
        DECLSPEC_XFGVIRT(ISignatureBuilder, AddData)
        HRESULT ( STDMETHODCALLTYPE *AddData )( 
            __RPC__in ISignatureBuilder * This,
            /* [in] */ __RPC__in const BYTE *pData,
            /* [in] */ DWORD cbSize);
        
        DECLSPEC_XFGVIRT(ISignatureBuilder, AddSignature)
        HRESULT ( STDMETHODCALLTYPE *AddSignature )( 
            __RPC__in ISignatureBuilder * This,
            /* [in] */ __RPC__in_opt ISignatureBuilder *pSignature);
        
        DECLSPEC_XFGVIRT(ISignatureBuilder, Clear)
        HRESULT ( STDMETHODCALLTYPE *Clear )( 
            __RPC__in ISignatureBuilder * This);
        
        DECLSPEC_XFGVIRT(ISignatureBuilder, GetSize)
        HRESULT ( STDMETHODCALLTYPE *GetSize )( 
            __RPC__in ISignatureBuilder * This,
            /* [out] */ __RPC__out DWORD *pcbSize);
        
        DECLSPEC_XFGVIRT(ISignatureBuilder, GetCorSignature)
        HRESULT ( STDMETHODCALLTYPE *GetCorSignature )( 
            __RPC__in ISignatureBuilder * This,
            /* [in] */ DWORD cbBuffer,
            /* [size_is][length_is][out] */ __RPC__out_ecount_part(cbBuffer, cbBuffer) BYTE *pCorSignature,
            /* [out] */ __RPC__out DWORD *pcbSignature);
        
        DECLSPEC_XFGVIRT(ISignatureBuilder, GetCorSignaturePtr)
        HRESULT ( STDMETHODCALLTYPE *GetCorSignaturePtr )( 
            __RPC__in ISignatureBuilder * This,
            /* [out] */ __RPC__deref_out_opt const BYTE **ppSignature);
        
        END_INTERFACE
    } ISignatureBuilderVtbl;

    interface ISignatureBuilder
    {
        CONST_VTBL struct ISignatureBuilderVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ISignatureBuilder_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ISignatureBuilder_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ISignatureBuilder_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ISignatureBuilder_Add(This,value)	\
    ( (This)->lpVtbl -> Add(This,value) ) 

#define ISignatureBuilder_AddSignedInt(This,value)	\
    ( (This)->lpVtbl -> AddSignedInt(This,value) ) 

#define ISignatureBuilder_AddToken(This,token)	\
    ( (This)->lpVtbl -> AddToken(This,token) ) 

#define ISignatureBuilder_AddElementType(This,type)	\
    ( (This)->lpVtbl -> AddElementType(This,type) ) 

#define ISignatureBuilder_AddData(This,pData,cbSize)	\
    ( (This)->lpVtbl -> AddData(This,pData,cbSize) ) 

#define ISignatureBuilder_AddSignature(This,pSignature)	\
    ( (This)->lpVtbl -> AddSignature(This,pSignature) ) 

#define ISignatureBuilder_Clear(This)	\
    ( (This)->lpVtbl -> Clear(This) ) 

#define ISignatureBuilder_GetSize(This,pcbSize)	\
    ( (This)->lpVtbl -> GetSize(This,pcbSize) ) 

#define ISignatureBuilder_GetCorSignature(This,cbBuffer,pCorSignature,pcbSignature)	\
    ( (This)->lpVtbl -> GetCorSignature(This,cbBuffer,pCorSignature,pcbSignature) ) 

#define ISignatureBuilder_GetCorSignaturePtr(This,ppSignature)	\
    ( (This)->lpVtbl -> GetCorSignaturePtr(This,ppSignature) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ISignatureBuilder_INTERFACE_DEFINED__ */


#ifndef __ITypeCreator_INTERFACE_DEFINED__
#define __ITypeCreator_INTERFACE_DEFINED__

/* interface ITypeCreator */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ITypeCreator;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("C6D612FA-B550-48E3-8859-DE440CF66627")
    ITypeCreator : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE FromSignature( 
            /* [in] */ DWORD cbBuffer,
            /* [in] */ __RPC__in const BYTE *pCorSignature,
            /* [out] */ __RPC__deref_out_opt IType **ppType,
            /* [optional][full][out][in] */ __RPC__inout_opt DWORD *pdwSigSize) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE FromCorElement( 
            /* [in] */ CorElementType type,
            /* [out] */ __RPC__deref_out_opt IType **ppType) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE FromToken( 
            /* [in] */ CorElementType type,
            /* [in] */ mdToken token,
            /* [out] */ __RPC__deref_out_opt IType **ppType) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ITypeCreatorVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in ITypeCreator * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in ITypeCreator * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in ITypeCreator * This);
        
        DECLSPEC_XFGVIRT(ITypeCreator, FromSignature)
        HRESULT ( STDMETHODCALLTYPE *FromSignature )( 
            __RPC__in ITypeCreator * This,
            /* [in] */ DWORD cbBuffer,
            /* [in] */ __RPC__in const BYTE *pCorSignature,
            /* [out] */ __RPC__deref_out_opt IType **ppType,
            /* [optional][full][out][in] */ __RPC__inout_opt DWORD *pdwSigSize);
        
        DECLSPEC_XFGVIRT(ITypeCreator, FromCorElement)
        HRESULT ( STDMETHODCALLTYPE *FromCorElement )( 
            __RPC__in ITypeCreator * This,
            /* [in] */ CorElementType type,
            /* [out] */ __RPC__deref_out_opt IType **ppType);
        
        DECLSPEC_XFGVIRT(ITypeCreator, FromToken)
        HRESULT ( STDMETHODCALLTYPE *FromToken )( 
            __RPC__in ITypeCreator * This,
            /* [in] */ CorElementType type,
            /* [in] */ mdToken token,
            /* [out] */ __RPC__deref_out_opt IType **ppType);
        
        END_INTERFACE
    } ITypeCreatorVtbl;

    interface ITypeCreator
    {
        CONST_VTBL struct ITypeCreatorVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ITypeCreator_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ITypeCreator_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ITypeCreator_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ITypeCreator_FromSignature(This,cbBuffer,pCorSignature,ppType,pdwSigSize)	\
    ( (This)->lpVtbl -> FromSignature(This,cbBuffer,pCorSignature,ppType,pdwSigSize) ) 

#define ITypeCreator_FromCorElement(This,type,ppType)	\
    ( (This)->lpVtbl -> FromCorElement(This,type,ppType) ) 

#define ITypeCreator_FromToken(This,type,token,ppType)	\
    ( (This)->lpVtbl -> FromToken(This,type,token,ppType) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ITypeCreator_INTERFACE_DEFINED__ */


#ifndef __IMethodLocal_INTERFACE_DEFINED__
#define __IMethodLocal_INTERFACE_DEFINED__

/* interface IMethodLocal */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IMethodLocal;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("F8C007DB-0D35-4726-9EDC-781590E30688")
    IMethodLocal : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetType( 
            /* [out] */ __RPC__deref_out_opt IType **ppType) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IMethodLocalVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IMethodLocal * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IMethodLocal * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IMethodLocal * This);
        
        DECLSPEC_XFGVIRT(IMethodLocal, GetType)
        HRESULT ( STDMETHODCALLTYPE *GetType )( 
            __RPC__in IMethodLocal * This,
            /* [out] */ __RPC__deref_out_opt IType **ppType);
        
        END_INTERFACE
    } IMethodLocalVtbl;

    interface IMethodLocal
    {
        CONST_VTBL struct IMethodLocalVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IMethodLocal_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IMethodLocal_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IMethodLocal_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IMethodLocal_GetType(This,ppType)	\
    ( (This)->lpVtbl -> GetType(This,ppType) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IMethodLocal_INTERFACE_DEFINED__ */


#ifndef __IMethodParameter_INTERFACE_DEFINED__
#define __IMethodParameter_INTERFACE_DEFINED__

/* interface IMethodParameter */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IMethodParameter;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("26255678-9F51-433F-89B1-51B978EB4C2B")
    IMethodParameter : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetType( 
            /* [out] */ __RPC__deref_out_opt IType **ppType) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IMethodParameterVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IMethodParameter * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IMethodParameter * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IMethodParameter * This);
        
        DECLSPEC_XFGVIRT(IMethodParameter, GetType)
        HRESULT ( STDMETHODCALLTYPE *GetType )( 
            __RPC__in IMethodParameter * This,
            /* [out] */ __RPC__deref_out_opt IType **ppType);
        
        END_INTERFACE
    } IMethodParameterVtbl;

    interface IMethodParameter
    {
        CONST_VTBL struct IMethodParameterVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IMethodParameter_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IMethodParameter_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IMethodParameter_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IMethodParameter_GetType(This,ppType)	\
    ( (This)->lpVtbl -> GetType(This,ppType) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IMethodParameter_INTERFACE_DEFINED__ */


#ifndef __IEnumMethodLocals_INTERFACE_DEFINED__
#define __IEnumMethodLocals_INTERFACE_DEFINED__

/* interface IEnumMethodLocals */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IEnumMethodLocals;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("C4440146-7E2D-4B1A-8F69-D6E4817D7295")
    IEnumMethodLocals : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Next( 
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IMethodLocal **rgelt,
            /* [out] */ __RPC__out ULONG *pceltFetched) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Skip( 
            /* [in] */ ULONG celt) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Reset( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Clone( 
            /* [out] */ __RPC__deref_out_opt IEnumMethodLocals **ppenum) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCount( 
            /* [out] */ __RPC__out DWORD *pLength) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IEnumMethodLocalsVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IEnumMethodLocals * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IEnumMethodLocals * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IEnumMethodLocals * This);
        
        DECLSPEC_XFGVIRT(IEnumMethodLocals, Next)
        HRESULT ( STDMETHODCALLTYPE *Next )( 
            __RPC__in IEnumMethodLocals * This,
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IMethodLocal **rgelt,
            /* [out] */ __RPC__out ULONG *pceltFetched);
        
        DECLSPEC_XFGVIRT(IEnumMethodLocals, Skip)
        HRESULT ( STDMETHODCALLTYPE *Skip )( 
            __RPC__in IEnumMethodLocals * This,
            /* [in] */ ULONG celt);
        
        DECLSPEC_XFGVIRT(IEnumMethodLocals, Reset)
        HRESULT ( STDMETHODCALLTYPE *Reset )( 
            __RPC__in IEnumMethodLocals * This);
        
        DECLSPEC_XFGVIRT(IEnumMethodLocals, Clone)
        HRESULT ( STDMETHODCALLTYPE *Clone )( 
            __RPC__in IEnumMethodLocals * This,
            /* [out] */ __RPC__deref_out_opt IEnumMethodLocals **ppenum);
        
        DECLSPEC_XFGVIRT(IEnumMethodLocals, GetCount)
        HRESULT ( STDMETHODCALLTYPE *GetCount )( 
            __RPC__in IEnumMethodLocals * This,
            /* [out] */ __RPC__out DWORD *pLength);
        
        END_INTERFACE
    } IEnumMethodLocalsVtbl;

    interface IEnumMethodLocals
    {
        CONST_VTBL struct IEnumMethodLocalsVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IEnumMethodLocals_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IEnumMethodLocals_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IEnumMethodLocals_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IEnumMethodLocals_Next(This,celt,rgelt,pceltFetched)	\
    ( (This)->lpVtbl -> Next(This,celt,rgelt,pceltFetched) ) 

#define IEnumMethodLocals_Skip(This,celt)	\
    ( (This)->lpVtbl -> Skip(This,celt) ) 

#define IEnumMethodLocals_Reset(This)	\
    ( (This)->lpVtbl -> Reset(This) ) 

#define IEnumMethodLocals_Clone(This,ppenum)	\
    ( (This)->lpVtbl -> Clone(This,ppenum) ) 

#define IEnumMethodLocals_GetCount(This,pLength)	\
    ( (This)->lpVtbl -> GetCount(This,pLength) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IEnumMethodLocals_INTERFACE_DEFINED__ */


#ifndef __IEnumMethodParameters_INTERFACE_DEFINED__
#define __IEnumMethodParameters_INTERFACE_DEFINED__

/* interface IEnumMethodParameters */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IEnumMethodParameters;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("2DBC9FAB-93BD-4733-82FA-EA3B3D558A0B")
    IEnumMethodParameters : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Next( 
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IMethodParameter **rgelt,
            /* [out] */ __RPC__out ULONG *pceltFetched) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Skip( 
            /* [in] */ ULONG celt) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Reset( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Clone( 
            /* [out] */ __RPC__deref_out_opt IEnumMethodParameters **ppenum) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCount( 
            /* [out] */ __RPC__out DWORD *pLength) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IEnumMethodParametersVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IEnumMethodParameters * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IEnumMethodParameters * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IEnumMethodParameters * This);
        
        DECLSPEC_XFGVIRT(IEnumMethodParameters, Next)
        HRESULT ( STDMETHODCALLTYPE *Next )( 
            __RPC__in IEnumMethodParameters * This,
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IMethodParameter **rgelt,
            /* [out] */ __RPC__out ULONG *pceltFetched);
        
        DECLSPEC_XFGVIRT(IEnumMethodParameters, Skip)
        HRESULT ( STDMETHODCALLTYPE *Skip )( 
            __RPC__in IEnumMethodParameters * This,
            /* [in] */ ULONG celt);
        
        DECLSPEC_XFGVIRT(IEnumMethodParameters, Reset)
        HRESULT ( STDMETHODCALLTYPE *Reset )( 
            __RPC__in IEnumMethodParameters * This);
        
        DECLSPEC_XFGVIRT(IEnumMethodParameters, Clone)
        HRESULT ( STDMETHODCALLTYPE *Clone )( 
            __RPC__in IEnumMethodParameters * This,
            /* [out] */ __RPC__deref_out_opt IEnumMethodParameters **ppenum);
        
        DECLSPEC_XFGVIRT(IEnumMethodParameters, GetCount)
        HRESULT ( STDMETHODCALLTYPE *GetCount )( 
            __RPC__in IEnumMethodParameters * This,
            /* [out] */ __RPC__out DWORD *pLength);
        
        END_INTERFACE
    } IEnumMethodParametersVtbl;

    interface IEnumMethodParameters
    {
        CONST_VTBL struct IEnumMethodParametersVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IEnumMethodParameters_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IEnumMethodParameters_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IEnumMethodParameters_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IEnumMethodParameters_Next(This,celt,rgelt,pceltFetched)	\
    ( (This)->lpVtbl -> Next(This,celt,rgelt,pceltFetched) ) 

#define IEnumMethodParameters_Skip(This,celt)	\
    ( (This)->lpVtbl -> Skip(This,celt) ) 

#define IEnumMethodParameters_Reset(This)	\
    ( (This)->lpVtbl -> Reset(This) ) 

#define IEnumMethodParameters_Clone(This,ppenum)	\
    ( (This)->lpVtbl -> Clone(This,ppenum) ) 

#define IEnumMethodParameters_GetCount(This,pLength)	\
    ( (This)->lpVtbl -> GetCount(This,pLength) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IEnumMethodParameters_INTERFACE_DEFINED__ */


#ifndef __ISingleRetDefaultInstrumentation_INTERFACE_DEFINED__
#define __ISingleRetDefaultInstrumentation_INTERFACE_DEFINED__

/* interface ISingleRetDefaultInstrumentation */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ISingleRetDefaultInstrumentation;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("2ED40F43-E51A-41A6-91FC-6FA9163C62E9")
    ISingleRetDefaultInstrumentation : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Initialize( 
            /* [in] */ __RPC__in_opt IInstructionGraph *pInstructionGraph) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ApplySingleRetDefaultInstrumentation( void) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ISingleRetDefaultInstrumentationVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in ISingleRetDefaultInstrumentation * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in ISingleRetDefaultInstrumentation * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in ISingleRetDefaultInstrumentation * This);
        
        DECLSPEC_XFGVIRT(ISingleRetDefaultInstrumentation, Initialize)
        HRESULT ( STDMETHODCALLTYPE *Initialize )( 
            __RPC__in ISingleRetDefaultInstrumentation * This,
            /* [in] */ __RPC__in_opt IInstructionGraph *pInstructionGraph);
        
        DECLSPEC_XFGVIRT(ISingleRetDefaultInstrumentation, ApplySingleRetDefaultInstrumentation)
        HRESULT ( STDMETHODCALLTYPE *ApplySingleRetDefaultInstrumentation )( 
            __RPC__in ISingleRetDefaultInstrumentation * This);
        
        END_INTERFACE
    } ISingleRetDefaultInstrumentationVtbl;

    interface ISingleRetDefaultInstrumentation
    {
        CONST_VTBL struct ISingleRetDefaultInstrumentationVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ISingleRetDefaultInstrumentation_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ISingleRetDefaultInstrumentation_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ISingleRetDefaultInstrumentation_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ISingleRetDefaultInstrumentation_Initialize(This,pInstructionGraph)	\
    ( (This)->lpVtbl -> Initialize(This,pInstructionGraph) ) 

#define ISingleRetDefaultInstrumentation_ApplySingleRetDefaultInstrumentation(This)	\
    ( (This)->lpVtbl -> ApplySingleRetDefaultInstrumentation(This) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ISingleRetDefaultInstrumentation_INTERFACE_DEFINED__ */


#ifndef __IProfilerManager2_INTERFACE_DEFINED__
#define __IProfilerManager2_INTERFACE_DEFINED__

/* interface IProfilerManager2 */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IProfilerManager2;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("DCB0764D-E18F-4F9A-91E8-6A40FCFE6775")
    IProfilerManager2 : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE DisableProfiling( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ApplyMetadata( 
            /* [in] */ __RPC__in_opt IModuleInfo *pMethodInfo) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IProfilerManager2Vtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IProfilerManager2 * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IProfilerManager2 * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IProfilerManager2 * This);
        
        DECLSPEC_XFGVIRT(IProfilerManager2, DisableProfiling)
        HRESULT ( STDMETHODCALLTYPE *DisableProfiling )( 
            __RPC__in IProfilerManager2 * This);
        
        DECLSPEC_XFGVIRT(IProfilerManager2, ApplyMetadata)
        HRESULT ( STDMETHODCALLTYPE *ApplyMetadata )( 
            __RPC__in IProfilerManager2 * This,
            /* [in] */ __RPC__in_opt IModuleInfo *pMethodInfo);
        
        END_INTERFACE
    } IProfilerManager2Vtbl;

    interface IProfilerManager2
    {
        CONST_VTBL struct IProfilerManager2Vtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IProfilerManager2_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IProfilerManager2_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IProfilerManager2_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IProfilerManager2_DisableProfiling(This)	\
    ( (This)->lpVtbl -> DisableProfiling(This) ) 

#define IProfilerManager2_ApplyMetadata(This,pMethodInfo)	\
    ( (This)->lpVtbl -> ApplyMetadata(This,pMethodInfo) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IProfilerManager2_INTERFACE_DEFINED__ */


#ifndef __IProfilerManager3_INTERFACE_DEFINED__
#define __IProfilerManager3_INTERFACE_DEFINED__

/* interface IProfilerManager3 */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IProfilerManager3;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("0B097E56-55EE-4EC4-B2F4-380B82448B63")
    IProfilerManager3 : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetApiVersion( 
            /* [out] */ __RPC__out DWORD *pApiVer) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IProfilerManager3Vtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IProfilerManager3 * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IProfilerManager3 * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IProfilerManager3 * This);
        
        DECLSPEC_XFGVIRT(IProfilerManager3, GetApiVersion)
        HRESULT ( STDMETHODCALLTYPE *GetApiVersion )( 
            __RPC__in IProfilerManager3 * This,
            /* [out] */ __RPC__out DWORD *pApiVer);
        
        END_INTERFACE
    } IProfilerManager3Vtbl;

    interface IProfilerManager3
    {
        CONST_VTBL struct IProfilerManager3Vtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IProfilerManager3_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IProfilerManager3_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IProfilerManager3_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IProfilerManager3_GetApiVersion(This,pApiVer)	\
    ( (This)->lpVtbl -> GetApiVersion(This,pApiVer) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IProfilerManager3_INTERFACE_DEFINED__ */


#ifndef __IProfilerManager4_INTERFACE_DEFINED__
#define __IProfilerManager4_INTERFACE_DEFINED__

/* interface IProfilerManager4 */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IProfilerManager4;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("24100BD8-58F2-483A-948A-5B0B8186E451")
    IProfilerManager4 : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetGlobalLoggingInstance( 
            /* [out] */ __RPC__deref_out_opt IProfilerManagerLogging **ppLogging) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IProfilerManager4Vtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IProfilerManager4 * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IProfilerManager4 * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IProfilerManager4 * This);
        
        DECLSPEC_XFGVIRT(IProfilerManager4, GetGlobalLoggingInstance)
        HRESULT ( STDMETHODCALLTYPE *GetGlobalLoggingInstance )( 
            __RPC__in IProfilerManager4 * This,
            /* [out] */ __RPC__deref_out_opt IProfilerManagerLogging **ppLogging);
        
        END_INTERFACE
    } IProfilerManager4Vtbl;

    interface IProfilerManager4
    {
        CONST_VTBL struct IProfilerManager4Vtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IProfilerManager4_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IProfilerManager4_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IProfilerManager4_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IProfilerManager4_GetGlobalLoggingInstance(This,ppLogging)	\
    ( (This)->lpVtbl -> GetGlobalLoggingInstance(This,ppLogging) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IProfilerManager4_INTERFACE_DEFINED__ */


#ifndef __IProfilerManager5_INTERFACE_DEFINED__
#define __IProfilerManager5_INTERFACE_DEFINED__

/* interface IProfilerManager5 */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IProfilerManager5;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("AF78C11D-385A-47B1-A4FB-8D6BA7FE9B2D")
    IProfilerManager5 : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE IsInstrumentationMethodRegistered( 
            /* [in] */ __RPC__in REFGUID cslid,
            /* [out] */ __RPC__out BOOL *pfRegistered) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IProfilerManager5Vtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IProfilerManager5 * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IProfilerManager5 * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IProfilerManager5 * This);
        
        DECLSPEC_XFGVIRT(IProfilerManager5, IsInstrumentationMethodRegistered)
        HRESULT ( STDMETHODCALLTYPE *IsInstrumentationMethodRegistered )( 
            __RPC__in IProfilerManager5 * This,
            /* [in] */ __RPC__in REFGUID cslid,
            /* [out] */ __RPC__out BOOL *pfRegistered);
        
        END_INTERFACE
    } IProfilerManager5Vtbl;

    interface IProfilerManager5
    {
        CONST_VTBL struct IProfilerManager5Vtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IProfilerManager5_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IProfilerManager5_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IProfilerManager5_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IProfilerManager5_IsInstrumentationMethodRegistered(This,cslid,pfRegistered)	\
    ( (This)->lpVtbl -> IsInstrumentationMethodRegistered(This,cslid,pfRegistered) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IProfilerManager5_INTERFACE_DEFINED__ */


#ifndef __IProfilerStringManager_INTERFACE_DEFINED__
#define __IProfilerStringManager_INTERFACE_DEFINED__

/* interface IProfilerStringManager */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IProfilerStringManager;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("D7EAEC8F-C4BB-4F5D-99B9-7215FEB0ED57")
    IProfilerStringManager : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE FreeString( 
            /* [optional][in] */ __RPC__in BSTR bstr) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IProfilerStringManagerVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IProfilerStringManager * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IProfilerStringManager * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IProfilerStringManager * This);
        
        DECLSPEC_XFGVIRT(IProfilerStringManager, FreeString)
        HRESULT ( STDMETHODCALLTYPE *FreeString )( 
            __RPC__in IProfilerStringManager * This,
            /* [optional][in] */ __RPC__in BSTR bstr);
        
        END_INTERFACE
    } IProfilerStringManagerVtbl;

    interface IProfilerStringManager
    {
        CONST_VTBL struct IProfilerStringManagerVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IProfilerStringManager_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IProfilerStringManager_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IProfilerStringManager_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IProfilerStringManager_FreeString(This,bstr)	\
    ( (This)->lpVtbl -> FreeString(This,bstr) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IProfilerStringManager_INTERFACE_DEFINED__ */


#ifndef __IInstrumentationMethodExceptionEvents_INTERFACE_DEFINED__
#define __IInstrumentationMethodExceptionEvents_INTERFACE_DEFINED__

/* interface IInstrumentationMethodExceptionEvents */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IInstrumentationMethodExceptionEvents;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("8310B758-6642-46AD-9423-DDA5F9E278AE")
    IInstrumentationMethodExceptionEvents : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE ExceptionCatcherEnter( 
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo,
            /* [in] */ UINT_PTR objectId) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExceptionCatcherLeave( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExceptionSearchCatcherFound( 
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExceptionSearchFilterEnter( 
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExceptionSearchFilterLeave( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExceptionSearchFunctionEnter( 
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExceptionSearchFunctionLeave( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExceptionThrown( 
            /* [in] */ UINT_PTR thrownObjectId) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExceptionUnwindFinallyEnter( 
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExceptionUnwindFinallyLeave( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExceptionUnwindFunctionEnter( 
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ExceptionUnwindFunctionLeave( void) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IInstrumentationMethodExceptionEventsVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodExceptionEvents, ExceptionCatcherEnter)
        HRESULT ( STDMETHODCALLTYPE *ExceptionCatcherEnter )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo,
            /* [in] */ UINT_PTR objectId);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodExceptionEvents, ExceptionCatcherLeave)
        HRESULT ( STDMETHODCALLTYPE *ExceptionCatcherLeave )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodExceptionEvents, ExceptionSearchCatcherFound)
        HRESULT ( STDMETHODCALLTYPE *ExceptionSearchCatcherFound )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodExceptionEvents, ExceptionSearchFilterEnter)
        HRESULT ( STDMETHODCALLTYPE *ExceptionSearchFilterEnter )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodExceptionEvents, ExceptionSearchFilterLeave)
        HRESULT ( STDMETHODCALLTYPE *ExceptionSearchFilterLeave )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodExceptionEvents, ExceptionSearchFunctionEnter)
        HRESULT ( STDMETHODCALLTYPE *ExceptionSearchFunctionEnter )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodExceptionEvents, ExceptionSearchFunctionLeave)
        HRESULT ( STDMETHODCALLTYPE *ExceptionSearchFunctionLeave )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodExceptionEvents, ExceptionThrown)
        HRESULT ( STDMETHODCALLTYPE *ExceptionThrown )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This,
            /* [in] */ UINT_PTR thrownObjectId);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodExceptionEvents, ExceptionUnwindFinallyEnter)
        HRESULT ( STDMETHODCALLTYPE *ExceptionUnwindFinallyEnter )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodExceptionEvents, ExceptionUnwindFinallyLeave)
        HRESULT ( STDMETHODCALLTYPE *ExceptionUnwindFinallyLeave )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodExceptionEvents, ExceptionUnwindFunctionEnter)
        HRESULT ( STDMETHODCALLTYPE *ExceptionUnwindFunctionEnter )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This,
            /* [in] */ __RPC__in_opt IMethodInfo *pMethodInfo);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodExceptionEvents, ExceptionUnwindFunctionLeave)
        HRESULT ( STDMETHODCALLTYPE *ExceptionUnwindFunctionLeave )( 
            __RPC__in IInstrumentationMethodExceptionEvents * This);
        
        END_INTERFACE
    } IInstrumentationMethodExceptionEventsVtbl;

    interface IInstrumentationMethodExceptionEvents
    {
        CONST_VTBL struct IInstrumentationMethodExceptionEventsVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IInstrumentationMethodExceptionEvents_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IInstrumentationMethodExceptionEvents_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IInstrumentationMethodExceptionEvents_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IInstrumentationMethodExceptionEvents_ExceptionCatcherEnter(This,pMethodInfo,objectId)	\
    ( (This)->lpVtbl -> ExceptionCatcherEnter(This,pMethodInfo,objectId) ) 

#define IInstrumentationMethodExceptionEvents_ExceptionCatcherLeave(This)	\
    ( (This)->lpVtbl -> ExceptionCatcherLeave(This) ) 

#define IInstrumentationMethodExceptionEvents_ExceptionSearchCatcherFound(This,pMethodInfo)	\
    ( (This)->lpVtbl -> ExceptionSearchCatcherFound(This,pMethodInfo) ) 

#define IInstrumentationMethodExceptionEvents_ExceptionSearchFilterEnter(This,pMethodInfo)	\
    ( (This)->lpVtbl -> ExceptionSearchFilterEnter(This,pMethodInfo) ) 

#define IInstrumentationMethodExceptionEvents_ExceptionSearchFilterLeave(This)	\
    ( (This)->lpVtbl -> ExceptionSearchFilterLeave(This) ) 

#define IInstrumentationMethodExceptionEvents_ExceptionSearchFunctionEnter(This,pMethodInfo)	\
    ( (This)->lpVtbl -> ExceptionSearchFunctionEnter(This,pMethodInfo) ) 

#define IInstrumentationMethodExceptionEvents_ExceptionSearchFunctionLeave(This)	\
    ( (This)->lpVtbl -> ExceptionSearchFunctionLeave(This) ) 

#define IInstrumentationMethodExceptionEvents_ExceptionThrown(This,thrownObjectId)	\
    ( (This)->lpVtbl -> ExceptionThrown(This,thrownObjectId) ) 

#define IInstrumentationMethodExceptionEvents_ExceptionUnwindFinallyEnter(This,pMethodInfo)	\
    ( (This)->lpVtbl -> ExceptionUnwindFinallyEnter(This,pMethodInfo) ) 

#define IInstrumentationMethodExceptionEvents_ExceptionUnwindFinallyLeave(This)	\
    ( (This)->lpVtbl -> ExceptionUnwindFinallyLeave(This) ) 

#define IInstrumentationMethodExceptionEvents_ExceptionUnwindFunctionEnter(This,pMethodInfo)	\
    ( (This)->lpVtbl -> ExceptionUnwindFunctionEnter(This,pMethodInfo) ) 

#define IInstrumentationMethodExceptionEvents_ExceptionUnwindFunctionLeave(This)	\
    ( (This)->lpVtbl -> ExceptionUnwindFunctionLeave(This) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IInstrumentationMethodExceptionEvents_INTERFACE_DEFINED__ */


#ifndef __IEnumInstructions_INTERFACE_DEFINED__
#define __IEnumInstructions_INTERFACE_DEFINED__

/* interface IEnumInstructions */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IEnumInstructions;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("2A4A827A-046D-4927-BD90-CE9607607280")
    IEnumInstructions : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Next( 
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IInstruction **rgelt,
            /* [in] */ __RPC__in ULONG *pceltFetched) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Skip( 
            /* [in] */ ULONG celt) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Reset( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Clone( 
            /* [out] */ __RPC__deref_out_opt IEnumInstructions **ppenum) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCount( 
            /* [out] */ __RPC__out DWORD *pLength) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IEnumInstructionsVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IEnumInstructions * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IEnumInstructions * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IEnumInstructions * This);
        
        DECLSPEC_XFGVIRT(IEnumInstructions, Next)
        HRESULT ( STDMETHODCALLTYPE *Next )( 
            __RPC__in IEnumInstructions * This,
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IInstruction **rgelt,
            /* [in] */ __RPC__in ULONG *pceltFetched);
        
        DECLSPEC_XFGVIRT(IEnumInstructions, Skip)
        HRESULT ( STDMETHODCALLTYPE *Skip )( 
            __RPC__in IEnumInstructions * This,
            /* [in] */ ULONG celt);
        
        DECLSPEC_XFGVIRT(IEnumInstructions, Reset)
        HRESULT ( STDMETHODCALLTYPE *Reset )( 
            __RPC__in IEnumInstructions * This);
        
        DECLSPEC_XFGVIRT(IEnumInstructions, Clone)
        HRESULT ( STDMETHODCALLTYPE *Clone )( 
            __RPC__in IEnumInstructions * This,
            /* [out] */ __RPC__deref_out_opt IEnumInstructions **ppenum);
        
        DECLSPEC_XFGVIRT(IEnumInstructions, GetCount)
        HRESULT ( STDMETHODCALLTYPE *GetCount )( 
            __RPC__in IEnumInstructions * This,
            /* [out] */ __RPC__out DWORD *pLength);
        
        END_INTERFACE
    } IEnumInstructionsVtbl;

    interface IEnumInstructions
    {
        CONST_VTBL struct IEnumInstructionsVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IEnumInstructions_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IEnumInstructions_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IEnumInstructions_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IEnumInstructions_Next(This,celt,rgelt,pceltFetched)	\
    ( (This)->lpVtbl -> Next(This,celt,rgelt,pceltFetched) ) 

#define IEnumInstructions_Skip(This,celt)	\
    ( (This)->lpVtbl -> Skip(This,celt) ) 

#define IEnumInstructions_Reset(This)	\
    ( (This)->lpVtbl -> Reset(This) ) 

#define IEnumInstructions_Clone(This,ppenum)	\
    ( (This)->lpVtbl -> Clone(This,ppenum) ) 

#define IEnumInstructions_GetCount(This,pLength)	\
    ( (This)->lpVtbl -> GetCount(This,pLength) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IEnumInstructions_INTERFACE_DEFINED__ */


#ifndef __IInstructionFactory_INTERFACE_DEFINED__
#define __IInstructionFactory_INTERFACE_DEFINED__

/* interface IInstructionFactory */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IInstructionFactory;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("CF059876-C5CA-4EBF-ACB9-9C58009CE31A")
    IInstructionFactory : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE CreateInstruction( 
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateByteOperandInstruction( 
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ BYTE operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateUShortOperandInstruction( 
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ USHORT operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateIntOperandInstruction( 
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ INT32 operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateLongOperandInstruction( 
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ INT64 operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateFloatOperandInstruction( 
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ float operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateDoubleOperandInstruction( 
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ double operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateTokenOperandInstruction( 
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ mdToken operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateBranchInstruction( 
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ __RPC__in_opt IInstruction *pBranchTarget,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateSwitchInstruction( 
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ DWORD cBranchTargets,
            /* [length_is][size_is][in] */ __RPC__in_ecount_part(cBranchTargets, cBranchTargets) IInstruction **ppBranchTargets,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateLoadConstInstruction( 
            /* [in] */ int value,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateLoadLocalInstruction( 
            /* [in] */ USHORT index,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateLoadLocalAddressInstruction( 
            /* [in] */ USHORT index,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateStoreLocalInstruction( 
            /* [in] */ USHORT index,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateLoadArgInstruction( 
            /* [in] */ USHORT index,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateLoadArgAddressInstruction( 
            /* [in] */ USHORT index,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE DecodeInstructionByteStream( 
            /* [in] */ DWORD cbMethod,
            /* [size_is][in] */ __RPC__in_ecount_full(cbMethod) LPCBYTE instructionBytes,
            /* [out] */ __RPC__deref_out_opt IInstructionGraph **ppInstructionGraph) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IInstructionFactoryVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IInstructionFactory * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IInstructionFactory * This);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateByteOperandInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateByteOperandInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ BYTE operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateUShortOperandInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateUShortOperandInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ USHORT operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateIntOperandInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateIntOperandInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ INT32 operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateLongOperandInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateLongOperandInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ INT64 operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateFloatOperandInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateFloatOperandInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ float operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateDoubleOperandInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateDoubleOperandInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ double operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateTokenOperandInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateTokenOperandInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ mdToken operand,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateBranchInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateBranchInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ __RPC__in_opt IInstruction *pBranchTarget,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateSwitchInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateSwitchInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ enum ILOrdinalOpcode opcode,
            /* [in] */ DWORD cBranchTargets,
            /* [length_is][size_is][in] */ __RPC__in_ecount_part(cBranchTargets, cBranchTargets) IInstruction **ppBranchTargets,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateLoadConstInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateLoadConstInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ int value,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateLoadLocalInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateLoadLocalInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ USHORT index,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateLoadLocalAddressInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateLoadLocalAddressInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ USHORT index,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateStoreLocalInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateStoreLocalInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ USHORT index,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateLoadArgInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateLoadArgInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ USHORT index,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, CreateLoadArgAddressInstruction)
        HRESULT ( STDMETHODCALLTYPE *CreateLoadArgAddressInstruction )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ USHORT index,
            /* [out] */ __RPC__deref_out_opt IInstruction **ppInstruction);
        
        DECLSPEC_XFGVIRT(IInstructionFactory, DecodeInstructionByteStream)
        HRESULT ( STDMETHODCALLTYPE *DecodeInstructionByteStream )( 
            __RPC__in IInstructionFactory * This,
            /* [in] */ DWORD cbMethod,
            /* [size_is][in] */ __RPC__in_ecount_full(cbMethod) LPCBYTE instructionBytes,
            /* [out] */ __RPC__deref_out_opt IInstructionGraph **ppInstructionGraph);
        
        END_INTERFACE
    } IInstructionFactoryVtbl;

    interface IInstructionFactory
    {
        CONST_VTBL struct IInstructionFactoryVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IInstructionFactory_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IInstructionFactory_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IInstructionFactory_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IInstructionFactory_CreateInstruction(This,opcode,ppInstruction)	\
    ( (This)->lpVtbl -> CreateInstruction(This,opcode,ppInstruction) ) 

#define IInstructionFactory_CreateByteOperandInstruction(This,opcode,operand,ppInstruction)	\
    ( (This)->lpVtbl -> CreateByteOperandInstruction(This,opcode,operand,ppInstruction) ) 

#define IInstructionFactory_CreateUShortOperandInstruction(This,opcode,operand,ppInstruction)	\
    ( (This)->lpVtbl -> CreateUShortOperandInstruction(This,opcode,operand,ppInstruction) ) 

#define IInstructionFactory_CreateIntOperandInstruction(This,opcode,operand,ppInstruction)	\
    ( (This)->lpVtbl -> CreateIntOperandInstruction(This,opcode,operand,ppInstruction) ) 

#define IInstructionFactory_CreateLongOperandInstruction(This,opcode,operand,ppInstruction)	\
    ( (This)->lpVtbl -> CreateLongOperandInstruction(This,opcode,operand,ppInstruction) ) 

#define IInstructionFactory_CreateFloatOperandInstruction(This,opcode,operand,ppInstruction)	\
    ( (This)->lpVtbl -> CreateFloatOperandInstruction(This,opcode,operand,ppInstruction) ) 

#define IInstructionFactory_CreateDoubleOperandInstruction(This,opcode,operand,ppInstruction)	\
    ( (This)->lpVtbl -> CreateDoubleOperandInstruction(This,opcode,operand,ppInstruction) ) 

#define IInstructionFactory_CreateTokenOperandInstruction(This,opcode,operand,ppInstruction)	\
    ( (This)->lpVtbl -> CreateTokenOperandInstruction(This,opcode,operand,ppInstruction) ) 

#define IInstructionFactory_CreateBranchInstruction(This,opcode,pBranchTarget,ppInstruction)	\
    ( (This)->lpVtbl -> CreateBranchInstruction(This,opcode,pBranchTarget,ppInstruction) ) 

#define IInstructionFactory_CreateSwitchInstruction(This,opcode,cBranchTargets,ppBranchTargets,ppInstruction)	\
    ( (This)->lpVtbl -> CreateSwitchInstruction(This,opcode,cBranchTargets,ppBranchTargets,ppInstruction) ) 

#define IInstructionFactory_CreateLoadConstInstruction(This,value,ppInstruction)	\
    ( (This)->lpVtbl -> CreateLoadConstInstruction(This,value,ppInstruction) ) 

#define IInstructionFactory_CreateLoadLocalInstruction(This,index,ppInstruction)	\
    ( (This)->lpVtbl -> CreateLoadLocalInstruction(This,index,ppInstruction) ) 

#define IInstructionFactory_CreateLoadLocalAddressInstruction(This,index,ppInstruction)	\
    ( (This)->lpVtbl -> CreateLoadLocalAddressInstruction(This,index,ppInstruction) ) 

#define IInstructionFactory_CreateStoreLocalInstruction(This,index,ppInstruction)	\
    ( (This)->lpVtbl -> CreateStoreLocalInstruction(This,index,ppInstruction) ) 

#define IInstructionFactory_CreateLoadArgInstruction(This,index,ppInstruction)	\
    ( (This)->lpVtbl -> CreateLoadArgInstruction(This,index,ppInstruction) ) 

#define IInstructionFactory_CreateLoadArgAddressInstruction(This,index,ppInstruction)	\
    ( (This)->lpVtbl -> CreateLoadArgAddressInstruction(This,index,ppInstruction) ) 

#define IInstructionFactory_DecodeInstructionByteStream(This,cbMethod,instructionBytes,ppInstructionGraph)	\
    ( (This)->lpVtbl -> DecodeInstructionByteStream(This,cbMethod,instructionBytes,ppInstructionGraph) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IInstructionFactory_INTERFACE_DEFINED__ */


#ifndef __IEnumAppMethodInfo_INTERFACE_DEFINED__
#define __IEnumAppMethodInfo_INTERFACE_DEFINED__

/* interface IEnumAppMethodInfo */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IEnumAppMethodInfo;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("541A45B7-D194-47EE-9231-AB69D27D1D59")
    IEnumAppMethodInfo : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Next( 
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IEnumAppMethodInfo **rgelt,
            /* [in] */ __RPC__in ULONG *pceltFetched) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Skip( 
            /* [in] */ ULONG celt) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Reset( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Clone( 
            /* [out] */ __RPC__deref_out_opt IEnumAppMethodInfo **ppenum) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCount( 
            /* [out] */ __RPC__out DWORD *pLength) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IEnumAppMethodInfoVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IEnumAppMethodInfo * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IEnumAppMethodInfo * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IEnumAppMethodInfo * This);
        
        DECLSPEC_XFGVIRT(IEnumAppMethodInfo, Next)
        HRESULT ( STDMETHODCALLTYPE *Next )( 
            __RPC__in IEnumAppMethodInfo * This,
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IEnumAppMethodInfo **rgelt,
            /* [in] */ __RPC__in ULONG *pceltFetched);
        
        DECLSPEC_XFGVIRT(IEnumAppMethodInfo, Skip)
        HRESULT ( STDMETHODCALLTYPE *Skip )( 
            __RPC__in IEnumAppMethodInfo * This,
            /* [in] */ ULONG celt);
        
        DECLSPEC_XFGVIRT(IEnumAppMethodInfo, Reset)
        HRESULT ( STDMETHODCALLTYPE *Reset )( 
            __RPC__in IEnumAppMethodInfo * This);
        
        DECLSPEC_XFGVIRT(IEnumAppMethodInfo, Clone)
        HRESULT ( STDMETHODCALLTYPE *Clone )( 
            __RPC__in IEnumAppMethodInfo * This,
            /* [out] */ __RPC__deref_out_opt IEnumAppMethodInfo **ppenum);
        
        DECLSPEC_XFGVIRT(IEnumAppMethodInfo, GetCount)
        HRESULT ( STDMETHODCALLTYPE *GetCount )( 
            __RPC__in IEnumAppMethodInfo * This,
            /* [out] */ __RPC__out DWORD *pLength);
        
        END_INTERFACE
    } IEnumAppMethodInfoVtbl;

    interface IEnumAppMethodInfo
    {
        CONST_VTBL struct IEnumAppMethodInfoVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IEnumAppMethodInfo_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IEnumAppMethodInfo_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IEnumAppMethodInfo_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IEnumAppMethodInfo_Next(This,celt,rgelt,pceltFetched)	\
    ( (This)->lpVtbl -> Next(This,celt,rgelt,pceltFetched) ) 

#define IEnumAppMethodInfo_Skip(This,celt)	\
    ( (This)->lpVtbl -> Skip(This,celt) ) 

#define IEnumAppMethodInfo_Reset(This)	\
    ( (This)->lpVtbl -> Reset(This) ) 

#define IEnumAppMethodInfo_Clone(This,ppenum)	\
    ( (This)->lpVtbl -> Clone(This,ppenum) ) 

#define IEnumAppMethodInfo_GetCount(This,pLength)	\
    ( (This)->lpVtbl -> GetCount(This,pLength) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IEnumAppMethodInfo_INTERFACE_DEFINED__ */


#ifndef __ILocalVariableCollection2_INTERFACE_DEFINED__
#define __ILocalVariableCollection2_INTERFACE_DEFINED__

/* interface ILocalVariableCollection2 */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ILocalVariableCollection2;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("61657FE7-BFBB-4B60-BBA7-1D3C326FA470")
    ILocalVariableCollection2 : public ILocalVariableCollection
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetEnum( 
            /* [out] */ __RPC__deref_out_opt IEnumMethodLocals **ppEnumMethodLocals) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ILocalVariableCollection2Vtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in ILocalVariableCollection2 * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in ILocalVariableCollection2 * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in ILocalVariableCollection2 * This);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection, Initialize)
        HRESULT ( STDMETHODCALLTYPE *Initialize )( 
            __RPC__in ILocalVariableCollection2 * This);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection, GetCorSignature)
        HRESULT ( STDMETHODCALLTYPE *GetCorSignature )( 
            __RPC__in ILocalVariableCollection2 * This,
            /* [out] */ __RPC__deref_out_opt ISignatureBuilder **ppSignature);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection, GetCount)
        HRESULT ( STDMETHODCALLTYPE *GetCount )( 
            __RPC__in ILocalVariableCollection2 * This,
            /* [out] */ __RPC__out DWORD *pdwCount);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection, AddLocal)
        HRESULT ( STDMETHODCALLTYPE *AddLocal )( 
            __RPC__in ILocalVariableCollection2 * This,
            /* [in] */ __RPC__in_opt IType *pType,
            /* [optional][full][out][in] */ __RPC__inout_opt DWORD *pIndex);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection, ReplaceSignature)
        HRESULT ( STDMETHODCALLTYPE *ReplaceSignature )( 
            __RPC__in ILocalVariableCollection2 * This,
            /* [in] */ __RPC__in const BYTE *pSignature,
            /* [in] */ DWORD dwSigSize);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection, CommitSignature)
        HRESULT ( STDMETHODCALLTYPE *CommitSignature )( 
            __RPC__in ILocalVariableCollection2 * This);
        
        DECLSPEC_XFGVIRT(ILocalVariableCollection2, GetEnum)
        HRESULT ( STDMETHODCALLTYPE *GetEnum )( 
            __RPC__in ILocalVariableCollection2 * This,
            /* [out] */ __RPC__deref_out_opt IEnumMethodLocals **ppEnumMethodLocals);
        
        END_INTERFACE
    } ILocalVariableCollection2Vtbl;

    interface ILocalVariableCollection2
    {
        CONST_VTBL struct ILocalVariableCollection2Vtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ILocalVariableCollection2_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ILocalVariableCollection2_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ILocalVariableCollection2_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ILocalVariableCollection2_Initialize(This)	\
    ( (This)->lpVtbl -> Initialize(This) ) 

#define ILocalVariableCollection2_GetCorSignature(This,ppSignature)	\
    ( (This)->lpVtbl -> GetCorSignature(This,ppSignature) ) 

#define ILocalVariableCollection2_GetCount(This,pdwCount)	\
    ( (This)->lpVtbl -> GetCount(This,pdwCount) ) 

#define ILocalVariableCollection2_AddLocal(This,pType,pIndex)	\
    ( (This)->lpVtbl -> AddLocal(This,pType,pIndex) ) 

#define ILocalVariableCollection2_ReplaceSignature(This,pSignature,dwSigSize)	\
    ( (This)->lpVtbl -> ReplaceSignature(This,pSignature,dwSigSize) ) 

#define ILocalVariableCollection2_CommitSignature(This)	\
    ( (This)->lpVtbl -> CommitSignature(This) ) 


#define ILocalVariableCollection2_GetEnum(This,ppEnumMethodLocals)	\
    ( (This)->lpVtbl -> GetEnum(This,ppEnumMethodLocals) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ILocalVariableCollection2_INTERFACE_DEFINED__ */


#ifndef __IEnumTypes_INTERFACE_DEFINED__
#define __IEnumTypes_INTERFACE_DEFINED__

/* interface IEnumTypes */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IEnumTypes;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("5618BD13-12FC-4198-A39D-8ED60265AAC6")
    IEnumTypes : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Next( 
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IType **rgelt,
            /* [out] */ __RPC__out ULONG *pceltFetched) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Skip( 
            /* [in] */ ULONG celt) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Reset( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Clone( 
            /* [out] */ __RPC__deref_out_opt IEnumTypes **ppenum) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCount( 
            /* [out] */ __RPC__out DWORD *pLength) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IEnumTypesVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IEnumTypes * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IEnumTypes * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IEnumTypes * This);
        
        DECLSPEC_XFGVIRT(IEnumTypes, Next)
        HRESULT ( STDMETHODCALLTYPE *Next )( 
            __RPC__in IEnumTypes * This,
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IType **rgelt,
            /* [out] */ __RPC__out ULONG *pceltFetched);
        
        DECLSPEC_XFGVIRT(IEnumTypes, Skip)
        HRESULT ( STDMETHODCALLTYPE *Skip )( 
            __RPC__in IEnumTypes * This,
            /* [in] */ ULONG celt);
        
        DECLSPEC_XFGVIRT(IEnumTypes, Reset)
        HRESULT ( STDMETHODCALLTYPE *Reset )( 
            __RPC__in IEnumTypes * This);
        
        DECLSPEC_XFGVIRT(IEnumTypes, Clone)
        HRESULT ( STDMETHODCALLTYPE *Clone )( 
            __RPC__in IEnumTypes * This,
            /* [out] */ __RPC__deref_out_opt IEnumTypes **ppenum);
        
        DECLSPEC_XFGVIRT(IEnumTypes, GetCount)
        HRESULT ( STDMETHODCALLTYPE *GetCount )( 
            __RPC__in IEnumTypes * This,
            /* [out] */ __RPC__out DWORD *pLength);
        
        END_INTERFACE
    } IEnumTypesVtbl;

    interface IEnumTypes
    {
        CONST_VTBL struct IEnumTypesVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IEnumTypes_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IEnumTypes_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IEnumTypes_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IEnumTypes_Next(This,celt,rgelt,pceltFetched)	\
    ( (This)->lpVtbl -> Next(This,celt,rgelt,pceltFetched) ) 

#define IEnumTypes_Skip(This,celt)	\
    ( (This)->lpVtbl -> Skip(This,celt) ) 

#define IEnumTypes_Reset(This)	\
    ( (This)->lpVtbl -> Reset(This) ) 

#define IEnumTypes_Clone(This,ppenum)	\
    ( (This)->lpVtbl -> Clone(This,ppenum) ) 

#define IEnumTypes_GetCount(This,pLength)	\
    ( (This)->lpVtbl -> GetCount(This,pLength) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IEnumTypes_INTERFACE_DEFINED__ */


#ifndef __ISignatureParser_INTERFACE_DEFINED__
#define __ISignatureParser_INTERFACE_DEFINED__

/* interface ISignatureParser */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ISignatureParser;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("33BD020E-372B-40F9-A735-4B4017ED56AC")
    ISignatureParser : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE ParseMethodSignature( 
            /* [in] */ __RPC__in const BYTE *pSignature,
            /* [in] */ ULONG cbSignature,
            /* [optional][full][out][in] */ __RPC__inout_opt ULONG *pCallingConvention,
            /* [optional][full][out][in] */ __RPC__deref_opt_inout_opt IType **ppReturnType,
            /* [optional][full][out][in] */ __RPC__deref_opt_inout_opt IEnumTypes **ppEnumParameterTypes,
            /* [optional][full][out][in] */ __RPC__inout_opt ULONG *pcGenericTypeParameters,
            /* [optional][full][out][in] */ __RPC__inout_opt ULONG *pcbRead) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ParseLocalVarSignature( 
            /* [in] */ __RPC__in const BYTE *pSignature,
            /* [in] */ ULONG cbSignature,
            /* [optional][full][out][in] */ __RPC__inout_opt ULONG *pCallingConvention,
            /* [optional][full][out][in] */ __RPC__deref_opt_inout_opt IEnumTypes **ppEnumTypes,
            /* [optional][full][out][in] */ __RPC__inout_opt ULONG *pcbRead) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ParseTypeSequence( 
            /* [in] */ __RPC__in const BYTE *pBuffer,
            /* [in] */ ULONG cbBuffer,
            /* [in] */ ULONG cTypes,
            /* [optional][full][out][in] */ __RPC__deref_opt_inout_opt IEnumTypes **ppEnumTypes,
            /* [optional][full][out][in] */ __RPC__inout_opt ULONG *pcbRead) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ISignatureParserVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in ISignatureParser * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in ISignatureParser * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in ISignatureParser * This);
        
        DECLSPEC_XFGVIRT(ISignatureParser, ParseMethodSignature)
        HRESULT ( STDMETHODCALLTYPE *ParseMethodSignature )( 
            __RPC__in ISignatureParser * This,
            /* [in] */ __RPC__in const BYTE *pSignature,
            /* [in] */ ULONG cbSignature,
            /* [optional][full][out][in] */ __RPC__inout_opt ULONG *pCallingConvention,
            /* [optional][full][out][in] */ __RPC__deref_opt_inout_opt IType **ppReturnType,
            /* [optional][full][out][in] */ __RPC__deref_opt_inout_opt IEnumTypes **ppEnumParameterTypes,
            /* [optional][full][out][in] */ __RPC__inout_opt ULONG *pcGenericTypeParameters,
            /* [optional][full][out][in] */ __RPC__inout_opt ULONG *pcbRead);
        
        DECLSPEC_XFGVIRT(ISignatureParser, ParseLocalVarSignature)
        HRESULT ( STDMETHODCALLTYPE *ParseLocalVarSignature )( 
            __RPC__in ISignatureParser * This,
            /* [in] */ __RPC__in const BYTE *pSignature,
            /* [in] */ ULONG cbSignature,
            /* [optional][full][out][in] */ __RPC__inout_opt ULONG *pCallingConvention,
            /* [optional][full][out][in] */ __RPC__deref_opt_inout_opt IEnumTypes **ppEnumTypes,
            /* [optional][full][out][in] */ __RPC__inout_opt ULONG *pcbRead);
        
        DECLSPEC_XFGVIRT(ISignatureParser, ParseTypeSequence)
        HRESULT ( STDMETHODCALLTYPE *ParseTypeSequence )( 
            __RPC__in ISignatureParser * This,
            /* [in] */ __RPC__in const BYTE *pBuffer,
            /* [in] */ ULONG cbBuffer,
            /* [in] */ ULONG cTypes,
            /* [optional][full][out][in] */ __RPC__deref_opt_inout_opt IEnumTypes **ppEnumTypes,
            /* [optional][full][out][in] */ __RPC__inout_opt ULONG *pcbRead);
        
        END_INTERFACE
    } ISignatureParserVtbl;

    interface ISignatureParser
    {
        CONST_VTBL struct ISignatureParserVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ISignatureParser_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ISignatureParser_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ISignatureParser_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ISignatureParser_ParseMethodSignature(This,pSignature,cbSignature,pCallingConvention,ppReturnType,ppEnumParameterTypes,pcGenericTypeParameters,pcbRead)	\
    ( (This)->lpVtbl -> ParseMethodSignature(This,pSignature,cbSignature,pCallingConvention,ppReturnType,ppEnumParameterTypes,pcGenericTypeParameters,pcbRead) ) 

#define ISignatureParser_ParseLocalVarSignature(This,pSignature,cbSignature,pCallingConvention,ppEnumTypes,pcbRead)	\
    ( (This)->lpVtbl -> ParseLocalVarSignature(This,pSignature,cbSignature,pCallingConvention,ppEnumTypes,pcbRead) ) 

#define ISignatureParser_ParseTypeSequence(This,pBuffer,cbBuffer,cTypes,ppEnumTypes,pcbRead)	\
    ( (This)->lpVtbl -> ParseTypeSequence(This,pBuffer,cbBuffer,cTypes,ppEnumTypes,pcbRead) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ISignatureParser_INTERFACE_DEFINED__ */


#ifndef __ITokenType_INTERFACE_DEFINED__
#define __ITokenType_INTERFACE_DEFINED__

/* interface ITokenType */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ITokenType;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("77655B33-1B29-4285-9F2D-FF9526E3A0AA")
    ITokenType : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetToken( 
            /* [out] */ __RPC__out mdToken *pToken) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOwningModule( 
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ITokenTypeVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in ITokenType * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in ITokenType * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in ITokenType * This);
        
        DECLSPEC_XFGVIRT(ITokenType, GetToken)
        HRESULT ( STDMETHODCALLTYPE *GetToken )( 
            __RPC__in ITokenType * This,
            /* [out] */ __RPC__out mdToken *pToken);
        
        DECLSPEC_XFGVIRT(ITokenType, GetOwningModule)
        HRESULT ( STDMETHODCALLTYPE *GetOwningModule )( 
            __RPC__in ITokenType * This,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo);
        
        END_INTERFACE
    } ITokenTypeVtbl;

    interface ITokenType
    {
        CONST_VTBL struct ITokenTypeVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ITokenType_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ITokenType_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ITokenType_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ITokenType_GetToken(This,pToken)	\
    ( (This)->lpVtbl -> GetToken(This,pToken) ) 

#define ITokenType_GetOwningModule(This,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetOwningModule(This,ppModuleInfo) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ITokenType_INTERFACE_DEFINED__ */


#ifndef __ICompositeType_INTERFACE_DEFINED__
#define __ICompositeType_INTERFACE_DEFINED__

/* interface ICompositeType */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ICompositeType;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("06B9FD79-0386-4CF3-93DD-A23E95EBC225")
    ICompositeType : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetRelatedType( 
            /* [out] */ __RPC__deref_out_opt IType **ppType) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ICompositeTypeVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in ICompositeType * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in ICompositeType * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in ICompositeType * This);
        
        DECLSPEC_XFGVIRT(ICompositeType, GetRelatedType)
        HRESULT ( STDMETHODCALLTYPE *GetRelatedType )( 
            __RPC__in ICompositeType * This,
            /* [out] */ __RPC__deref_out_opt IType **ppType);
        
        END_INTERFACE
    } ICompositeTypeVtbl;

    interface ICompositeType
    {
        CONST_VTBL struct ICompositeTypeVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ICompositeType_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ICompositeType_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ICompositeType_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ICompositeType_GetRelatedType(This,ppType)	\
    ( (This)->lpVtbl -> GetRelatedType(This,ppType) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ICompositeType_INTERFACE_DEFINED__ */


#ifndef __IGenericParameterType_INTERFACE_DEFINED__
#define __IGenericParameterType_INTERFACE_DEFINED__

/* interface IGenericParameterType */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IGenericParameterType;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("1D5C1393-DC7E-4FEF-8A9D-A3DAF7A55C6E")
    IGenericParameterType : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetPosition( 
            /* [out] */ __RPC__out ULONG *pPosition) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IGenericParameterTypeVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IGenericParameterType * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IGenericParameterType * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IGenericParameterType * This);
        
        DECLSPEC_XFGVIRT(IGenericParameterType, GetPosition)
        HRESULT ( STDMETHODCALLTYPE *GetPosition )( 
            __RPC__in IGenericParameterType * This,
            /* [out] */ __RPC__out ULONG *pPosition);
        
        END_INTERFACE
    } IGenericParameterTypeVtbl;

    interface IGenericParameterType
    {
        CONST_VTBL struct IGenericParameterTypeVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IGenericParameterType_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IGenericParameterType_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IGenericParameterType_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IGenericParameterType_GetPosition(This,pPosition)	\
    ( (This)->lpVtbl -> GetPosition(This,pPosition) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IGenericParameterType_INTERFACE_DEFINED__ */


#ifndef __ISingleRetDefaultInstrumentation2_INTERFACE_DEFINED__
#define __ISingleRetDefaultInstrumentation2_INTERFACE_DEFINED__

/* interface ISingleRetDefaultInstrumentation2 */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ISingleRetDefaultInstrumentation2;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("7A88FF19-F3A1-4C43-89DB-61DF376441B5")
    ISingleRetDefaultInstrumentation2 : public ISingleRetDefaultInstrumentation
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetBranchTargetInstruction( 
            /* [out] */ __RPC__deref_out_opt IInstruction **pInstruction) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ISingleRetDefaultInstrumentation2Vtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in ISingleRetDefaultInstrumentation2 * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in ISingleRetDefaultInstrumentation2 * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in ISingleRetDefaultInstrumentation2 * This);
        
        DECLSPEC_XFGVIRT(ISingleRetDefaultInstrumentation, Initialize)
        HRESULT ( STDMETHODCALLTYPE *Initialize )( 
            __RPC__in ISingleRetDefaultInstrumentation2 * This,
            /* [in] */ __RPC__in_opt IInstructionGraph *pInstructionGraph);
        
        DECLSPEC_XFGVIRT(ISingleRetDefaultInstrumentation, ApplySingleRetDefaultInstrumentation)
        HRESULT ( STDMETHODCALLTYPE *ApplySingleRetDefaultInstrumentation )( 
            __RPC__in ISingleRetDefaultInstrumentation2 * This);
        
        DECLSPEC_XFGVIRT(ISingleRetDefaultInstrumentation2, GetBranchTargetInstruction)
        HRESULT ( STDMETHODCALLTYPE *GetBranchTargetInstruction )( 
            __RPC__in ISingleRetDefaultInstrumentation2 * This,
            /* [out] */ __RPC__deref_out_opt IInstruction **pInstruction);
        
        END_INTERFACE
    } ISingleRetDefaultInstrumentation2Vtbl;

    interface ISingleRetDefaultInstrumentation2
    {
        CONST_VTBL struct ISingleRetDefaultInstrumentation2Vtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ISingleRetDefaultInstrumentation2_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ISingleRetDefaultInstrumentation2_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ISingleRetDefaultInstrumentation2_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ISingleRetDefaultInstrumentation2_Initialize(This,pInstructionGraph)	\
    ( (This)->lpVtbl -> Initialize(This,pInstructionGraph) ) 

#define ISingleRetDefaultInstrumentation2_ApplySingleRetDefaultInstrumentation(This)	\
    ( (This)->lpVtbl -> ApplySingleRetDefaultInstrumentation(This) ) 


#define ISingleRetDefaultInstrumentation2_GetBranchTargetInstruction(This,pInstruction)	\
    ( (This)->lpVtbl -> GetBranchTargetInstruction(This,pInstruction) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ISingleRetDefaultInstrumentation2_INTERFACE_DEFINED__ */


#ifndef __IInstrumentationMethodJitEvents_INTERFACE_DEFINED__
#define __IInstrumentationMethodJitEvents_INTERFACE_DEFINED__

/* interface IInstrumentationMethodJitEvents */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IInstrumentationMethodJitEvents;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("9B028F9E-E2E0-4A61-862B-A4E1158657D0")
    IInstrumentationMethodJitEvents : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE JitStarted( 
            /* [in] */ FunctionID functionID,
            /* [in] */ BOOL isRejit) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE JitComplete( 
            /* [in] */ FunctionID functionID,
            /* [in] */ BOOL isRejit,
            /* [in] */ HRESULT jitResult) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IInstrumentationMethodJitEventsVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IInstrumentationMethodJitEvents * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IInstrumentationMethodJitEvents * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IInstrumentationMethodJitEvents * This);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodJitEvents, JitStarted)
        HRESULT ( STDMETHODCALLTYPE *JitStarted )( 
            __RPC__in IInstrumentationMethodJitEvents * This,
            /* [in] */ FunctionID functionID,
            /* [in] */ BOOL isRejit);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodJitEvents, JitComplete)
        HRESULT ( STDMETHODCALLTYPE *JitComplete )( 
            __RPC__in IInstrumentationMethodJitEvents * This,
            /* [in] */ FunctionID functionID,
            /* [in] */ BOOL isRejit,
            /* [in] */ HRESULT jitResult);
        
        END_INTERFACE
    } IInstrumentationMethodJitEventsVtbl;

    interface IInstrumentationMethodJitEvents
    {
        CONST_VTBL struct IInstrumentationMethodJitEventsVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IInstrumentationMethodJitEvents_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IInstrumentationMethodJitEvents_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IInstrumentationMethodJitEvents_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IInstrumentationMethodJitEvents_JitStarted(This,functionID,isRejit)	\
    ( (This)->lpVtbl -> JitStarted(This,functionID,isRejit) ) 

#define IInstrumentationMethodJitEvents_JitComplete(This,functionID,isRejit,jitResult)	\
    ( (This)->lpVtbl -> JitComplete(This,functionID,isRejit,jitResult) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IInstrumentationMethodJitEvents_INTERFACE_DEFINED__ */


#ifndef __IMethodJitInfo_INTERFACE_DEFINED__
#define __IMethodJitInfo_INTERFACE_DEFINED__

/* interface IMethodJitInfo */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IMethodJitInfo;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("A2A780D6-F337-406C-BA57-F10FBD6C46F9")
    IMethodJitInfo : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetFunctionID( 
            /* [out] */ __RPC__out FunctionID *pFunctionId) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIsRejit( 
            /* [out] */ __RPC__out BOOL *pIsRejit) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetRejitId( 
            /* [out] */ __RPC__out ReJITID *pRejitId) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetJitHR( 
            /* [out] */ __RPC__out HRESULT *pHResult) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetILTransformationStatus( 
            /* [out] */ __RPC__out BOOL *pIsTranformed) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModuleInfo( 
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IMethodJitInfoVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IMethodJitInfo * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IMethodJitInfo * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IMethodJitInfo * This);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo, GetFunctionID)
        HRESULT ( STDMETHODCALLTYPE *GetFunctionID )( 
            __RPC__in IMethodJitInfo * This,
            /* [out] */ __RPC__out FunctionID *pFunctionId);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo, GetIsRejit)
        HRESULT ( STDMETHODCALLTYPE *GetIsRejit )( 
            __RPC__in IMethodJitInfo * This,
            /* [out] */ __RPC__out BOOL *pIsRejit);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo, GetRejitId)
        HRESULT ( STDMETHODCALLTYPE *GetRejitId )( 
            __RPC__in IMethodJitInfo * This,
            /* [out] */ __RPC__out ReJITID *pRejitId);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo, GetJitHR)
        HRESULT ( STDMETHODCALLTYPE *GetJitHR )( 
            __RPC__in IMethodJitInfo * This,
            /* [out] */ __RPC__out HRESULT *pHResult);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo, GetILTransformationStatus)
        HRESULT ( STDMETHODCALLTYPE *GetILTransformationStatus )( 
            __RPC__in IMethodJitInfo * This,
            /* [out] */ __RPC__out BOOL *pIsTranformed);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo, GetModuleInfo)
        HRESULT ( STDMETHODCALLTYPE *GetModuleInfo )( 
            __RPC__in IMethodJitInfo * This,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo);
        
        END_INTERFACE
    } IMethodJitInfoVtbl;

    interface IMethodJitInfo
    {
        CONST_VTBL struct IMethodJitInfoVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IMethodJitInfo_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IMethodJitInfo_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IMethodJitInfo_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IMethodJitInfo_GetFunctionID(This,pFunctionId)	\
    ( (This)->lpVtbl -> GetFunctionID(This,pFunctionId) ) 

#define IMethodJitInfo_GetIsRejit(This,pIsRejit)	\
    ( (This)->lpVtbl -> GetIsRejit(This,pIsRejit) ) 

#define IMethodJitInfo_GetRejitId(This,pRejitId)	\
    ( (This)->lpVtbl -> GetRejitId(This,pRejitId) ) 

#define IMethodJitInfo_GetJitHR(This,pHResult)	\
    ( (This)->lpVtbl -> GetJitHR(This,pHResult) ) 

#define IMethodJitInfo_GetILTransformationStatus(This,pIsTranformed)	\
    ( (This)->lpVtbl -> GetILTransformationStatus(This,pIsTranformed) ) 

#define IMethodJitInfo_GetModuleInfo(This,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetModuleInfo(This,ppModuleInfo) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IMethodJitInfo_INTERFACE_DEFINED__ */


#ifndef __IMethodJitInfo2_INTERFACE_DEFINED__
#define __IMethodJitInfo2_INTERFACE_DEFINED__

/* interface IMethodJitInfo2 */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IMethodJitInfo2;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("8311A7CF-30EC-42C9-85A4-F59713A4F37D")
    IMethodJitInfo2 : public IMethodJitInfo
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetILNativeMapping( 
            /* [in] */ ULONG32 cMaps,
            /* [size_is][out][in] */ __RPC__inout_ecount_full(cMaps) COR_DEBUG_IL_TO_NATIVE_MAP pMap[  ],
            /* [out] */ __RPC__out ULONG32 *pcNeeded) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetILInstrumentationMap( 
            /* [in] */ ULONG32 cMaps,
            /* [size_is][out][in] */ __RPC__inout_ecount_full(cMaps) COR_IL_MAP pMap[  ],
            /* [out] */ __RPC__out ULONG32 *pcNeeded) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMethodToken( 
            /* [out] */ __RPC__out mdMethodDef *pToken) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetNativeCodeAddress( 
            /* [out] */ __RPC__out UINT_PTR *pCodeAddress) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IMethodJitInfo2Vtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IMethodJitInfo2 * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IMethodJitInfo2 * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IMethodJitInfo2 * This);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo, GetFunctionID)
        HRESULT ( STDMETHODCALLTYPE *GetFunctionID )( 
            __RPC__in IMethodJitInfo2 * This,
            /* [out] */ __RPC__out FunctionID *pFunctionId);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo, GetIsRejit)
        HRESULT ( STDMETHODCALLTYPE *GetIsRejit )( 
            __RPC__in IMethodJitInfo2 * This,
            /* [out] */ __RPC__out BOOL *pIsRejit);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo, GetRejitId)
        HRESULT ( STDMETHODCALLTYPE *GetRejitId )( 
            __RPC__in IMethodJitInfo2 * This,
            /* [out] */ __RPC__out ReJITID *pRejitId);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo, GetJitHR)
        HRESULT ( STDMETHODCALLTYPE *GetJitHR )( 
            __RPC__in IMethodJitInfo2 * This,
            /* [out] */ __RPC__out HRESULT *pHResult);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo, GetILTransformationStatus)
        HRESULT ( STDMETHODCALLTYPE *GetILTransformationStatus )( 
            __RPC__in IMethodJitInfo2 * This,
            /* [out] */ __RPC__out BOOL *pIsTranformed);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo, GetModuleInfo)
        HRESULT ( STDMETHODCALLTYPE *GetModuleInfo )( 
            __RPC__in IMethodJitInfo2 * This,
            /* [out] */ __RPC__deref_out_opt IModuleInfo **ppModuleInfo);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo2, GetILNativeMapping)
        HRESULT ( STDMETHODCALLTYPE *GetILNativeMapping )( 
            __RPC__in IMethodJitInfo2 * This,
            /* [in] */ ULONG32 cMaps,
            /* [size_is][out][in] */ __RPC__inout_ecount_full(cMaps) COR_DEBUG_IL_TO_NATIVE_MAP pMap[  ],
            /* [out] */ __RPC__out ULONG32 *pcNeeded);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo2, GetILInstrumentationMap)
        HRESULT ( STDMETHODCALLTYPE *GetILInstrumentationMap )( 
            __RPC__in IMethodJitInfo2 * This,
            /* [in] */ ULONG32 cMaps,
            /* [size_is][out][in] */ __RPC__inout_ecount_full(cMaps) COR_IL_MAP pMap[  ],
            /* [out] */ __RPC__out ULONG32 *pcNeeded);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo2, GetMethodToken)
        HRESULT ( STDMETHODCALLTYPE *GetMethodToken )( 
            __RPC__in IMethodJitInfo2 * This,
            /* [out] */ __RPC__out mdMethodDef *pToken);
        
        DECLSPEC_XFGVIRT(IMethodJitInfo2, GetNativeCodeAddress)
        HRESULT ( STDMETHODCALLTYPE *GetNativeCodeAddress )( 
            __RPC__in IMethodJitInfo2 * This,
            /* [out] */ __RPC__out UINT_PTR *pCodeAddress);
        
        END_INTERFACE
    } IMethodJitInfo2Vtbl;

    interface IMethodJitInfo2
    {
        CONST_VTBL struct IMethodJitInfo2Vtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IMethodJitInfo2_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IMethodJitInfo2_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IMethodJitInfo2_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IMethodJitInfo2_GetFunctionID(This,pFunctionId)	\
    ( (This)->lpVtbl -> GetFunctionID(This,pFunctionId) ) 

#define IMethodJitInfo2_GetIsRejit(This,pIsRejit)	\
    ( (This)->lpVtbl -> GetIsRejit(This,pIsRejit) ) 

#define IMethodJitInfo2_GetRejitId(This,pRejitId)	\
    ( (This)->lpVtbl -> GetRejitId(This,pRejitId) ) 

#define IMethodJitInfo2_GetJitHR(This,pHResult)	\
    ( (This)->lpVtbl -> GetJitHR(This,pHResult) ) 

#define IMethodJitInfo2_GetILTransformationStatus(This,pIsTranformed)	\
    ( (This)->lpVtbl -> GetILTransformationStatus(This,pIsTranformed) ) 

#define IMethodJitInfo2_GetModuleInfo(This,ppModuleInfo)	\
    ( (This)->lpVtbl -> GetModuleInfo(This,ppModuleInfo) ) 


#define IMethodJitInfo2_GetILNativeMapping(This,cMaps,pMap,pcNeeded)	\
    ( (This)->lpVtbl -> GetILNativeMapping(This,cMaps,pMap,pcNeeded) ) 

#define IMethodJitInfo2_GetILInstrumentationMap(This,cMaps,pMap,pcNeeded)	\
    ( (This)->lpVtbl -> GetILInstrumentationMap(This,cMaps,pMap,pcNeeded) ) 

#define IMethodJitInfo2_GetMethodToken(This,pToken)	\
    ( (This)->lpVtbl -> GetMethodToken(This,pToken) ) 

#define IMethodJitInfo2_GetNativeCodeAddress(This,pCodeAddress)	\
    ( (This)->lpVtbl -> GetNativeCodeAddress(This,pCodeAddress) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IMethodJitInfo2_INTERFACE_DEFINED__ */


#ifndef __IInstrumentationMethodJitEvents2_INTERFACE_DEFINED__
#define __IInstrumentationMethodJitEvents2_INTERFACE_DEFINED__

/* interface IInstrumentationMethodJitEvents2 */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IInstrumentationMethodJitEvents2;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("DC5B373D-C38D-4299-83D9-129B6ACCEE2F")
    IInstrumentationMethodJitEvents2 : public IInstrumentationMethodJitEvents
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE JitComplete( 
            /* [in] */ __RPC__in_opt IMethodJitInfo *pJitInfo) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IInstrumentationMethodJitEvents2Vtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IInstrumentationMethodJitEvents2 * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IInstrumentationMethodJitEvents2 * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IInstrumentationMethodJitEvents2 * This);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodJitEvents, JitStarted)
        HRESULT ( STDMETHODCALLTYPE *JitStarted )( 
            __RPC__in IInstrumentationMethodJitEvents2 * This,
            /* [in] */ FunctionID functionID,
            /* [in] */ BOOL isRejit);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodJitEvents, JitComplete)
        HRESULT ( STDMETHODCALLTYPE *JitComplete )( 
            __RPC__in IInstrumentationMethodJitEvents2 * This,
            /* [in] */ FunctionID functionID,
            /* [in] */ BOOL isRejit,
            /* [in] */ HRESULT jitResult);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodJitEvents2, JitComplete)
        HRESULT ( STDMETHODCALLTYPE *JitComplete )( 
            __RPC__in IInstrumentationMethodJitEvents2 * This,
            /* [in] */ __RPC__in_opt IMethodJitInfo *pJitInfo);
        
        END_INTERFACE
    } IInstrumentationMethodJitEvents2Vtbl;

    interface IInstrumentationMethodJitEvents2
    {
        CONST_VTBL struct IInstrumentationMethodJitEvents2Vtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IInstrumentationMethodJitEvents2_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IInstrumentationMethodJitEvents2_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IInstrumentationMethodJitEvents2_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IInstrumentationMethodJitEvents2_JitStarted(This,functionID,isRejit)	\
    ( (This)->lpVtbl -> JitStarted(This,functionID,isRejit) ) 

#define IInstrumentationMethodJitEvents2_JitComplete(This,functionID,isRejit,jitResult)	\
    ( (This)->lpVtbl -> JitComplete(This,functionID,isRejit,jitResult) ) 


#define IInstrumentationMethodJitEvents2_JitComplete(This,pJitInfo)	\
    ( (This)->lpVtbl -> JitComplete(This,pJitInfo) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IInstrumentationMethodJitEvents2_INTERFACE_DEFINED__ */


#ifndef __IInstrumentationMethodSetting_INTERFACE_DEFINED__
#define __IInstrumentationMethodSetting_INTERFACE_DEFINED__

/* interface IInstrumentationMethodSetting */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IInstrumentationMethodSetting;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("EF0B0C79-08E7-4C3A-A4C5-02A9C9CE8809")
    IInstrumentationMethodSetting : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetName( 
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetValue( 
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrValue) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IInstrumentationMethodSettingVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IInstrumentationMethodSetting * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IInstrumentationMethodSetting * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IInstrumentationMethodSetting * This);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodSetting, GetName)
        HRESULT ( STDMETHODCALLTYPE *GetName )( 
            __RPC__in IInstrumentationMethodSetting * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrName);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodSetting, GetValue)
        HRESULT ( STDMETHODCALLTYPE *GetValue )( 
            __RPC__in IInstrumentationMethodSetting * This,
            /* [out] */ __RPC__deref_out_opt BSTR *pbstrValue);
        
        END_INTERFACE
    } IInstrumentationMethodSettingVtbl;

    interface IInstrumentationMethodSetting
    {
        CONST_VTBL struct IInstrumentationMethodSettingVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IInstrumentationMethodSetting_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IInstrumentationMethodSetting_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IInstrumentationMethodSetting_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IInstrumentationMethodSetting_GetName(This,pbstrName)	\
    ( (This)->lpVtbl -> GetName(This,pbstrName) ) 

#define IInstrumentationMethodSetting_GetValue(This,pbstrValue)	\
    ( (This)->lpVtbl -> GetValue(This,pbstrValue) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IInstrumentationMethodSetting_INTERFACE_DEFINED__ */


#ifndef __IEnumInstrumentationMethodSettings_INTERFACE_DEFINED__
#define __IEnumInstrumentationMethodSettings_INTERFACE_DEFINED__

/* interface IEnumInstrumentationMethodSettings */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IEnumInstrumentationMethodSettings;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("9B03D87E-72F0-4D8E-A4B1-15BCD8227073")
    IEnumInstrumentationMethodSettings : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Next( 
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IInstrumentationMethodSetting **rgelt,
            /* [out] */ __RPC__out ULONG *pceltFetched) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Skip( 
            /* [in] */ ULONG celt) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Reset( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Clone( 
            /* [out] */ __RPC__deref_out_opt IEnumInstrumentationMethodSettings **ppenum) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetCount( 
            /* [out] */ __RPC__out DWORD *pLength) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IEnumInstrumentationMethodSettingsVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IEnumInstrumentationMethodSettings * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IEnumInstrumentationMethodSettings * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IEnumInstrumentationMethodSettings * This);
        
        DECLSPEC_XFGVIRT(IEnumInstrumentationMethodSettings, Next)
        HRESULT ( STDMETHODCALLTYPE *Next )( 
            __RPC__in IEnumInstrumentationMethodSettings * This,
            /* [in] */ ULONG celt,
            /* [length_is][size_is][out] */ __RPC__out_ecount_part(celt, celt) IInstrumentationMethodSetting **rgelt,
            /* [out] */ __RPC__out ULONG *pceltFetched);
        
        DECLSPEC_XFGVIRT(IEnumInstrumentationMethodSettings, Skip)
        HRESULT ( STDMETHODCALLTYPE *Skip )( 
            __RPC__in IEnumInstrumentationMethodSettings * This,
            /* [in] */ ULONG celt);
        
        DECLSPEC_XFGVIRT(IEnumInstrumentationMethodSettings, Reset)
        HRESULT ( STDMETHODCALLTYPE *Reset )( 
            __RPC__in IEnumInstrumentationMethodSettings * This);
        
        DECLSPEC_XFGVIRT(IEnumInstrumentationMethodSettings, Clone)
        HRESULT ( STDMETHODCALLTYPE *Clone )( 
            __RPC__in IEnumInstrumentationMethodSettings * This,
            /* [out] */ __RPC__deref_out_opt IEnumInstrumentationMethodSettings **ppenum);
        
        DECLSPEC_XFGVIRT(IEnumInstrumentationMethodSettings, GetCount)
        HRESULT ( STDMETHODCALLTYPE *GetCount )( 
            __RPC__in IEnumInstrumentationMethodSettings * This,
            /* [out] */ __RPC__out DWORD *pLength);
        
        END_INTERFACE
    } IEnumInstrumentationMethodSettingsVtbl;

    interface IEnumInstrumentationMethodSettings
    {
        CONST_VTBL struct IEnumInstrumentationMethodSettingsVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IEnumInstrumentationMethodSettings_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IEnumInstrumentationMethodSettings_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IEnumInstrumentationMethodSettings_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IEnumInstrumentationMethodSettings_Next(This,celt,rgelt,pceltFetched)	\
    ( (This)->lpVtbl -> Next(This,celt,rgelt,pceltFetched) ) 

#define IEnumInstrumentationMethodSettings_Skip(This,celt)	\
    ( (This)->lpVtbl -> Skip(This,celt) ) 

#define IEnumInstrumentationMethodSettings_Reset(This)	\
    ( (This)->lpVtbl -> Reset(This) ) 

#define IEnumInstrumentationMethodSettings_Clone(This,ppenum)	\
    ( (This)->lpVtbl -> Clone(This,ppenum) ) 

#define IEnumInstrumentationMethodSettings_GetCount(This,pLength)	\
    ( (This)->lpVtbl -> GetCount(This,pLength) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IEnumInstrumentationMethodSettings_INTERFACE_DEFINED__ */


#ifndef __IInstrumentationMethodAttachContext_INTERFACE_DEFINED__
#define __IInstrumentationMethodAttachContext_INTERFACE_DEFINED__

/* interface IInstrumentationMethodAttachContext */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IInstrumentationMethodAttachContext;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("2C37B76C-B350-4738-8B29-B92C7ED6C522")
    IInstrumentationMethodAttachContext : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE EnumSettings( 
            /* [out] */ __RPC__deref_out_opt IEnumInstrumentationMethodSettings **ppEnum) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IInstrumentationMethodAttachContextVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IInstrumentationMethodAttachContext * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IInstrumentationMethodAttachContext * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IInstrumentationMethodAttachContext * This);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodAttachContext, EnumSettings)
        HRESULT ( STDMETHODCALLTYPE *EnumSettings )( 
            __RPC__in IInstrumentationMethodAttachContext * This,
            /* [out] */ __RPC__deref_out_opt IEnumInstrumentationMethodSettings **ppEnum);
        
        END_INTERFACE
    } IInstrumentationMethodAttachContextVtbl;

    interface IInstrumentationMethodAttachContext
    {
        CONST_VTBL struct IInstrumentationMethodAttachContextVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IInstrumentationMethodAttachContext_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IInstrumentationMethodAttachContext_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IInstrumentationMethodAttachContext_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IInstrumentationMethodAttachContext_EnumSettings(This,ppEnum)	\
    ( (This)->lpVtbl -> EnumSettings(This,ppEnum) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IInstrumentationMethodAttachContext_INTERFACE_DEFINED__ */


#ifndef __IInstrumentationMethodAttach_INTERFACE_DEFINED__
#define __IInstrumentationMethodAttach_INTERFACE_DEFINED__

/* interface IInstrumentationMethodAttach */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_IInstrumentationMethodAttach;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("3BD6C171-4F3C-45C3-8CB9-BC8C337D1683")
    IInstrumentationMethodAttach : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE InitializeForAttach( 
            /* [in] */ __RPC__in_opt IProfilerManager *pProfilerManager,
            /* [in] */ __RPC__in_opt IInstrumentationMethodAttachContext *pContext) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AttachComplete( void) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IInstrumentationMethodAttachVtbl
    {
        BEGIN_INTERFACE
        
        DECLSPEC_XFGVIRT(IUnknown, QueryInterface)
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __RPC__in IInstrumentationMethodAttach * This,
            /* [in] */ __RPC__in REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        DECLSPEC_XFGVIRT(IUnknown, AddRef)
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __RPC__in IInstrumentationMethodAttach * This);
        
        DECLSPEC_XFGVIRT(IUnknown, Release)
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __RPC__in IInstrumentationMethodAttach * This);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodAttach, InitializeForAttach)
        HRESULT ( STDMETHODCALLTYPE *InitializeForAttach )( 
            __RPC__in IInstrumentationMethodAttach * This,
            /* [in] */ __RPC__in_opt IProfilerManager *pProfilerManager,
            /* [in] */ __RPC__in_opt IInstrumentationMethodAttachContext *pContext);
        
        DECLSPEC_XFGVIRT(IInstrumentationMethodAttach, AttachComplete)
        HRESULT ( STDMETHODCALLTYPE *AttachComplete )( 
            __RPC__in IInstrumentationMethodAttach * This);
        
        END_INTERFACE
    } IInstrumentationMethodAttachVtbl;

    interface IInstrumentationMethodAttach
    {
        CONST_VTBL struct IInstrumentationMethodAttachVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IInstrumentationMethodAttach_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IInstrumentationMethodAttach_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IInstrumentationMethodAttach_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IInstrumentationMethodAttach_InitializeForAttach(This,pProfilerManager,pContext)	\
    ( (This)->lpVtbl -> InitializeForAttach(This,pProfilerManager,pContext) ) 

#define IInstrumentationMethodAttach_AttachComplete(This)	\
    ( (This)->lpVtbl -> AttachComplete(This) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IInstrumentationMethodAttach_INTERFACE_DEFINED__ */

#endif /* __MicrosoftInstrumentationEngine_LIBRARY_DEFINED__ */

/* Additional Prototypes for ALL interfaces */

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


