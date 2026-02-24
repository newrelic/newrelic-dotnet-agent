// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! IMetaDataImport COM interface definition
//!
//! Derived from Microsoft's cor.h header in the .NET runtime (MIT licensed).
//! This interface is used to read metadata from loaded .NET assemblies —
//! specifically to resolve method tokens into human-readable names.
//!
//! Only GetMethodProps and GetTypeDefProps are called in the POC.
//! All other methods use correct vtable positions with opaque parameter types.

use crate::ffi::*;
use com::{
    interfaces::IUnknown,
    sys::{GUID, HRESULT},
};
use std::ffi::c_void;

// Additional metadata token types needed by IMetaDataImport
pub type HCORENUM = *mut c_void;
pub type LPCWSTR = *const WCHAR;
pub type PCCOR_SIGNATURE = *const u8;
pub type UVCP_CONSTANT = *const c_void;
pub type MDUTF8CSTR = *const u8;
pub type mdTypeRef = mdToken;
pub type mdInterfaceImpl = mdToken;
pub type mdMemberRef = mdToken;
pub type mdParamDef = mdToken;
pub type mdProperty = mdToken;
pub type mdEvent = mdToken;
pub type mdPermission = mdToken;
pub type mdSignature = mdToken;
pub type mdModuleRef = mdToken;
pub type mdTypeSpec = mdToken;
pub type mdString = mdToken;
pub type mdCustomAttribute = mdToken;
pub type mdModule = mdToken;
pub type COR_FIELD_OFFSET = u64; // struct { mdFieldDef, ULONG } but we just need the size right
#[allow(non_camel_case_types)]
pub type int = i32;

interfaces! {
    /// IMetaDataImport - reads metadata from .NET assemblies.
    /// GUID: 7DAC8207-D3AE-4c75-9B67-92801A497D44
    ///
    /// ~50 methods. We only call GetMethodProps and GetTypeDefProps.
    /// Other methods maintain correct vtable positions with opaque types.
    #[uuid("7DAC8207-D3AE-4c75-9B67-92801A497D44")]
    pub unsafe interface IMetaDataImport: IUnknown {
        // 1 - Note: CloseEnum returns void, not HRESULT. The com crate requires HRESULT
        // returns, so we define it as returning HRESULT but never check the return value.
        pub fn CloseEnum(&self, hEnum: HCORENUM) -> HRESULT;
        // 2
        pub fn CountEnum(&self, hEnum: HCORENUM, pulCount: *mut ULONG) -> HRESULT;
        // 3
        pub fn ResetEnum(&self, hEnum: HCORENUM, ulPos: *const ULONG) -> HRESULT;
        // 4
        pub fn EnumTypeDefs(&self, phEnum: *mut HCORENUM, rTypeDefs: *const mdTypeDef, cMax: ULONG, pcTypeDefs: *mut ULONG) -> HRESULT;
        // 5
        pub fn EnumInterfaceImpls(&self, phEnum: *mut HCORENUM, td: mdTypeDef, rImpls: *mut mdInterfaceImpl, cMax: ULONG, pcImpls: *mut ULONG) -> HRESULT;
        // 6
        pub fn EnumTypeRefs(&self, phEnum: *mut HCORENUM, rTypeRefs: *mut mdTypeRef, cMax: ULONG, pcTypeRefs: *mut ULONG) -> HRESULT;
        // 7
        pub fn FindTypeDefByName(&self, szTypeDef: LPCWSTR, tkEnclosingClass: mdToken, ptd: *mut mdTypeDef) -> HRESULT;
        // 8
        pub fn GetScopeProps(&self, szName: *mut WCHAR, cchName: ULONG, pchName: *mut ULONG, pmvid: *mut GUID) -> HRESULT;
        // 9
        pub fn GetModuleFromScope(&self, pmd: *mut mdModule) -> HRESULT;
        // 10 ** Used: resolves type token to type name **
        pub fn GetTypeDefProps(&self, td: mdTypeDef, szTypeDef: *mut WCHAR, cchTypeDef: ULONG, pchTypeDef: *mut ULONG, pdwTypeDefFlags: *mut DWORD, ptkExtends: *mut mdToken) -> HRESULT;
        // 11
        pub fn GetInterfaceImplProps(&self, iiImpl: mdInterfaceImpl, pClass: *mut mdTypeDef, ptkIface: *mut mdToken) -> HRESULT;
        // 12
        pub fn GetTypeRefProps(&self, tr: mdTypeRef, ptkResolutionScope: *mut mdToken, szName: *mut WCHAR, cchName: ULONG, pchName: *mut ULONG) -> HRESULT;
        // 13
        pub fn ResolveTypeRef(&self, tr: mdTypeRef, riid: REFIID, ppIScope: *mut *mut IUnknown, ptd: *mut mdTypeDef) -> HRESULT;
        // 14
        pub fn EnumMembers(&self, phEnum: *mut HCORENUM, cl: mdTypeDef, rMembers: *mut mdToken, cMax: ULONG, pcTokens: *mut ULONG) -> HRESULT;
        // 15
        pub fn EnumMembersWithName(&self, phEnum: *mut HCORENUM, cl: mdTypeDef, szName: LPCWSTR, rMembers: *mut mdToken, cMax: ULONG, pcTokens: *mut ULONG) -> HRESULT;
        // 16
        pub fn EnumMethods(&self, phEnum: *mut HCORENUM, cl: mdTypeDef, rMethods: *mut mdMethodDef, cMax: ULONG, pcTokens: *mut ULONG) -> HRESULT;
        // 17
        pub fn EnumMethodsWithName(&self, phEnum: *mut HCORENUM, cl: mdTypeDef, szName: LPCWSTR, rMethods: *mut mdMethodDef, cMax: ULONG, pcTokens: *mut ULONG) -> HRESULT;
        // 18
        pub fn EnumFields(&self, phEnum: *mut HCORENUM, cl: mdTypeDef, rFields: *mut mdFieldDef, cMax: ULONG, pcTokens: *mut ULONG) -> HRESULT;
        // 19
        pub fn EnumFieldsWithName(&self, phEnum: *mut HCORENUM, cl: mdTypeDef, szName: LPCWSTR, rFields: *mut mdFieldDef, cMax: ULONG, pcTokens: *mut ULONG) -> HRESULT;
        // 20
        pub fn EnumParams(&self, phEnum: *mut HCORENUM, mb: mdMethodDef, rParams: *mut mdParamDef, cMax: ULONG, pcTokens: *mut ULONG) -> HRESULT;
        // 21
        pub fn EnumMemberRefs(&self, phEnum: *mut HCORENUM, tkParent: mdToken, rMemberRefs: *mut mdMemberRef, cMax: ULONG, pcTokens: *mut ULONG) -> HRESULT;
        // 22
        pub fn EnumMethodImpls(&self, phEnum: *mut HCORENUM, td: mdTypeDef, rMethodBody: *mut mdToken, rMethodDecl: *mut mdToken, cMax: ULONG, pcTokens: *mut ULONG) -> HRESULT;
        // 23
        pub fn EnumPermissionSets(&self, phEnum: *mut HCORENUM, tk: mdToken, dwActions: DWORD, rPermission: *mut mdPermission, cMax: ULONG, pcTokens: *mut ULONG) -> HRESULT;
        // 24
        pub fn FindMember(&self, td: mdTypeDef, szName: LPCWSTR, pvSigBlob: PCCOR_SIGNATURE, cbSigBlob: ULONG, pmb: *mut mdToken) -> HRESULT;
        // 25
        pub fn FindMethod(&self, td: mdTypeDef, szName: LPCWSTR, pvSigBlob: PCCOR_SIGNATURE, cbSigBlob: ULONG, pmb: *mut mdMethodDef) -> HRESULT;
        // 26
        pub fn FindField(&self, td: mdTypeDef, szName: LPCWSTR, pvSigBlob: PCCOR_SIGNATURE, cbSigBlob: ULONG, pmb: *mut mdFieldDef) -> HRESULT;
        // 27
        pub fn FindMemberRef(&self, td: mdTypeRef, szName: LPCWSTR, pvSigBlob: PCCOR_SIGNATURE, cbSigBlob: ULONG, pmr: *mut mdMemberRef) -> HRESULT;
        // 28 ** Used: resolves method token to method name and type token **
        pub fn GetMethodProps(&self, mb: mdMethodDef, pClass: *mut mdTypeDef, szMethod: *mut WCHAR, cchMethod: ULONG, pchMethod: *mut ULONG, pdwAttr: *mut DWORD, ppvSigBlob: *mut PCCOR_SIGNATURE, pcbSigBlob: *mut ULONG, pulCodeRVA: *mut ULONG, pdwImplFlags: *mut DWORD) -> HRESULT;
        // 29
        pub fn GetMemberRefProps(&self, mr: mdMemberRef, ptk: *mut mdToken, szMember: *mut WCHAR, cchMember: ULONG, pchMember: *mut ULONG, ppvSigBlob: *mut PCCOR_SIGNATURE, pbSig: *mut ULONG) -> HRESULT;
        // 30
        pub fn EnumProperties(&self, phEnum: *mut HCORENUM, td: mdTypeDef, rProperties: *mut mdProperty, cMax: ULONG, pcProperties: *mut ULONG) -> HRESULT;
        // 31
        pub fn EnumEvents(&self, phEnum: *mut HCORENUM, td: mdTypeDef, rEvents: *mut mdEvent, cMax: ULONG, pcEvents: *mut ULONG) -> HRESULT;
        // 32
        pub fn GetEventProps(&self, ev: mdEvent, pClass: *mut mdTypeDef, szEvent: *mut WCHAR, cchEvent: ULONG, pchEvent: *mut ULONG, pdwEventFlags: *mut DWORD, ptkEventType: *mut mdToken, pmdAddOn: *mut mdMethodDef, pmdRemoveOn: *mut mdMethodDef, pmdFire: *mut mdMethodDef, rmdOtherMethod: *mut mdMethodDef, cMax: ULONG, pcOtherMethod: *mut ULONG) -> HRESULT;
        // 33
        pub fn EnumMethodSemantics(&self, phEnum: *mut HCORENUM, mb: mdMethodDef, rEventProp: *mut mdToken, cMax: ULONG, pcEventProp: *mut ULONG) -> HRESULT;
        // 34
        pub fn GetMethodSemantics(&self, mb: mdMethodDef, tkEventProp: mdToken, pdwSemanticsFlags: *mut DWORD) -> HRESULT;
        // 35
        pub fn GetClassLayout(&self, td: mdTypeDef, pdwPackSize: *mut DWORD, rFieldOffset: *mut c_void, cMax: ULONG, pcFieldOffset: *mut ULONG, pulClassSize: *mut ULONG) -> HRESULT;
        // 36
        pub fn GetFieldMarshal(&self, tk: mdToken, ppvNativeType: *mut PCCOR_SIGNATURE, pcbNativeType: *mut ULONG) -> HRESULT;
        // 37
        pub fn GetRVA(&self, tk: mdToken, pulCodeRVA: *mut ULONG, pdwImplFlags: *mut DWORD) -> HRESULT;
        // 38
        pub fn GetPermissionSetProps(&self, pm: mdPermission, pdwAction: *mut DWORD, ppvPermission: *mut *mut c_void, pcbPermission: *mut ULONG) -> HRESULT;
        // 39
        pub fn GetSigFromToken(&self, mdSig: mdSignature, ppvSig: *mut PCCOR_SIGNATURE, pcbSig: *mut ULONG) -> HRESULT;
        // 40
        pub fn GetModuleRefProps(&self, mur: mdModuleRef, szName: *mut WCHAR, cchName: ULONG, pchName: *mut ULONG) -> HRESULT;
        // 41
        pub fn EnumModuleRefs(&self, phEnum: *mut HCORENUM, rModuleRefs: *mut mdModuleRef, cmax: ULONG, pcModuleRefs: *mut ULONG) -> HRESULT;
        // 42
        pub fn GetTypeSpecFromToken(&self, typespec: mdTypeSpec, ppvSig: *mut PCCOR_SIGNATURE, pcbSig: *mut ULONG) -> HRESULT;
        // 43
        pub fn GetNameFromToken(&self, tk: mdToken, pszUtf8NamePtr: *mut MDUTF8CSTR) -> HRESULT;
        // 44
        pub fn EnumUnresolvedMethods(&self, phEnum: *mut HCORENUM, rMethods: *mut mdToken, cMax: ULONG, pcTokens: *mut ULONG) -> HRESULT;
        // 45
        pub fn GetUserString(&self, stk: mdString, szString: *mut WCHAR, cchString: ULONG, pchString: *mut ULONG) -> HRESULT;
        // 46
        pub fn GetPinvokeMap(&self, tk: mdToken, pdwMappingFlags: *mut DWORD, szImportName: *mut WCHAR, cchImportName: ULONG, pchImportName: *mut ULONG, pmrImportDLL: *mut mdModuleRef) -> HRESULT;
        // 47
        pub fn EnumSignatures(&self, phEnum: *mut HCORENUM, rSignatures: *mut mdSignature, cMax: ULONG, pcSignatures: *mut ULONG) -> HRESULT;
        // 48
        pub fn EnumTypeSpecs(&self, phEnum: *mut HCORENUM, rTypeSpecs: *mut mdTypeSpec, cMax: ULONG, pcTypeSpecs: *mut ULONG) -> HRESULT;
        // 49
        pub fn EnumUserStrings(&self, phEnum: *mut HCORENUM, rStrings: *mut mdString, cMax: ULONG, pcStrings: *mut ULONG) -> HRESULT;
        // 50
        pub fn GetParamForMethodIndex(&self, md: mdMethodDef, ulParamSeq: ULONG, ppd: *mut mdParamDef) -> HRESULT;
        // 51
        pub fn EnumCustomAttributes(&self, phEnum: *mut HCORENUM, tk: mdToken, tkType: mdToken, rCustomAttributes: *mut mdCustomAttribute, cMax: ULONG, pcCustomAttributes: *mut ULONG) -> HRESULT;
        // 52
        pub fn GetCustomAttributeProps(&self, cv: mdCustomAttribute, ptkObj: *mut mdToken, ptkType: *mut mdToken, ppBlob: *mut *mut c_void, pcbSize: *mut ULONG) -> HRESULT;
        // 53
        pub fn FindTypeRef(&self, tkResolutionScope: mdToken, szName: LPCWSTR, ptr: *mut mdTypeRef) -> HRESULT;
        // 54
        pub fn GetMemberProps(&self, mb: mdToken, pClass: *mut mdTypeDef, szMember: *mut WCHAR, cchMember: ULONG, pchMember: *mut ULONG, pdwAttr: *mut DWORD, ppvSigBlob: *mut PCCOR_SIGNATURE, pcbSigBlob: *mut ULONG, pulCodeRVA: *mut ULONG, pdwImplFlags: *mut DWORD, pdwCPlusTypeFlag: *mut DWORD, ppValue: *mut UVCP_CONSTANT, pcchValue: *mut ULONG) -> HRESULT;
        // 55
        pub fn GetFieldProps(&self, mb: mdToken, pClass: *mut mdTypeDef, szField: *mut WCHAR, cchField: ULONG, pchField: *mut ULONG, pdwAttr: *mut DWORD, ppvSigBlob: *mut PCCOR_SIGNATURE, pcbSigBlob: *mut ULONG, pdwCPlusTypeFlag: *mut DWORD, ppValue: *mut UVCP_CONSTANT, pcchValue: *mut ULONG) -> HRESULT;
        // 56
        pub fn GetPropertyProps(&self, prop: mdProperty, pClass: *mut mdTypeDef, szProperty: *mut WCHAR, cchProperty: ULONG, pchProperty: *mut ULONG, pdwPropFlags: *mut DWORD, ppvSig: *mut PCCOR_SIGNATURE, pbSig: *mut ULONG, pdwCPlusTypeFlag: *mut DWORD, ppDefaultValue: *mut UVCP_CONSTANT, pcchDefaultValue: *mut ULONG, pmdSetter: *mut mdMethodDef, pmdGetter: *mut mdMethodDef, rmdOtherMethod: *mut mdMethodDef, cMax: ULONG, pcOtherMethod: *mut ULONG) -> HRESULT;
        // 57
        pub fn GetParamProps(&self, tk: mdParamDef, pmd: *mut mdMethodDef, pulSequence: *mut ULONG, szName: *mut WCHAR, cchName: ULONG, pchName: *mut ULONG, pdwAttr: *mut DWORD, pdwCPlusTypeFlag: *mut DWORD, ppValue: *mut UVCP_CONSTANT, pcchValue: *mut ULONG) -> HRESULT;
        // 58
        pub fn GetCustomAttributeByName(&self, tkObj: mdToken, szName: LPCWSTR, ppData: *mut *mut c_void, pcbData: *mut ULONG) -> HRESULT;
        // 59 - Note: returns BOOL, not HRESULT. Same ABI (both are i32).
        pub fn IsValidToken(&self, tk: mdToken) -> HRESULT;
        // 60
        pub fn GetNestedClassProps(&self, tdNestedClass: mdTypeDef, ptdEnclosingClass: *mut mdTypeDef) -> HRESULT;
        // 61
        pub fn GetNativeCallConvFromSig(&self, pvSig: *const c_void, cbSig: ULONG, pCallConv: *mut ULONG) -> HRESULT;
        // 62
        pub fn IsGlobal(&self, pd: mdToken, pbGlobal: *mut i32) -> HRESULT;
    }
}
